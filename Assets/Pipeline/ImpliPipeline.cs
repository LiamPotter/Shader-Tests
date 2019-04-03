using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Conditional = System.Diagnostics.ConditionalAttribute;
public class ImpliPipeline : RenderPipeline
{
    CullResults cull;

    CommandBuffer cameraBuffer = new CommandBuffer { name = "Render Camera" };

    CommandBuffer shadowBuffer = new CommandBuffer { name = "Render Shadows" };

    RenderTexture shadowMap;

    Material errorMaterial;

    DrawRendererFlags drawFlags;

    const int maxVisibleLights = 16;

    #region Static IDs
    static int visibleLightColorsId 
        = Shader.PropertyToID("_VisibleLightColors");
    static int visibleLightDirectionsOrPositionsId 
        = Shader.PropertyToID("_VisibleLightDirectionsOrPositions");
    static int visibleLightAttenuationsId
        = Shader.PropertyToID("_VisibleLightAttenuations");
    static int visibleLightSpotDirectionsId
        = Shader.PropertyToID("_VisibleLightSpotDirections");
    static int lightIndicesOffsetAndCountId
        = Shader.PropertyToID("unity_LightIndicesOffsetAndCount");
    static int shadowMapId 
        = Shader.PropertyToID("_ShadowMap");
    static int worldToShadowMatrixId
        = Shader.PropertyToID("_WorldToShadowMatrix");
    static int shadowBiasId
        = Shader.PropertyToID("_ShadowBias");
    #endregion

    Vector4[] visibleLightColors = new Vector4[maxVisibleLights];
    Vector4[] visibleLightDirectionsOrPositions = new Vector4[maxVisibleLights];
    Vector4[] visibleLightAttenuations = new Vector4[maxVisibleLights];
    Vector4[] visibleLightSpotDirections = new Vector4[maxVisibleLights];

    int shadowMapSize;

