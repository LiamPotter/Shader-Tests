using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InstancedColor : MonoBehaviour
{
    static MaterialPropertyBlock propertyBlock;
    static int ColorID = Shader.PropertyToID("_Color");
    [SerializeField]
    Color color = Color.white;
    private void Awake()
    {
        OnValidate();
    }
    private void OnValidate()
    {
        if (color == Color.white)
            color = RandomColor();
        if(propertyBlock==null)
            propertyBlock = new MaterialPropertyBlock();
        propertyBlock.SetColor(ColorID, color);
        GetComponent<MeshRenderer>().SetPropertyBlock(propertyBlock);
    }
    private Color RandomColor()
    {
        return new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f), 1f);
    }
}
