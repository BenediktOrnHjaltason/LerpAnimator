using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;



[CustomEditor(typeof(LerpAnimator))]
public class LerpAnimatorEditor : Editor
{
    LerpAnimator animator;

    static List<Transform> editorTransformsArray;
    static List<TransformData> editorStartStates;
    static List<Segment> editorSegmentsArray;


    int serializedArrayCount;

    private void OnEnable()
    {
        animator = (LerpAnimator)target;

        EditorApplication.update += OnEditorUpdate;

        CollectEditorTransformsReferences();
        CollectEditorStartStates();
        CollectEditorSegmentsData();

        Debug.Log("EditorTransformsArray size = " + editorTransformsArray.Count);

        lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue;

        //ApplyFromDatastore(lastSelectedState);
        //SampleFromScene(lastSelectedState);


    }

    private void OnDisable()
    {
        //ApplyStartState();
        EditorApplication.update -= OnEditorUpdate;
    }

    private void CollectEditorTransformsReferences()
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

    private void CollectEditorStartStates()
    {
        editorStartStates = new List<TransformData>();

        int numberOfStartStates = serializedObject.FindProperty("StartStates").arraySize;

        for (int i = 0; i < numberOfStartStates; i++)
        {
            Vector3 position = serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value;
            Quaternion rotation = serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("rotation").quaternionValue;
            Vector3 scale = serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value;

            editorStartStates.Add(new TransformData(position, rotation, scale));

            //Debug.Log("Collected start position : " + position);
        }
    }

    private void CollectEditorSegmentsData()
    {
        editorSegmentsArray = new List<Segment>();

        int numberOfSegments = serializedObject.FindProperty("Segments").arraySize;

        for (int i = 0; i < numberOfSegments; i++)
        {
            Segment segment = new Segment();

            int toTransformDataCount = serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").arraySize;

            List<TransformData> toTransformData = new List<TransformData>();

            for (int j = 0; j < toTransformDataCount; j++)
            {
                Vector3 position = serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(j).FindPropertyRelative("position").vector3Value;
                Quaternion rotation = serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(j).FindPropertyRelative("rotation").quaternionValue;
                Vector3 scale = serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(j).FindPropertyRelative("scale").vector3Value;

                toTransformData.Add(new TransformData(position, rotation, scale));

                //Debug.Log("Collected segment to-position : " + position);
            }

            segment.toTransformData = toTransformData;
            segment.duration = serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i).FindPropertyRelative("duration").floatValue;
            segment.curve = serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i).FindPropertyRelative("curve").animationCurveValue;

