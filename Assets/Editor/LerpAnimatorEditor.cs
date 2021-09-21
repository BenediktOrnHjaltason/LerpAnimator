using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(LerpAnimator))]
public class LerpAnimatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("TransformsToActOn"), true);

        serializedObject.ApplyModifiedProperties();
    }
}
