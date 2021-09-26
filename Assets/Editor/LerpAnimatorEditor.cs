using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;



[CustomEditor(typeof(LerpAnimator))]
public class LerpAnimatorEditor : Editor
{
    LerpAnimator animator;

    static List<Transform> editorTransformsArray;

    int serializedArrayCount;


    private void OnEnable()
    {
        animator = (LerpAnimator)target;

        EditorApplication.update += OnEditorUpdate;
        EditorApplication.hierarchyChanged += OnHierarchyChanged;

        CollectTransformsReferences();
    }

    private void OnDisable()
    {
        //ApplyStartState();
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnHierarchyChanged()
    {
        Debug.Log("OnHierarchyChanged");
    }

    private void CollectTransformsReferences()
    {
        serializedArrayCount = serializedObject.FindProperty("TransformsToActOn").arraySize;

        editorTransformsArray = new List<Transform>();

        for (int i = 0; i < serializedArrayCount; i++)
        {
            Transform transform = serializedObject.FindProperty("TransformsToActOn").GetArrayElementAtIndex(i).objectReferenceValue as Transform;

            editorTransformsArray.Add(transform);
        }

        serializedArrayCount = editorTransformsArray.Count;
    }



    

    bool justModifiedSegmentsNumber = false;
    bool OnGUIChangedCalled = false;

    #region GUI
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("TransformsToActOn"), true);

        GUILayout.Space(20);
        GUILayout.Label("Start state");

        GUILayout.BeginHorizontal("Box");
        if (GUILayout.Button("Sample scene"))
        {
            SampleFromScene(-1);
        }

        if (GUILayout.Button("Select"))
        {
           ApplyFromDatastore(-1);
        }

        if (animator == null) Debug.Log("LAE: animator is null");

        if(serializedObject.FindProperty("Segments").arraySize > 0)
        {
            if (GUILayout.Button("Play from start"))
            {
                //ApplyStartState();
            }
        }

        GUILayout.EndHorizontal();


        GUILayout.Space(20);
        GUILayout.Label("Segments");

        int numberOfSegments = serializedObject.FindProperty("Segments").arraySize;

        GUILayout.BeginHorizontal("Box");

        if (GUILayout.Button("Remove segment"))
        {
            if (numberOfSegments < 2) return;

            Debug.Log("Pressed Remove segment");
            RemoveSegment();
            justModifiedSegmentsNumber = true;
        }

        if (GUILayout.Button("Add segment"))
        {
            Debug.Log("Pressed Add segment");
            AddSegment();
            justModifiedSegmentsNumber = true;
        }

        GUILayout.EndHorizontal();