            editorSegmentsArray.Add(segment);
        }
    }



    

    bool justModifiedSegmentsNumber = false;
    bool OnGUIChangedCalled = false;

    int lastSelectedState;

    #region GUI
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("TransformsToActOn"), true);

        GUILayout.Space(20);
        GUILayout.Label("START STATE");

        GUILayout.BeginHorizontal("Box");
        EditorGUILayout.LabelField(lastSelectedState == -1 ? "|>" : "", GUILayout.Width(20));

        if (GUILayout.Button("Select"))
        {
            lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = -1;
            serializedObject.ApplyModifiedProperties();
            ApplyFromDatastore(-1);
        }
        
        if (GUILayout.Button("Sample"))
        {
            SampleFromScene(-1);
        }

        if(serializedObject.FindProperty("Segments").arraySize > 0)
        {
            if (GUILayout.Button("Play"))
            {
                CollectEditorSegmentsData();

                lastSelectedState  = serializedObject.FindProperty("lastSelectedState").intValue = -1;
                ApplyFromDatastore(-1);
                StartPlayback(-1);
            }

            if (GUILayout.Button("STOP"))
            {
                ApplyFromDatastore(lastSelectedState);
                playbackRunning = false;
            }
        }

        GUILayout.EndHorizontal();

        //EditorGUILayout.PropertyField(serializedObject.FindProperty("StartStates"), true);


        GUILayout.Space(20);
        GUILayout.Label("SEGMENTS");

        int numberOfSegments = serializedObject.FindProperty("Segments").arraySize;

        GUILayout.BeginHorizontal("Box");

        if (GUILayout.Button("Add segment"))
        {
            AddSegment();
            justModifiedSegmentsNumber = true;
        }

        if (GUILayout.Button("Remove segment"))
        {
            if (numberOfSegments < 2) return;

            RemoveSegment();
            justModifiedSegmentsNumber = true;
        }

        GUILayout.EndHorizontal();

        EditorGUILayout.Space(20);

        //EditorGUILayout.PropertyField(serializedObject.FindProperty("Segments"));

        if (!justModifiedSegmentsNumber)
        {

            for (int i = 0; i < numberOfSegments; i++)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i).FindPropertyRelative("duration"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i).FindPropertyRelative("curve"));
                serializedObject.ApplyModifiedProperties();


                GUILayout.BeginHorizontal("Box");

                EditorGUILayout.LabelField(lastSelectedState == i ? "|>" : "", GUILayout.Width(20));

                if (GUILayout.Button("Select"))
                {
                    lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = i;
                    ApplyFromDatastore(i);
                }

                
                if (GUILayout.Button("Sample"))
                {
                    SampleFromScene(i);
                }
                

                if (i != numberOfSegments -1 && GUILayout.Button("Play"))
                {
                    CollectEditorSegmentsData();

                    lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = i;
                    ApplyFromDatastore(i);
                    StartPlayback(i);
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(15);
            }
        }
        justModifiedSegmentsNumber = false;


        EditorGUILayout.Space(20);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("OnSequenceEnd"));

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

                            CollectEditorTransformsReferences();
                            InsertDataForNewlyOverriddenTransform(i);

                            return;
                        }
                    }


                    if (serializedTransform != null)
                    {
                        Debug.Log("User set array element value");

                        CollectEditorTransformsReferences();

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

                break;
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

                break;
            }
        }

        CollectEditorTransformsReferences();
        CollectEditorStartStates();
        CollectEditorSegmentsData();
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


        CollectEditorTransformsReferences();
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

        CollectEditorTransformsReferences();
        CollectEditorStartStates();
        CollectEditorSegmentsData();

        Debug.Log("transforms count after user decreased array size: " + editorTransformsArray.Count);
    }

    #endregion



    private void AddSegment()
    {
        serializedObject.FindProperty("Segments").arraySize++;
        serializedObject.ApplyModifiedProperties();


        
        //Insert data for transforms allready in array

        int indexAdded_Segments = serializedObject.FindProperty("Segments").arraySize -1;

        serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("duration").floatValue = 1;
        serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("curve").animationCurveValue = AnimationCurve.Linear(0, 0, 1, 1);


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

                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("rotation").quaternionValue =
                    serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("rotation").quaternionValue;

                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("scale").vector3Value =
                    serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value;
            }

            else
            {
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("position").vector3Value =
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments - 1).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("position").vector3Value;

                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("rotation").quaternionValue =
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments - 1).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("rotation").quaternionValue;

                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("scale").vector3Value =
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments - 1).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("scale").vector3Value;
            }
            

            serializedObject.ApplyModifiedProperties();
        }

        CollectEditorSegmentsData();
    }

    private void RemoveSegment()
    {
        serializedObject.FindProperty("Segments").arraySize--;
        serializedObject.ApplyModifiedProperties();


        //If last segment to select was the one we are about to delete, set to previous segment
        if (lastSelectedState == editorSegmentsArray.Count - 1)
        {
            ApplyFromDatastore(editorSegmentsArray.Count - 2);
            lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = editorSegmentsArray.Count - 2;
        }

        editorSegmentsArray.RemoveAt(editorSegmentsArray.Count - 1);
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
                    serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("rotation").quaternionValue;

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
                    serializedObject.FindProperty("Segments").GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("rotation").quaternionValue;

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

                serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("rotation").quaternionValue
                    = editorTransformsArray[i].transform.localRotation;

                serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value
                    = editorTransformsArray[i].transform.localScale;
            }

            serializedObject.ApplyModifiedProperties();
            CollectEditorStartStates();
        }

        else
        {
            for (int i = 0; i < editorTransformsArray.Count; i++)
            {
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value
                    = editorTransformsArray[i].transform.localPosition;

                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("rotation").quaternionValue
                    = editorTransformsArray[i].transform.localRotation;

                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value
                    = editorTransformsArray[i].transform.localScale;
            }

            serializedObject.ApplyModifiedProperties();
            CollectEditorSegmentsData();
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
            serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(newStartStatesCount - 1).FindPropertyRelative("rotation").quaternionValue = editorTransformsArray[newStartStatesCount - 1] == null ? Quaternion.identity : editorTransformsArray[newStartStatesCount - 1].localRotation;
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
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(newToTransformDataCount - 1).FindPropertyRelative("rotation").quaternionValue = editorTransformsArray[newToTransformDataCount - 1] == null ? Quaternion.identity : editorTransformsArray[newToTransformDataCount - 1].localRotation;
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(newToTransformDataCount - 1).FindPropertyRelative("scale").vector3Value = editorTransformsArray[newToTransformDataCount - 1] == null ? Vector3.zero : editorTransformsArray[newToTransformDataCount - 1].localScale;
            }
        }

        serializedObject.ApplyModifiedProperties();

        CollectEditorStartStates();
        CollectEditorSegmentsData();
    }

    private void InsertDataForNewlyOverriddenTransform(int index)
    {
        if (editorTransformsArray[index] == null) Debug.Log("InsertDataForNewly... element is null");

        //Start states
        serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(index).FindPropertyRelative("position").vector3Value = editorTransformsArray[index] == null ? Vector3.zero :  editorTransformsArray[index].localPosition;
        serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(index).FindPropertyRelative("rotation").quaternionValue = editorTransformsArray[index] == null ? Quaternion.identity : editorTransformsArray[index].localRotation;
        serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(index).FindPropertyRelative("scale").vector3Value = editorTransformsArray[index] == null ? Vector3.zero : editorTransformsArray[index].localScale;

        //Segments
        int segmentsCount = serializedObject.FindProperty("Segments").arraySize;

        for (int i = 0; i < segmentsCount; i++)
        {
            serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(index).FindPropertyRelative("position").vector3Value = editorTransformsArray[index] == null ? Vector3.zero : editorTransformsArray[index].localPosition;
            serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(index).FindPropertyRelative("rotation").quaternionValue = editorTransformsArray[index] == null ? Quaternion.identity : editorTransformsArray[index].localRotation;
            serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(index).FindPropertyRelative("scale").vector3Value = editorTransformsArray[index] == null ? Vector3.zero : editorTransformsArray[index].localScale;
        }

        serializedObject.ApplyModifiedProperties();
    }

    #region Playback

    double startTime;
    double lerpStep;
    int fromIndex;
    int toIndex;
    int indexStartedFrom;

    bool playbackRunning;

    private void StartPlayback(int fromIndex)
    {
        if (serializedObject.FindProperty("Segments").arraySize == 0) return;

        ApplyFromDatastore(fromIndex);

        startTime = EditorApplication.timeSinceStartup;

        this.fromIndex = indexStartedFrom = fromIndex;

        toIndex = fromIndex == -1 ? 0 : fromIndex + 1;

        playbackRunning = true;
    }


    private void OnEditorUpdate()
    {
        if (playbackRunning)
        {
            lerpStep = (EditorApplication.timeSinceStartup - startTime) / editorSegmentsArray[toIndex].duration;

            if (fromIndex == -1)
            {
                for (int i = 0; i < editorTransformsArray.Count; i++)
                {
                    

                    editorTransformsArray[i].localPosition =
                        Vector3.Lerp(editorStartStates[i].position,
                                              editorSegmentsArray[toIndex].toTransformData[i].position,
                                              editorSegmentsArray[toIndex].curve.Evaluate((float)lerpStep));

                    editorTransformsArray[i].localRotation =
                        Quaternion.Lerp(editorStartStates[i].rotation,
                                              editorSegmentsArray[toIndex].toTransformData[i].rotation,
                                              editorSegmentsArray[toIndex].curve.Evaluate((float)lerpStep));

                    editorTransformsArray[i].localScale =
                        Vector3.Lerp(editorStartStates[i].scale,
                                              editorSegmentsArray[toIndex].toTransformData[i].scale,
                                              editorSegmentsArray[toIndex].curve.Evaluate((float)lerpStep));
                }
            }

            else
            {
                for (int i = 0; i < editorTransformsArray.Count; i++)
                {
                    editorTransformsArray[i].localPosition =
                        Vector3.Lerp(editorSegmentsArray[fromIndex].toTransformData[i].position,
                                              editorSegmentsArray[toIndex].toTransformData[i].position,
                                              editorSegmentsArray[toIndex].curve.Evaluate((float)lerpStep));

                    editorTransformsArray[i].localRotation =
                        Quaternion.Lerp(editorSegmentsArray[fromIndex].toTransformData[i].rotation,
                                              editorSegmentsArray[toIndex].toTransformData[i].rotation,
                                              editorSegmentsArray[toIndex].curve.Evaluate((float)lerpStep));

                    editorTransformsArray[i].localScale =
                        Vector3.Lerp(editorSegmentsArray[fromIndex].toTransformData[i].scale,
                                              editorSegmentsArray[toIndex].toTransformData[i].scale,
                                              editorSegmentsArray[toIndex].curve.Evaluate((float)lerpStep));
                }
            }

            if (lerpStep > 1 )
            {
                //Was it the last segment?
                if (toIndex + 1 > editorSegmentsArray.Count -1)
                {
                    //Debug.Log("Detected toIndex + 1 is more than indexes in editorSegmentsArray. toIndex + 1 = " + (toIndex + 1 ).ToString());

                    playbackRunning = false;
                    lastSelectedState = toIndex;
                    //ApplyFromDatastore(lastSelectedState);
                }

                //Go to next segment
                else
                {
                    fromIndex = fromIndex == -1 ? 0 : ++fromIndex;
                    toIndex++;

                    startTime = EditorApplication.timeSinceStartup;

                    //Debug.Log("Going to next segment. fromIndex " + fromIndex + ". toIndex =" + toIndex);
                }
            }
            
        }
    }

    #endregion
}
