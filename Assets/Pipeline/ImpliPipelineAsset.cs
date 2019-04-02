using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
[CreateAssetMenu(menuName = "Rendering/Impli Pipeline")]
public class ImpliPipelineAsset : RenderPipelineAsset
{
    [SerializeField]
    bool dynamicBatching = false;

    [SerializeField]
    bool instancing = false;

    protected override IRenderPipeline InternalCreatePipeline()
    {
        return new ImpliPipeline(dynamicBatching, instancing);
    }

  
}
