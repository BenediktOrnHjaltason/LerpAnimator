using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


[CustomEditor(typeof(LerpAnimator))]
public class LerpAnimatorEditor : Editor
{
    /// <summary>
    /// transforms references used to playback animation during edit mode. Saving a copy to avoid having to access serialized object in Update() while animating
    /// </summary>
    List<Transform> editorTransforms;
    /// <summary>
    /// Start states data used to playback animation during edit mode
    /// </summary>
    List<TransformData> editorStartStates;
    /// <summary>
    /// Segments data used to playback animation during edit mode
    /// </summary>
    List<Segment> editorSegments;


    int serializedArrayCount;

    #region Events

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;

        CollectEditorTransforms();
        CollectEditorStartStates();
        CollectEditorSegments();

        lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue;
    }

    private void OnDisable()
    {
        //ApplyStartState();
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
    }

    
    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
            ApplyFromDatastore(-1);
    }

    private void OnGUIChanged()
    {
        //Debug.Log("OnGUIChanged called");

        CheckForTransformsArrayChanged();
    }


    /// <summary>
    /// Handles when Undo/Redo is performed by user. Because Undo.undoRedoPerformed is fired before changes are registered in serializedObject,
    /// a delay is used to wait for it to update before collecting data again.
    /// </summary>
    bool collectEditorDataDelayed = false;
    float delayedCollectTimerStart;
    const float delayAmount = 0.05f;
    private void OnUndoRedoPerformed()
    {
        CollectEditorTransforms();
        CollectEditorStartStates();
        CollectEditorSegments();

        //Debug.Log("OnUndoRedoPerformed called. Transforms count in serialized object: " + serializedObject.FindProperty("TransformsToActOn").arraySize);
        delayedCollectTimerStart = (float)EditorApplication.timeSinceStartup;
        collectEditorDataDelayed = true;
    }

    #endregion

    #region Editor data copies

    private void CollectEditorTransforms()
    {
        serializedArrayCount = serializedObject.FindProperty("TransformsToActOn").arraySize;

        editorTransforms = new List<Transform>();

        for (int i = 0; i < serializedArrayCount; i++)
        {
            Transform transform = serializedObject.FindProperty("TransformsToActOn").GetArrayElementAtIndex(i).objectReferenceValue as Transform;

            editorTransforms.Add(transform);
        }

        serializedArrayCount = editorTransforms.Count;
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
        }
    }

    private void CollectEditorSegments()
    {
        editorSegments = new List<Segment>();

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
            }

            segment.toTransformData = toTransformData;
            segment.duration = serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i).FindPropertyRelative("duration").floatValue;
            segment.curve = serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i).FindPropertyRelative("curve").animationCurveValue;

            editorSegments.Add(segment);
        }
    }
    #endregion

    #region GUI

    bool justModifiedSegmentsNumber = false;
    bool OnGUIChangedCalled = false;

    int lastSelectedState;

    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("TransformsToActOn"), true);

        GUILayout.Space(20);
        GUILayout.Label("START STATE");

        GUILayout.BeginHorizontal("Box");
        EditorGUILayout.LabelField(lastSelectedState == -1 ? "|>" : "", GUILayout.Width(20));

        if (GUILayout.Button("Preview"))
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
                CollectEditorSegments();

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

                if (GUILayout.Button("Preview"))
                {
                    lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = i;
                    ApplyFromDatastore(i);
                }

                
                if (GUILayout.Button("Sample"))
                {
                    lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = i;
                    SampleFromScene(i);
                }
                

                if (i != numberOfSegments -1 && GUILayout.Button("Play"))
                {
                    CollectEditorSegments();

                    lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = i;
                    ApplyFromDatastore(i);
                    StartPlayback(i);
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(15);
            }
        }
        justModifiedSegmentsNumber = false;

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
        EditorGUILayout.PropertyField(serializedObject.FindProperty("OnSequenceEnd"));


        serializedObject.ApplyModifiedProperties();


        if (GUI.changed)
        {
            OnGUIChanged();
            OnGUIChangedCalled = true;
        }
        else OnGUIChangedCalled = false;

        int currentTransformsArraySize = serializedObject.FindProperty("TransformsToActOn").arraySize;

        //If user deletes array element completely
        if (!OnGUIChangedCalled && currentTransformsArraySize < serializedArrayCount)
        {
            Debug.Log("Detected user deleted array element");

            OnUserDeletedElementDirectly();
            

            serializedArrayCount = serializedObject.FindProperty("TransformsToActOn").arraySize;
        }
    }
    #endregion

    #region Data consistency
    

    private void CheckForTransformsArrayChanged()
    {
        int currentTransformsArrayCount = serializedObject.FindProperty("TransformsToActOn").arraySize;

        //If user adjusts array count
        if (editorTransforms.Count != currentTransformsArrayCount)
        {
            if (currentTransformsArrayCount > editorTransforms.Count)
            {
                Debug.Log("User increased array count");

                OnUserIncreasedTransformsArraySize();
            }
            else
            {
                Debug.Log("User decreased array count");

                OnUserDecreasedTransformsArraySize();
            }
        }

        else
        {
            //Check if user overrided array element

            for (int i = 0; i < serializedArrayCount; i++)
            {
                Transform serializedTransform = serializedObject.FindProperty("TransformsToActOn").GetArrayElementAtIndex(i).objectReferenceValue as Transform;

                //Insert data for new element
                if (serializedTransform != editorTransforms[i])
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

                            CollectEditorTransforms();
                            InsertDataForNewlyOverriddenTransform(i);

                            return;
                        }
                    }


                    if (serializedTransform != null)
                    {
                        Debug.Log("User set array element value");



                        InsertDataForNewlyOverriddenTransform(i);
                        CollectEditorTransforms();
                        return;
                    }

                    else
                    {
                        Debug.Log("User nulled element");
                        CollectEditorTransforms();
                    };

                    
                }
            }
        }

        serializedArrayCount = serializedObject.FindProperty("TransformsToActOn").arraySize;
    }

    private void OnUserDeletedElementDirectly()
    {
        Debug.Log("OnUserDeletedElementDirectly called");

        //remove at index for editor transforms array and data
        for (int i = 0; i < editorTransforms.Count; i++)
        {
            if (serializedObject.FindProperty("TransformsToActOn").arraySize == 0)
            {
                editorTransforms.Clear();

                serializedObject.FindProperty("StartStates").ClearArray();

                int numberOfSegments = serializedObject.FindProperty("Segments").arraySize;
                for (int j = 0; j < numberOfSegments; j++)
                {
                    serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").ClearArray();
                }

                serializedObject.ApplyModifiedProperties();

                break;
            }

            if (editorTransforms[i] != serializedObject.FindProperty("TransformsToActOn").GetArrayElementAtIndex(i).objectReferenceValue)
            {
                Debug.Log("OnUserDeletedElementDirectly: Found index " + i);

                editorTransforms.RemoveAt(i);

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

        CollectEditorTransforms();
        CollectEditorStartStates();
        CollectEditorSegments();
    }

    private void OnUserIncreasedTransformsArraySize()
    {
        int newElementsCount = serializedObject.FindProperty("TransformsToActOn").arraySize;
        int difference = newElementsCount - editorTransforms.Count;

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


        CollectEditorTransforms();
        //CollectEditorStartStates();
        AddDataElementsForNewlyAddedTransformsElement(difference);
    }
    
    private void OnUserDecreasedTransformsArraySize()
    {

        int serializedTransformsArrayCount = serializedObject.FindProperty("TransformsToActOn").arraySize;
        int editorTransformsCount = editorTransforms.Count;
        int difference = editorTransformsCount - serializedTransformsArrayCount;

        //Remove from end from all collections
        for (int i = 0; i < difference; i++ )
        {
            editorTransforms.RemoveAt(editorTransforms.Count - 1);

            serializedObject.FindProperty("StartStates").arraySize--;


            int numberOfSegments = serializedObject.FindProperty("Segments").arraySize;

            for (int j = 0; j < numberOfSegments; j++)
            {
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").arraySize--;
            }
        }

        serializedObject.ApplyModifiedProperties();

        CollectEditorTransforms();
        CollectEditorStartStates();
        CollectEditorSegments();
    }

    private void AddDataElementsForNewlyAddedTransformsElement(int difference)
    {

        //Debug.Log("AddDataElementsForNewlyAddedTransformsElement called. Difference = " + difference + ". Last index in start states array = " + serializedObject.FindProperty("StartStates").arraySize);

        for (int i = 0; i < difference; i++)
        {
            serializedObject.FindProperty("StartStates").arraySize++;

            int newStartStatesCount = serializedObject.FindProperty("StartStates").arraySize;

            //--- Add states, initialized to object added
            //StartStates

            serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(newStartStatesCount - 1).FindPropertyRelative("position").vector3Value = editorTransforms[newStartStatesCount - 1] == null ? Vector3.zero : editorTransforms[newStartStatesCount - 1].localPosition;
            serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(newStartStatesCount - 1).FindPropertyRelative("rotation").quaternionValue = editorTransforms[newStartStatesCount - 1] == null ? Quaternion.identity : editorTransforms[newStartStatesCount - 1].localRotation;
            serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(newStartStatesCount - 1).FindPropertyRelative("scale").vector3Value = editorTransforms[newStartStatesCount - 1] == null ? Vector3.zero : editorTransforms[newStartStatesCount - 1].localScale;

            //Segments
            //toTransformData amounts should be the same as start states amount

            //TODO: This will only add data to existing segments. What happens if new segments are added after transforms are added?

            int segmentsCount = serializedObject.FindProperty("Segments").arraySize;

            for (int j = 0; j < segmentsCount; j++)
            {
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").arraySize++;

                int newToTransformDataCount = serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").arraySize;

                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(newToTransformDataCount - 1).FindPropertyRelative("position").vector3Value = editorTransforms[newToTransformDataCount - 1] == null ? Vector3.zero : editorTransforms[newToTransformDataCount - 1].localPosition;
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(newToTransformDataCount - 1).FindPropertyRelative("rotation").quaternionValue = editorTransforms[newToTransformDataCount - 1] == null ? Quaternion.identity : editorTransforms[newToTransformDataCount - 1].localRotation;
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(newToTransformDataCount - 1).FindPropertyRelative("scale").vector3Value = editorTransforms[newToTransformDataCount - 1] == null ? Vector3.zero : editorTransforms[newToTransformDataCount - 1].localScale;
            }
        }

        serializedObject.ApplyModifiedProperties();

        CollectEditorStartStates();
        CollectEditorSegments();
    }

    private void InsertDataForNewlyOverriddenTransform(int index)
    {
        if (editorTransforms[index] == null) Debug.Log("InsertDataForNewly... element is null");

        //Start states
        serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(index).FindPropertyRelative("position").vector3Value = editorTransforms[index] == null ? Vector3.zero : editorTransforms[index].localPosition;
        serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(index).FindPropertyRelative("rotation").quaternionValue = editorTransforms[index] == null ? Quaternion.identity : editorTransforms[index].localRotation;
        serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(index).FindPropertyRelative("scale").vector3Value = editorTransforms[index] == null ? Vector3.zero : editorTransforms[index].localScale;

        //Segments
        int segmentsCount = serializedObject.FindProperty("Segments").arraySize;

        for (int i = 0; i < segmentsCount; i++)
        {
            serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(index).FindPropertyRelative("position").vector3Value = editorTransforms[index] == null ? Vector3.zero : editorTransforms[index].localPosition;
            serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(index).FindPropertyRelative("rotation").quaternionValue = editorTransforms[index] == null ? Quaternion.identity : editorTransforms[index].localRotation;
            serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(index).FindPropertyRelative("scale").vector3Value = editorTransforms[index] == null ? Vector3.zero : editorTransforms[index].localScale;
        }

        serializedObject.ApplyModifiedProperties();
    }

    #endregion

    #region User Operation

    private void AddSegment()
    {
        serializedObject.FindProperty("Segments").arraySize++;
        serializedObject.ApplyModifiedProperties();


        
        //Insert data for transforms allready in array

        int indexAdded_Segments = serializedObject.FindProperty("Segments").arraySize -1;

        serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("duration").floatValue = 1;
        serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("curve").animationCurveValue = AnimationCurve.Linear(0, 0, 1, 1);


        int numberOfTransforms = serializedObject.FindProperty("TransformsToActOn").arraySize;
        
        //Clear pre filled array elements
        serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").ClearArray();

        for (int i = 0; i < numberOfTransforms; i++)
        {
            
            serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").arraySize++;

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

        CollectEditorSegments();
    }

    private void RemoveSegment()
    {
        serializedObject.FindProperty("Segments").arraySize--;
        serializedObject.ApplyModifiedProperties();


        //If last segment to select was the one we are about to delete, set to previous segment
        if (lastSelectedState == editorSegments.Count - 1)
        {
            ApplyFromDatastore(editorSegments.Count - 2);
            lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = editorSegments.Count - 2;
        }

        editorSegments.RemoveAt(editorSegments.Count - 1);
    }

    public void ApplyFromDatastore(int segmentIndex)
    {
        if (segmentIndex == -1)
        {
            for (int i = 0; i < editorTransforms.Count; i++)
            {
                if (editorTransforms[i])
                {
                    editorTransforms[i].localPosition =
                    serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value;

                    editorTransforms[i].localRotation =
                        serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("rotation").quaternionValue;

                    editorTransforms[i].localScale =
                        serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value;
                }
            }
        }

        else
        {
            for (int i = 0; i < editorTransforms.Count; i++)
            {
                if (editorTransforms[i])
                {
                    editorTransforms[i].localPosition =
                    serializedObject.FindProperty("Segments").GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value;

                    editorTransforms[i].localRotation =
                    serializedObject.FindProperty("Segments").GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("rotation").quaternionValue;

                    editorTransforms[i].localScale =
                    serializedObject.FindProperty("Segments").GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value;
                }
            }
        }
    }

    public void SampleFromScene(int segmentIndex)
    {
        if (segmentIndex == -1)
        {
            for (int i = 0; i < editorTransforms.Count; i++)
            {
                if (editorTransforms[i])
                {
                    serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value
                    = editorTransforms[i].transform.localPosition;

                    serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("rotation").quaternionValue
                        = editorTransforms[i].transform.localRotation;

                    serializedObject.FindProperty("StartStates").GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value
                        = editorTransforms[i].transform.localScale;
                }
            }

            serializedObject.ApplyModifiedProperties();
            CollectEditorStartStates();
        }

        else
        {
            for (int i = 0; i < editorTransforms.Count; i++)
            {
                if (editorTransforms[i])
                {
                    serializedObject.FindProperty("Segments").GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value
                    = editorTransforms[i].transform.localPosition;

                    serializedObject.FindProperty("Segments").GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("rotation").quaternionValue
                        = editorTransforms[i].transform.localRotation;

                    serializedObject.FindProperty("Segments").GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value
                        = editorTransforms[i].transform.localScale;
                }
            }

            serializedObject.ApplyModifiedProperties();
            CollectEditorSegments();
        }
    }

    #endregion

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
            lerpStep = (EditorApplication.timeSinceStartup - startTime) / editorSegments[toIndex].duration;

            if (fromIndex == -1)
            {
                for (int i = 0; i < editorTransforms.Count; i++)
                {
                    if (editorTransforms[i])
                    {
                        editorTransforms[i].localPosition =
                        Vector3.Lerp(editorStartStates[i].position,
                                              editorSegments[toIndex].toTransformData[i].position,
                                              editorSegments[toIndex].curve.Evaluate((float)lerpStep));

                        editorTransforms[i].localRotation =
                            Quaternion.Lerp(editorStartStates[i].rotation,
                                                  editorSegments[toIndex].toTransformData[i].rotation,
                                                  editorSegments[toIndex].curve.Evaluate((float)lerpStep));

                        editorTransforms[i].localScale =
                            Vector3.Lerp(editorStartStates[i].scale,
                                                  editorSegments[toIndex].toTransformData[i].scale,
                                                  editorSegments[toIndex].curve.Evaluate((float)lerpStep));
                    }
                }
            }

            else
            {
                for (int i = 0; i < editorTransforms.Count; i++)
                {
                    if (editorTransforms[i])
                    {
                        editorTransforms[i].localPosition =
                        Vector3.Lerp(editorSegments[fromIndex].toTransformData[i].position,
                                              editorSegments[toIndex].toTransformData[i].position,
                                              editorSegments[toIndex].curve.Evaluate((float)lerpStep));

                        editorTransforms[i].localRotation =
                            Quaternion.Lerp(editorSegments[fromIndex].toTransformData[i].rotation,
                                                  editorSegments[toIndex].toTransformData[i].rotation,
                                                  editorSegments[toIndex].curve.Evaluate((float)lerpStep));

                        editorTransforms[i].localScale =
                            Vector3.Lerp(editorSegments[fromIndex].toTransformData[i].scale,
                                                  editorSegments[toIndex].toTransformData[i].scale,
                                                  editorSegments[toIndex].curve.Evaluate((float)lerpStep));
                    }
                }
            }

            if (lerpStep > 1 )
            {
                //Was it the last segment?
                if (toIndex + 1 > editorSegments.Count -1)
                {
                    //Debug.Log("Detected toIndex + 1 is more than indexes in editorSegments. toIndex + 1 = " + (toIndex + 1 ).ToString());

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

        //Handles UndoRedo
        if (collectEditorDataDelayed && EditorApplication.timeSinceStartup - delayedCollectTimerStart > delayAmount)
        {
            CollectEditorTransforms();
            CollectEditorStartStates();
            CollectEditorSegments();

            collectEditorDataDelayed = false;
        }
    }

    #endregion
}
