using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
[CreateAssetMenu(menuName = "Rendering/Impli Pipeline")]
public class ImpliPipelineAsset : RenderPipelineAsset
{
    public enum ShadowMapSize
    {
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _2048 = 2046,
        _4096 = 4096
    }

    [SerializeField]
    ShadowMapSize shadowMapSize = ShadowMapSize._1024;

    [SerializeField]
    bool dynamicBatching = false;

    [SerializeField]
    bool instancing = false;

    protected override IRenderPipeline InternalCreatePipeline()
    {
        return new ImpliPipeline(dynamicBatching, instancing,(int)shadowMapSize);
    }

  
}
