using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InstancedMaterialProperties : MonoBehaviour
{
    static MaterialPropertyBlock propertyBlock;
    static int ColorID = Shader.PropertyToID("_Color");
    static int SmoothnessID = Shader.PropertyToID("_Smoothness");

    [SerializeField]
    Color color = Color.white;

    [SerializeField,Range(0f,1f)]
    float smoothness = 0.5f;

    [SerializeField]
    bool randomize=true;

    private void Awake()
    {
        OnValidate();
    }
    private void OnValidate()
    {
        if (color == Color.white&&randomize)
            color = RandomColor();
        if(propertyBlock==null)
            propertyBlock = new MaterialPropertyBlock();
        propertyBlock.SetColor(ColorID, color);
        propertyBlock.SetFloat(SmoothnessID, smoothness);
        GetComponent<MeshRenderer>().SetPropertyBlock(propertyBlock);
    }
    private Color RandomColor()
    {
        
        return new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f), 1f);
    }
}
