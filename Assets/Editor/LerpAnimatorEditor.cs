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

        CollectTransformsReferences();
    }

    private void OnDisable()
    {
        //ApplyStartState();
        EditorApplication.update -= OnEditorUpdate;
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
        GUILayout.Label("START STATE");

        GUILayout.BeginHorizontal("Box");
        if (GUILayout.Button("Select"))
        {
            ApplyFromDatastore(-1);
        }
        if (GUILayout.Button("Sample scene"))
        {
            SampleFromScene(-1);
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

        EditorGUILayout.PropertyField(serializedObject.FindProperty("StartStates"), true);


        GUILayout.Space(20);
        GUILayout.Label("SEGMENTS");

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
                GUILayout.BeginHorizontal("Box");
                if (GUILayout.Button("Select"))
                {
                    Debug.Log("You pressed Preview on segment " + i);
                    ApplyFromDatastore(i);
                }

                if (GUILayout.Button("Sample scene"))
                {
                    SampleFromScene(i);
                }

                if (GUILayout.Button("Play from here"))
                {
                    Debug.Log("You pressed Play from here on segment " + i);
                }
                GUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i));
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
        }

        else
        {
            //Check if user overrided array element

            for (int i = 0; i < serializedArrayCount; i++)
            {
                Transform serializedTransform = serializedObject.FindProperty("TransformsToActOn").GetArrayElementAtIndex(i).objectReferenceValue as Transform;

                //Insert data for new element
                if (serializedTransform != editorTransformsArray[i])
                {
                    //First check if no duplicate exists elsewhere
                    int serializedArraySize = serializedObject.FindProperty("TransformsToActOn").arraySize;
                    for (int j = 0; j < serializedArraySize; j++)
                    {

                        if (j != i && serializedTransform == serializedObject.FindProperty("TransformsToActOn").GetArrayElementAtIndex(j).objectReferenceValue as Transform) 
                        {
                            Debug.LogWarning("Lerp Animator: You inserted a duplicate transform into the array. Not Allowed. Nulling values");

                            //Null serialized element
                            serializedObject.FindProperty("TransformsToActOn").GetArrayElementAtIndex(i).objectReferenceValue = null;
                            serializedObject.ApplyModifiedProperties();

                            CollectTransformsReferences();
                            InsertDataForNewlyOverriddenTransform(i);

                            return;
                        }
                    }


                    if (serializedTransform != null)
                    {
                        Debug.Log("User set array element value");

                        CollectTransformsReferences();

                        InsertDataForNewlyOverriddenTransform(i);

                        return;
                    }

                    else Debug.Log("User nulled array element");
                }
            }


        }

        serializedArrayCount = serializedObject.FindProperty("TransformsToActOn").arraySize;
    }

    private void OnUserDeletedElementDirectly()
    {
        Debug.Log("OnUserDeletedElementDirectly called");

        //remove at index for editor transforms array and data
        for (int i = 0; i < editorTransformsArray.Count; i++)
        {
            if (serializedObject.FindProperty("TransformsToActOn").arraySize == 0)
            {
                editorTransformsArray.Clear();

                serializedObject.FindProperty("StartStates").ClearArray();

                int numberOfSegments = serializedObject.FindProperty("Segments").arraySize;
                for (int j = 0; j < numberOfSegments; j++)
                {
                    serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").ClearArray();
                }

                serializedObject.ApplyModifiedProperties();
                return;
            }

            if (editorTransformsArray[i] != serializedObject.FindProperty("TransformsToActOn").GetArrayElementAtIndex(i).objectReferenceValue)
            {
                Debug.Log("OnUserDeletedElementDirectly: Found index " + i);

                editorTransformsArray.RemoveAt(i);

                serializedObject.FindProperty("StartStates").DeleteArrayElementAtIndex(i);

                int numberOfSegments = serializedObject.FindProperty("Segments").arraySize;
                for (int j = 0; j < numberOfSegments; j++)
                {
                    serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").DeleteArrayElementAtIndex(i);
                }

                serializedObject.ApplyModifiedProperties();

                return;
            }
        }

        CollectTransformsReferences();
    }

    private void OnUserIncreasedTransformsArrayCount()
    {
        int newElementsCount = serializedObject.FindProperty("TransformsToActOn").arraySize;
        int difference = newElementsCount - editorTransformsArray.Count;

        //Null repeating transforms elements due to increasing size
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

        int serializedTransformsArrayCount = serializedObject.FindProperty("TransformsToActOn").arraySize;
        int editorTransformsArrayCount = editorTransformsArray.Count;
        int difference = editorTransformsArrayCount - serializedTransformsArrayCount;

        //Remove from end from all collections
        for (int i = 0; i < difference; i++ )
        {
            editorTransformsArray.RemoveAt(editorTransformsArray.Count - 1);

            serializedObject.FindProperty("StartStates").arraySize--;


            int numberOfSegments = serializedObject.FindProperty("Segments").arraySize;

            for (int j = 0; j < numberOfSegments; j++)
            {
                Debug.Log("Removing from end of segments toTransformData lists");

                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").arraySize--;
            }
        }

        serializedObject.ApplyModifiedProperties();

        CollectTransformsReferences();

        Debug.Log("transforms count after user decreased array size: " + editorTransformsArray.Count);
    }

    #endregion



    private void AddSegment()
    {
        serializedObject.FindProperty("Segments").arraySize++;
        serializedObject.ApplyModifiedProperties();


        
        //Insert data for transforms allready in array

        int indexAdded_Segments = serializedObject.FindProperty("Segments").arraySize -1;

        int numberOfTransforms = serializedObject.FindProperty("TransformsToActOn").arraySize;

        
        Debug.Log("AddSegment: numberOfTransforms = " + numberOfTransforms);
        
        //Clear pre filled array elements
        serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").ClearArray();

        for (int i = 0; i < numberOfTransforms; i++)
        {
            
            serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").arraySize++;
            
            Debug.Log("AddSegment: Adding toTransformsData to segment");

            serializedObject.ApplyModifiedProperties();

            int indexAdded_Data = serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").arraySize - 1;

            if (indexAdded_Segments == 0)
            {
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("position").vector3Value =
                serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value;

                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("rotation").vector3Value =
                    serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("rotation").vector3Value;

                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("scale").vector3Value =
                    serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value;
            }

            else
            {
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("position").vector3Value =
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments - 1).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("position").vector3Value;

                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("rotation").vector3Value =
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments - 1).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("rotation").vector3Value;

                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("scale").vector3Value =
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments - 1).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("scale").vector3Value;
            }
            

            serializedObject.ApplyModifiedProperties();
        }
    }

    private void RemoveSegment()
    {
        serializedObject.FindProperty("Segments").arraySize--;
        serializedObject.ApplyModifiedProperties();
    }

    double startTime;
    double step;
    private void StartSequence()
    {
        if (animator.Segments.Count == 0) return;

        startTime = EditorApplication.timeSinceStartup;
    }

    #region Data management
    public void ApplyFromDatastore(int segmentIndex)
    {
        if (segmentIndex == -1)
        {
            for (int i = 0; i < editorTransformsArray.Count; i++)
            {
                editorTransformsArray[i].localPosition =
                    serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value;

                editorTransformsArray[i].localRotation =
                    Quaternion.Euler(serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("rotation").vector3Value);

                editorTransformsArray[i].localScale =
                    serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value;
            }
        }

        else
        {
            for (int i = 0; i < editorTransformsArray.Count; i++)
            {
                editorTransformsArray[i].localPosition =
                    serializedObject.FindProperty("Segments").GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value;

                editorTransformsArray[i].localRotation =
                    Quaternion.Euler(serializedObject.FindProperty("Segments").GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("rotation").vector3Value);

                editorTransformsArray[i].localScale =
                    serializedObject.FindProperty("Segments").GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value;
            }
        }
        
    }
    #endregion

    public void SampleFromScene(int segmentIndex)
    {
        if (segmentIndex == -1)
        {
            for (int i = 0; i < editorTransformsArray.Count; i++)
            {
                serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value
                    = editorTransformsArray[i].transform.localPosition;

                serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("rotation").vector3Value
                    = editorTransformsArray[i].transform.localRotation.eulerAngles;

                serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value
                    = editorTransformsArray[i].transform.localScale;
            }
        }

        else
        {
            for (int i = 0; i < editorTransformsArray.Count; i++)
            {
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value
                    = editorTransformsArray[i].transform.localPosition;

                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("rotation").vector3Value
                    = editorTransformsArray[i].transform.localRotation.eulerAngles;

                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value
                    = editorTransformsArray[i].transform.localScale;
            }
        }
        
    }

    private void AddDataElementsForNewlyAddedTransformsElements(int difference)
    {

        for (int i = 0; i < difference; i++)
        {
            serializedObject.FindProperty("StartStates").arraySize++;

            int newStartStatesCount = serializedObject.FindProperty("StartStates").arraySize;

            //--- Add states, initialized to object added
            //StartStates

            serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(newStartStatesCount - 1).FindPropertyRelative("position").vector3Value = editorTransformsArray[newStartStatesCount - 1] == null ? Vector3.zero : editorTransformsArray[newStartStatesCount - 1].localPosition;
            serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(newStartStatesCount - 1).FindPropertyRelative("rotation").vector3Value = editorTransformsArray[newStartStatesCount - 1] == null ? Vector3.zero : editorTransformsArray[newStartStatesCount - 1].localRotation.eulerAngles;
            serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(newStartStatesCount - 1).FindPropertyRelative("scale").vector3Value = editorTransformsArray[newStartStatesCount - 1] == null ? Vector3.zero : editorTransformsArray[newStartStatesCount - 1].localScale;

            //Segments
            //toTransformData amounts should be the same as start states amount

            //TODO: This will only add data to existing segments. What happens if new segments are added after transforms are added?

            int segmentsCount = serializedObject.FindProperty("Segments").arraySize;

            for (int j = 0; j < segmentsCount; j++)
            {
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").arraySize++;

                int newToTransformDataCount = serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").arraySize;

                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(newToTransformDataCount - 1).FindPropertyRelative("position").vector3Value = editorTransformsArray[newToTransformDataCount - 1] == null ?  Vector3.zero : editorTransformsArray[newToTransformDataCount - 1].localPosition;
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(newToTransformDataCount - 1).FindPropertyRelative("rotation").vector3Value = editorTransformsArray[newToTransformDataCount - 1] == null ? Vector3.zero : editorTransformsArray[newToTransformDataCount - 1].localRotation.eulerAngles;
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(newToTransformDataCount - 1).FindPropertyRelative("scale").vector3Value = editorTransformsArray[newToTransformDataCount - 1] == null ? Vector3.zero : editorTransformsArray[newToTransformDataCount - 1].localScale;
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void InsertDataForNewlyOverriddenTransform(int index)
    {
        if (editorTransformsArray[index] == null) Debug.Log("InsertDataForNewly... element is null");

        //Start states
        serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(index).FindPropertyRelative("position").vector3Value = editorTransformsArray[index] == null ? Vector3.zero :  editorTransformsArray[index].localPosition;
        serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(index).FindPropertyRelative("rotation").vector3Value = editorTransformsArray[index] == null ? Vector3.zero : editorTransformsArray[index].localRotation.eulerAngles;
        serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(index).FindPropertyRelative("scale").vector3Value = editorTransformsArray[index] == null ? Vector3.zero : editorTransformsArray[index].localScale;

        //Segments
        int segmentsCount = serializedObject.FindProperty("Segments").arraySize;

        for (int i = 0; i < segmentsCount; i++)
        {
            serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(index).FindPropertyRelative("position").vector3Value = editorTransformsArray[index] == null ? Vector3.zero : editorTransformsArray[index].localPosition;
            serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(index).FindPropertyRelative("rotation").vector3Value = editorTransformsArray[index] == null ? Vector3.zero : editorTransformsArray[index].localRotation.eulerAngles;
            serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(index).FindPropertyRelative("scale").vector3Value = editorTransformsArray[index] == null ? Vector3.zero : editorTransformsArray[index].localScale;
        }

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