        //EditorGUILayout.PropertyField(serializedObject.FindProperty("Segments"));

        
        if (!justModifiedSegmentsNumber)
        {

            for (int i = 0; i < numberOfSegments; i++)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i));

                GUILayout.BeginHorizontal("Box");
                if (GUILayout.Button("Select"))
                {
                    Debug.Log("You pressed Preview on segment " + i);
                    ApplyFromDatastore(i);
                }

                if (GUILayout.Button("Sample scene"))
                {
                    Debug.Log("You pressed sample on segment" + i);
                }

                if (GUILayout.Button("Play from here"))
                {
                    Debug.Log("You pressed Play from here on segment " + i);
                }
                GUILayout.EndHorizontal();
            }

            
        }
        justModifiedSegmentsNumber = false;

        EditorGUI.BeginChangeCheck();
        if (EditorGUI.EndChangeCheck()) Debug.Log("A change was made");

        serializedObject.ApplyModifiedProperties();


        if (GUI.changed)
        {
            OnGUIChanged();
            OnGUIChangedCalled = true;
        }
        else OnGUIChangedCalled = false;

        int currentTransformsArraySize = serializedObject.FindProperty("TransformsToActOn").arraySize;

        //If user deletes array element completely
        if (!OnGUIChangedCalled && currentTransformsArraySize != serializedArrayCount)
        {
            Debug.Log("Detected user deleted array element");

            OnUserDeletedElementDirectly();

            serializedArrayCount = serializedObject.FindProperty("TransformsToActOn").arraySize;
        }
    }
    #endregion

    #region Data consistency
    private void OnGUIChanged()
    {
        Debug.Log("OnGUIChanged");

        
        int currentTransformsArrayCount = serializedObject.FindProperty("TransformsToActOn").arraySize;

        //If user adjusts array count
        if (editorTransformsArray.Count != currentTransformsArrayCount)
        {
            if (currentTransformsArrayCount > editorTransformsArray.Count)
            {
                Debug.Log("User increased array count");

                OnUserIncreasedTransformsArrayCount();
            }
            else
            {
                Debug.Log("User decreased array count");

                OnUserDecreasedTransformsArrayCount();
            }

            //TODO: Delete corresponding data array elements also

            CollectTransformsReferences();
        }

        else
        {
            //Check if user overrided array element

            for (int i = 0; i < serializedArrayCount; i++)
            {
                Transform serializedTransform = serializedObject.FindProperty("TransformsToActOn").GetArrayElementAtIndex(i).objectReferenceValue as Transform;

                if (serializedTransform != editorTransformsArray[i])
                {
                    if (serializedTransform != null)
                    {
                        Debug.Log("User set array element value");

                        //TODO: Add 
                    }

                    else Debug.Log("User nulled array element");
                }
            }


        }

        serializedArrayCount = serializedObject.FindProperty("TransformsToActOn").arraySize;
    }

    private void OnUserDeletedElementDirectly()
    {

    }


    //
    private void OnUserIncreasedTransformsArrayCount()
    {
        int newElementsCount = serializedObject.FindProperty("TransformsToActOn").arraySize;
        int difference = newElementsCount - editorTransformsArray.Count;

        for (int i = newElementsCount - 1; i > 0; i--)
        {
            Transform higherIndexTransform = (Transform)serializedObject.FindProperty("TransformsToActOn").GetArrayElementAtIndex(i).objectReferenceValue;
            Transform lowerIndexTransform = (Transform)serializedObject.FindProperty("TransformsToActOn").GetArrayElementAtIndex(i - 1).objectReferenceValue;

            if (higherIndexTransform != null && lowerIndexTransform != null && higherIndexTransform == lowerIndexTransform)
            {
                Debug.Log("Detected duplicate");

                serializedObject.FindProperty("TransformsToActOn").GetArrayElementAtIndex(i).objectReferenceValue = null;

                serializedObject.ApplyModifiedProperties();
            }
        }


        CollectTransformsReferences();
        AddDataElementsForNewlyAddedTransformsElements(difference);
    }

    private void OnUserDecreasedTransformsArrayCount()
    {
        CollectTransformsReferences();

        //TODO: Delete data elements for the deleted elements

        Debug.Log("transforms count after user decreased array size: " + editorTransformsArray.Count);
    }

    #endregion



    private void AddSegment()
    {
        serializedObject.FindProperty("Segments").arraySize++;
        serializedObject.ApplyModifiedProperties();
    }

    private void RemoveSegment()
    {
        serializedObject.FindProperty("Segments").arraySize--;
        serializedObject.ApplyModifiedProperties();
    }

    private void ApplyStartState(int index = -1)
    {
        ApplyFromDatastore(index);
    }

    double startTime;
    double step;
    private void StartSequence()
    {
        if (animator.Segments.Count == 0) return;

        startTime = EditorApplication.timeSinceStartup;
    }

    #region Data management
    public void ApplyFromDatastore(int index)
    {
        
    }
    #endregion

    public void SampleFromScene(int index)
    {

    }

    private void AddDataElementsForNewlyAddedTransformsElements(int difference)
    {

        for (int i = 0; i < difference; i++)
        {
            serializedObject.FindProperty("StartStates").arraySize++;

            int newStartStatesCount = serializedObject.FindProperty("StartStates").arraySize;

            //--- Add states, initialized to object added
            //StartStates
            serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(newStartStatesCount - 1).FindPropertyRelative("position").vector3Value =  Vector3.zero;
            serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(newStartStatesCount - 1).FindPropertyRelative("rotation").vector3Value =  Vector3.zero;
            serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(newStartStatesCount - 1).FindPropertyRelative("scale").vector3Value =  Vector3.zero;

            //Segments
            //toTransformData amounts should be the same as start states amount

            //TODO: This will only add data to existing segments. What happens if new segments are added after transforms are added?

            int segmentsCount = serializedObject.FindProperty("Segments").arraySize;

            for (int j = 0; j < segmentsCount; j++)
            {
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").arraySize++;

                int newToTransformDataCount = serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").arraySize;

                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(newToTransformDataCount - 1).FindPropertyRelative("position").vector3Value =  Vector3.zero;
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(newToTransformDataCount - 1).FindPropertyRelative("rotation").vector3Value = Vector3.zero;
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(newToTransformDataCount - 1).FindPropertyRelative("scale").vector3Value = Vector3.zero;
            }
        }

        Debug.Log("First segment contains toTranformData for " + serializedObject.FindProperty("Segments").GetArrayElementAtIndex(0).FindPropertyRelative("toTransformData").arraySize + " transforms");

        serializedObject.ApplyModifiedProperties();
    }

    private bool CalculatingInterpolationStep(double startTime, double duration, out double step)
    {
        step = (EditorApplication.timeSinceStartup - startTime) / duration;

        return step < 1;
    }

    private void OnEditorUpdate()
    {
        //Debug.Log("OnEditorUpdate");
    }
}