    public ImpliPipeline(bool dynamicBatching, bool instancing, int shadowMapSize)
    {
        GraphicsSettings.lightsUseLinearIntensity = true;
        if (dynamicBatching)
            drawFlags = DrawRendererFlags.EnableDynamicBatching;
        if (instancing)
            drawFlags |= DrawRendererFlags.EnableInstancing;
        this.shadowMapSize = shadowMapSize;
    }

    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        base.Render(renderContext, cameras);
        foreach (var camera in cameras)
        {
            Render(renderContext, camera);
        }

    }

    void Render(ScriptableRenderContext context, Camera camera)
    {
        ScriptableCullingParameters cullingParameters;
        if (!CullResults.GetCullingParameters(camera, out cullingParameters))
            return;
#if UNITY_EDITOR
        if (camera.cameraType == CameraType.SceneView)
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
#endif
        CullResults.Cull(ref cullingParameters, context, ref cull);

        if (cull.visibleLights.Count > 0)
            RenderShadows(context);

        context.SetupCameraProperties(camera);

        CameraClearFlags clearflags = camera.clearFlags;
        cameraBuffer.ClearRenderTarget(
            (clearflags & CameraClearFlags.Depth) != 0,
            (clearflags & CameraClearFlags.Color) != 0,
            camera.backgroundColor);

        if (cull.visibleLights.Count > 0)
            ConfigureLights();
        else
        {
            cameraBuffer.SetGlobalVector(lightIndicesOffsetAndCountId, Vector4.zero);
        }
        cameraBuffer.BeginSample("Render Camera");

        cameraBuffer.SetGlobalVectorArray(visibleLightColorsId, visibleLightColors);
        cameraBuffer.SetGlobalVectorArray(visibleLightDirectionsOrPositionsId, visibleLightDirectionsOrPositions);
        cameraBuffer.SetGlobalVectorArray(visibleLightAttenuationsId, visibleLightAttenuations);
        cameraBuffer.SetGlobalVectorArray(visibleLightSpotDirectionsId, visibleLightSpotDirections);

        context.ExecuteCommandBuffer(cameraBuffer);
        cameraBuffer.Clear();

        var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("SRPDefaultUnlit"))
        {
            flags = drawFlags,
          
        };
        if(cull.visibleLights.Count>0)
        {
            drawSettings.rendererConfiguration = RendererConfiguration.PerObjectLightIndices8;
        }
        drawSettings.sorting.flags = SortFlags.CommonOpaque;

        var filterSettings = new FilterRenderersSettings(true)
        {
            renderQueueRange = RenderQueueRange.opaque
        };

        context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);

        context.DrawSkybox(camera);

        drawSettings.sorting.flags = SortFlags.CommonTransparent;
        filterSettings.renderQueueRange = RenderQueueRange.transparent;

        context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);

        DrawDefaultPipeline(context, camera);

        cameraBuffer.EndSample("Render Camera");
        context.ExecuteCommandBuffer(cameraBuffer);
        cameraBuffer.Clear();

        context.Submit();

        if(shadowMap)
        {
            RenderTexture.ReleaseTemporary(shadowMap);
            shadowMap = null;
        }
    }

    private void ConfigureLights()
    {

        for (int i = 0; i < cull.visibleLights.Count; i++)
        {
            if (i == maxVisibleLights)
                break;

            VisibleLight light = cull.visibleLights[i];
            visibleLightColors[i] = light.finalColor;

            Vector4 attenuation = Vector4.zero;
            attenuation.w = 1f;

            if (light.lightType == LightType.Directional)
            {
                Vector4 dir = light.localToWorld.GetColumn(2);
                dir.x = -dir.x;
                dir.y = -dir.y;
                dir.z = -dir.z;
                visibleLightDirectionsOrPositions[i] = dir;
            }
            else
            {
                visibleLightDirectionsOrPositions[i] = light.localToWorld.GetColumn(3);
                attenuation.x = 1f / Mathf.Max(light.range * light.range, 0.00001f);

                if (light.lightType == LightType.Spot)
                {
                    Vector4 dir = light.localToWorld.GetColumn(2);
                    dir.x = -dir.x;
                    dir.y = -dir.y;
                    dir.z = -dir.z;
                    visibleLightSpotDirections[i] = dir;

                    float outerRad = Mathf.Deg2Rad * 0.5f * light.spotAngle;
                    float outerCos = Mathf.Cos(outerRad);
                    float outerTan = Mathf.Tan(outerRad);
                    float innerCos = Mathf.Cos(Mathf.Atan(((46f / 64f) * outerTan)));
                    float angleRange = Mathf.Max(innerCos - outerCos, 0.00001f);
                    attenuation.z = 1f / angleRange;
                    attenuation.w = -outerCos * attenuation.z;
                }
            }

            visibleLightAttenuations[i] = attenuation;
        }
        if (cull.visibleLights.Count > maxVisibleLights)
        {
            int[] lightIndices = cull.GetLightIndexMap();
            for (int i = maxVisibleLights; i < cull.visibleLights.Count; i++)
            {
                lightIndices[i] = -1;
            }
            cull.SetLightIndexMap(lightIndices);
        }
    }

    private void RenderShadows(ScriptableRenderContext context)
    {
        shadowMap = RenderTexture.GetTemporary(shadowMapSize, shadowMapSize, 16, RenderTextureFormat.Shadowmap);
        shadowMap.filterMode = FilterMode.Bilinear;
        shadowMap.wrapMode = TextureWrapMode.Clamp;

        CoreUtils.SetRenderTarget(
            shadowBuffer, 
            shadowMap,
            RenderBufferLoadAction.DontCare,
            RenderBufferStoreAction.Store,
            ClearFlag.Depth
           );
        shadowBuffer.BeginSample("Render Shadows");
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();

        Matrix4x4 viewMatrix, projectionMatrix;
        ShadowSplitData splitData;
        cull.ComputeSpotShadowMatricesAndCullingPrimitives(
            0, out viewMatrix, out projectionMatrix, out splitData);

        shadowBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        shadowBuffer.SetGlobalFloat(shadowBiasId, cull.visibleLights[0].light.shadowBias);
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();

        var shadowSettings = new DrawShadowsSettings(cull, 0);
        context.DrawShadows(ref shadowSettings);

        if(SystemInfo.usesReversedZBuffer)
        {
            projectionMatrix.m20 = -projectionMatrix.m20;
            projectionMatrix.m21 = -projectionMatrix.m21;
            projectionMatrix.m22 = -projectionMatrix.m22;
            projectionMatrix.m23 = -projectionMatrix.m23;
        }
        var scaleOffset = Matrix4x4.identity;
        scaleOffset.m00 = scaleOffset.m11 = scaleOffset.m22 = 0.5f;
        scaleOffset.m03 = scaleOffset.m13 = scaleOffset.m23 = 0.5f;

        Matrix4x4 worldToShadowMatrix = scaleOffset*(projectionMatrix * viewMatrix);
        shadowBuffer.SetGlobalMatrix(worldToShadowMatrixId, worldToShadowMatrix);
        shadowBuffer.SetGlobalTexture(shadowMapId, shadowMap);

        shadowBuffer.EndSample("Render Shadows");
    }

    [Conditional("DEVELOPMENT_BUILD"),Conditional("UNITY_EDITOR")]
    private void DrawDefaultPipeline(ScriptableRenderContext context, Camera camera)
    {
        if (errorMaterial == null)
        {
            Shader errorShader = Shader.Find("Hidden/InternalErrorShader");
            errorMaterial = new Material(errorShader) { hideFlags = HideFlags.HideAndDontSave };
        }

        var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("ForwardBase"));
        drawSettings.SetShaderPassName(1, new ShaderPassName("PrepassBase"));
        drawSettings.SetShaderPassName(2, new ShaderPassName("Always"));
        drawSettings.SetShaderPassName(3, new ShaderPassName("Vertex"));
        drawSettings.SetShaderPassName(4, new ShaderPassName("VertexLMRGBM"));
        drawSettings.SetShaderPassName(5, new ShaderPassName("VertexLM"));
        drawSettings.SetOverrideMaterial(errorMaterial, 0);

        var filterSettings = new FilterRenderersSettings(true);
    

        context.DrawRenderers(cull.visibleRenderers, ref drawSettings, filterSettings);
    }
}
