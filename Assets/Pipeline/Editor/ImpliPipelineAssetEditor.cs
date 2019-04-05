using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.Rendering;

[CustomEditor(typeof(ImpliPipelineAsset))]
public class ImpliPipelineAssetEditor : Editor
{
    private SerializedProperty shadowCascades;
    private SerializedProperty twoCascadesSplit;
    private SerializedProperty fourCascadesSplit;

    private void OnEnable()
    {
        shadowCascades = serializedObject.FindProperty("shadowCascades");
        twoCascadesSplit = serializedObject.FindProperty("twoCascadesSplit");
        fourCascadesSplit = serializedObject.FindProperty("fourCascadesSplit");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        switch (shadowCascades.enumValueIndex)
        {
            case 0: return;
            case 1:
                CoreEditorUtils.DrawCascadeSplitGUI<float>(ref twoCascadesSplit);
                break;
            case 2:
                CoreEditorUtils.DrawCascadeSplitGUI<Vector3>(ref fourCascadesSplit);
                break;
        }
        serializedObject.ApplyModifiedProperties();
    }
}
