using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Events;


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


    List<bool> editorShowRotationOffsets;
    List<bool> editorShowSegmentEvents;

    SerializedProperty SerializedStartOnPlay;
    

    SerializedProperty SerializedTransforms;
    SerializedProperty SerializedSegments;
    SerializedProperty SerializedStartStates;

    //To remember inspector fold out states for segment rotations and events
    SerializedProperty SerializedShowRotations;
    SerializedProperty SerializedShowSegmentEvents;

    int serializedArrayCount;

    #region Events

    private void OnEnable()
    {
        SerializedStartOnPlay = serializedObject.FindProperty("StartOnPlay");

        SerializedTransforms = serializedObject.FindProperty("TransformsToActOn");
        SerializedStartStates = serializedObject.FindProperty("StartStates");
        SerializedSegments = serializedObject.FindProperty("Segments");

        SerializedShowRotations = serializedObject.FindProperty("ShowRotations");
        SerializedShowSegmentEvents = serializedObject.FindProperty("ShowSegmentEvents");


        EditorApplication.update += OnEditorUpdate;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;

        editorShowRotationOffsets = new List<bool>();
        editorShowSegmentEvents = new List<bool>();

        if (SerializedSegments.arraySize < 1)
        {
            SerializedShowRotations.arraySize++;
            AddSegment();
            serializedObject.ApplyModifiedProperties();

            SerializedSegments.GetArrayElementAtIndex(SerializedSegments.arraySize - 1).FindPropertyRelative("curve").animationCurveValue = AnimationCurve.Linear(0, 0, 1, 1);
            SerializedSegments.GetArrayElementAtIndex(SerializedSegments.arraySize - 1).FindPropertyRelative("duration").floatValue = 1;

            lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = -1;
        }

        CollectEditorTransforms();
        CollectEditorStartStates();
        CollectEditorSegments();
        CollectEditorShowRotations();

        lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue;

        for (int i = 0; i < SerializedSegments.arraySize; i++)
        {
            editorShowRotationOffsets.Add(SerializedShowRotations.GetArrayElementAtIndex(i).boolValue);
            editorShowSegmentEvents.Add(SerializedShowSegmentEvents.GetArrayElementAtIndex(i).boolValue);
        }
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
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
    bool handlingUndoRedo = false;
    float delayedCollectTimerStart;
    const float delayAmount = 0.05f;
    private void OnUndoRedoPerformed()
    {
        handlingUndoRedo = true;
        processingData = true;

        CollectEditorTransforms();
        CollectEditorStartStates();
        CollectEditorSegments();

        Debug.Log("OnUndoRedoPerformed called");
        delayedCollectTimerStart = (float)EditorApplication.timeSinceStartup;
        
    }

    #endregion

    #region Editor data copies

    private void CollectEditorTransforms()
    {
        serializedArrayCount = SerializedTransforms.arraySize;

        editorTransforms = new List<Transform>();

        for (int i = 0; i < serializedArrayCount; i++)
        {
            Transform transform = SerializedTransforms.GetArrayElementAtIndex(i).objectReferenceValue as Transform;

            editorTransforms.Add(transform);
        }

        serializedArrayCount = editorTransforms.Count;
    }

    private void CollectEditorStartStates()
    {
        editorStartStates = new List<TransformData>();

        for (int i = 0; i < SerializedStartStates.arraySize; i++)
        {
            Vector3 position = SerializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value;
            Vector3 rotation = SerializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("offset").vector3Value;
            Vector3 scale = SerializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value;

            editorStartStates.Add(new TransformData(position, rotation, scale));
        }
    }

    private void CollectEditorSegments()
    {
        editorSegments = new List<Segment>();

        int numberOfSegments = SerializedSegments.arraySize;

        for (int i = 0; i < numberOfSegments; i++)
        {
            Segment segment = new Segment();

            int toTransformDataCount = SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").arraySize;

            List<TransformData> toTransformData = new List<TransformData>();

            for (int j = 0; j < toTransformDataCount; j++)
            {
                Vector3 position = SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(j).FindPropertyRelative("position").vector3Value;
                Vector3 rotation = SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(j).FindPropertyRelative("offset").vector3Value;
                Vector3 scale = SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(j).FindPropertyRelative("scale").vector3Value;

                toTransformData.Add(new TransformData(position, rotation, scale));
            }

            segment.toTransformData = toTransformData;
            segment.duration = SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("duration").floatValue;
            segment.curve = SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("curve").animationCurveValue;

            editorSegments.Add(segment);
        }
    }

    private void CollectEditorShowRotations()
    {
        editorShowRotationOffsets = new List<bool>();

        for (int i = 0; i < SerializedShowRotations.arraySize; i++)
        {
            editorShowRotationOffsets.Add(SerializedShowRotations.GetArrayElementAtIndex(i).boolValue);
        }
    }
    #endregion

    #region GUI

    bool modifyingSegmentsNumber = false;
    bool OnGUIChangedCalled = false;

    int lastSelectedState;

    bool processingData;


    bool handlingUserDeletedElement = false;
    public override void OnInspectorGUI()
    {
        if (handlingUserDeletedElement) return;

        EditorGUILayout.PropertyField(SerializedStartOnPlay);

        EditorGUILayout.PropertyField(SerializedTransforms, true);


        GUILayout.Space(20);
        GUILayout.Label("START STATES - Samples location, rotation and scale");

        GUILayout.BeginHorizontal("Box");
        EditorGUILayout.LabelField(lastSelectedState == -1 ? "|>" : "", GUILayout.Width(20));

        if (GUILayout.Button("Preview"))
        {
            lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = -1;
            serializedObject.ApplyModifiedProperties();
            playbackRunning = false;
            ApplyFromDatastore(-1);
        }
        
        if (GUILayout.Button("Sample"))
        {
            lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = -1;
            SampleFromScene(-1);
        }

        if(SerializedSegments.arraySize > 0)
        {
            if (GUILayout.Button("Play"))
            {
                CollectEditorSegments();

                lastSelectedState  = serializedObject.FindProperty("lastSelectedState").intValue = -1;
                ApplyFromDatastore(-1);
                StartEditorPlayback(-1);
            }

            /*
            if (GUILayout.Button("STOP"))
            {
                ApplyFromDatastore(lastSelectedState);
                playbackRunning = false;
            }
            */
        }

        GUILayout.EndHorizontal();

        //EditorGUILayout.PropertyField(SerializedStartStates, true);


        GUILayout.Space(20);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        GUILayout.Label("SEGMENTS - Samples location and scale");
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        int numberOfSegments = SerializedSegments.arraySize;


        EditorGUILayout.Space(10);

        if (!modifyingSegmentsNumber && !handlingUndoRedo)
        {

            for (int i = 0; i < numberOfSegments; i++)
            {
                GUILayout.Label((i+1).ToString());

                bool showEvents = EditorGUILayout.Foldout(SerializedShowSegmentEvents.GetArrayElementAtIndex(i).boolValue, "EventsOnStart", true);

                if (showEvents != editorShowSegmentEvents[i])
                {
                    serializedObject.FindProperty("ShowSegmentEvents").GetArrayElementAtIndex(i).boolValue = showEvents;
                }

                editorShowSegmentEvents[i] = showEvents;

                if (editorShowSegmentEvents[i])
                {
                    EditorGUILayout.PropertyField(SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("OnSegmentStart"));
                }

                EditorGUILayout.PropertyField(SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("name"));
                EditorGUILayout.PropertyField(SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("duration"));
                EditorGUILayout.PropertyField(SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("curve"));

                bool showRotation = EditorGUILayout.Foldout(SerializedShowRotations.GetArrayElementAtIndex(i).boolValue, "RotationOffsets", true);

                if (showRotation != editorShowRotationOffsets[i])
                {
                    SerializedShowRotations.GetArrayElementAtIndex(i).boolValue = showRotation;
                }

                editorShowRotationOffsets[i] = showRotation;

                if (editorShowRotationOffsets[i])
                {
                    for (int j = 0; j < editorTransforms.Count; j++)
                    {

                        GUILayout.BeginHorizontal("Box");

                        
                        EditorGUILayout.LabelField(editorTransforms[j] != null ? editorTransforms[j].name : "*NULL*", GUILayout.Width(100));

                        

                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.PropertyField(SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(j).FindPropertyRelative("offset"));
                        if (EditorGUI.EndChangeCheck())
                        {
                            //OnRotationEntered(i);

                            CollectEditorSegments();
                            ApplyFromDatastore(i);

                            lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = i;
                        }

                        GUILayout.EndHorizontal();
                    }
                }

                serializedObject.ApplyModifiedProperties();

                GUILayout.BeginHorizontal("Box");

                EditorGUILayout.LabelField(lastSelectedState == i ? "|>" : "", GUILayout.Width(20));

                if (GUILayout.Button("Preview"))
                {
                    lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = i;
                    playbackRunning = false;
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
                    StartEditorPlayback(i);
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(15);
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            }
        }

        GUILayout.BeginHorizontal("Box");

        if (GUILayout.Button("Add segment"))
        {
            processingData = true;
            modifyingSegmentsNumber = true;
            AddSegment();
            modifyingSegmentsNumber = false;
            processingData = false;


        }

        if (GUILayout.Button("Remove segment"))
        {
            if (numberOfSegments < 2) return;

            processingData = true;
            modifyingSegmentsNumber = true;
            RemoveSegment();
            modifyingSegmentsNumber = false;
            processingData = false;


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

        int currentTransformsArraySize = SerializedTransforms.arraySize;

        //If user deletes array element completely
        if (!OnGUIChangedCalled && !handlingUndoRedo && currentTransformsArraySize < serializedArrayCount)
        {
            Debug.Log("Detected user deleted array element");

            OnUserDeletedElementDirectly();
            serializedObject.Update();
            

            serializedArrayCount = SerializedTransforms.arraySize;
        }
    }

    #endregion

    #region Data consistency
    

    private void CheckForTransformsArrayChanged()
    {
        int currentTransformsArrayCount = SerializedTransforms.arraySize;

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
                Transform serializedTransform = SerializedTransforms.GetArrayElementAtIndex(i).objectReferenceValue as Transform;

                //Insert data for new element
                if (serializedTransform != editorTransforms[i])
                {
                    //First check if no duplicate exists elsewhere
                    int serializedArraySize = SerializedTransforms.arraySize;
                    for (int j = 0; j < serializedArraySize; j++)
                    {

                        if (j != i && serializedTransform == SerializedTransforms.GetArrayElementAtIndex(j).objectReferenceValue as Transform)
                        {
                            Debug.LogWarning("Lerp Animator: You inserted a duplicate transform into the array. Not Allowed. Nulling values");

                            //Null serialized element
                            SerializedTransforms.GetArrayElementAtIndex(i).objectReferenceValue = null;
                            serializedObject.ApplyModifiedProperties();

                            CollectEditorTransforms();
                            InsertDataForNewlyOverriddenTransform(i);

                            return;
                        }
                    }


                    if (serializedTransform != null)
                    {
                        Debug.Log("User set array element value");


                        CollectEditorTransforms();
                        InsertDataForNewlyOverriddenTransform(i);
                        

                        return;
                    }

                    
                    else
                    {
                        Debug.Log("User nulled element");
                        CollectEditorTransforms();
                    }
                }
            }
        }

        serializedArrayCount = SerializedTransforms.arraySize;
    }


    private void OnUserDeletedElementDirectly()
    {
        Debug.Log("OnUserDeletedElementDirectly called");
        handlingUserDeletedElement = true;


        int serializedTransformsCount = SerializedTransforms.arraySize;

        bool foundDeletedBeforeLastIndex = false;
        //remove at index for editor transforms array and data
        for (int i = 0; i < serializedTransformsCount; i++)
        {
            if (SerializedTransforms.arraySize == 0)
            {
                editorTransforms.Clear();

                SerializedStartStates.ClearArray();

                int numberOfSegments = SerializedSegments.arraySize;
                for (int j = 0; j < numberOfSegments; j++)
                {
                    SerializedSegments.GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").ClearArray();
                }

                serializedObject.ApplyModifiedProperties();

                break;
            }

            if (editorTransforms[i] != SerializedTransforms.GetArrayElementAtIndex(i).objectReferenceValue)
            {
                Debug.Log("OnUserDeletedElementDirectly: Found index " + i);
                foundDeletedBeforeLastIndex = true;

                editorTransforms.RemoveAt(i);

                SerializedStartStates.DeleteArrayElementAtIndex(i);

                int numberOfSegments = SerializedSegments.arraySize;
                for (int j = 0; j < numberOfSegments; j++)
                {
                    SerializedSegments.GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").DeleteArrayElementAtIndex(i);
                }

                serializedObject.ApplyModifiedProperties();

                break;
            }
        }

        //If the deleted element was the last index
        if (!foundDeletedBeforeLastIndex)
        {
            editorTransforms.RemoveAt(editorTransforms.Count - 1);

            Debug.Log("SerializedStartStates size = " + SerializedStartStates.arraySize);

            SerializedStartStates.arraySize--;

            

            for (int i = 0; i < SerializedSegments.arraySize; i++)
                SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").arraySize--;

            serializedObject.ApplyModifiedProperties();
        }

        CollectEditorTransforms();
        CollectEditorStartStates();
        CollectEditorSegments();

        handlingUserDeletedElement = false;
    }

    private void OnUserIncreasedTransformsArraySize()
    {
        int newElementsCount = SerializedTransforms.arraySize;
        int difference = newElementsCount - editorTransforms.Count;

        //Null repeating transforms elements due to increasing size
        for (int i = newElementsCount - 1; i > 0; i--)
        {
            Transform higherIndexTransform = (Transform)SerializedTransforms.GetArrayElementAtIndex(i).objectReferenceValue;
            Transform lowerIndexTransform = (Transform)SerializedTransforms.GetArrayElementAtIndex(i - 1).objectReferenceValue;

            if (higherIndexTransform != null && lowerIndexTransform != null && higherIndexTransform == lowerIndexTransform)
            {
                Debug.Log("Detected duplicate");

                SerializedTransforms.GetArrayElementAtIndex(i).objectReferenceValue = null;

                serializedObject.ApplyModifiedProperties();

                
            }
        }


        CollectEditorTransforms();
        //CollectEditorStartStates();
        AddDataElementsForNewlyAddedTransformsElement(difference);
    }
    
    private void OnUserDecreasedTransformsArraySize()
    {
        Debug.Log("OnUserDecreasedTransformsArraySize called");

        int serializedTransformsArrayCount = SerializedTransforms.arraySize;
        int editorTransformsCount = editorTransforms.Count;
        int difference = editorTransformsCount - serializedTransformsArrayCount;

        //Remove from end from all collections
        for (int i = 0; i < difference; i++ )
        {
            editorTransforms.RemoveAt(editorTransforms.Count - 1);

            SerializedStartStates.arraySize--;


            int numberOfSegments = SerializedSegments.arraySize;

            for (int j = 0; j < numberOfSegments; j++)
            {
                SerializedSegments.GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").arraySize--;
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
            SerializedStartStates.arraySize++;

            int newStartStatesCount = serializedObject.FindProperty("StartStates").arraySize;

            //--- Add states, initialized to object added
            //StartStates

            SerializedStartStates.GetArrayElementAtIndex(newStartStatesCount - 1).FindPropertyRelative("position").vector3Value = editorTransforms[newStartStatesCount - 1] == null ? Vector3.zero : editorTransforms[newStartStatesCount - 1].localPosition;
            SerializedStartStates.GetArrayElementAtIndex(newStartStatesCount - 1).FindPropertyRelative("offset").vector3Value = Vector3.zero;
            SerializedStartStates.GetArrayElementAtIndex(newStartStatesCount - 1).FindPropertyRelative("scale").vector3Value = editorTransforms[newStartStatesCount - 1] == null ? Vector3.zero : editorTransforms[newStartStatesCount - 1].localScale;

            //Segments
            //toTransformData amounts should be the same as start states amount

            //TODO: This will only add data to existing segments. What happens if new segments are added after transforms are added?

            int segmentsCount = serializedObject.FindProperty("Segments").arraySize;

            for (int j = 0; j < segmentsCount; j++)
            {
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").arraySize++;

                int newToTransformDataCount = serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").arraySize;

                SerializedSegments.GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(newToTransformDataCount - 1).FindPropertyRelative("position").vector3Value = editorTransforms[newToTransformDataCount - 1] == null ? Vector3.zero : editorTransforms[newToTransformDataCount - 1].localPosition;
                SerializedSegments.GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(newToTransformDataCount - 1).FindPropertyRelative("offset").vector3Value =  Vector3.zero;
                SerializedSegments.GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(newToTransformDataCount - 1).FindPropertyRelative("scale").vector3Value = editorTransforms[newToTransformDataCount - 1] == null ? Vector3.zero : editorTransforms[newToTransformDataCount - 1].localScale;
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
        SerializedStartStates.GetArrayElementAtIndex(index).FindPropertyRelative("position").vector3Value = editorTransforms[index] == null ? Vector3.zero : editorTransforms[index].localPosition;
        SerializedStartStates.GetArrayElementAtIndex(index).FindPropertyRelative("offset").vector3Value = Vector3.zero;
        SerializedStartStates.GetArrayElementAtIndex(index).FindPropertyRelative("scale").vector3Value = editorTransforms[index] == null ? Vector3.zero : editorTransforms[index].localScale;

        //Segments
        int segmentsCount = serializedObject.FindProperty("Segments").arraySize;

        for (int i = 0; i < segmentsCount; i++)
        {
            SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(index).FindPropertyRelative("position").vector3Value = editorTransforms[index] == null ? Vector3.zero : editorTransforms[index].localPosition;
            SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(index).FindPropertyRelative("offset").vector3Value = Vector3.zero;
            SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(index).FindPropertyRelative("scale").vector3Value = editorTransforms[index] == null ? Vector3.zero : editorTransforms[index].localScale;
        }

        serializedObject.ApplyModifiedProperties();
    }

    #endregion

    #region User Operation

    private void AddSegment()
    {
        SerializedSegments.arraySize++;
        serializedObject.ApplyModifiedProperties();

        //Insert data for transforms allready in array

        int indexAdded_Segments = SerializedSegments.arraySize -1;

        lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = indexAdded_Segments;

        SerializedSegments.GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("duration").floatValue = 1;
        SerializedSegments.GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("curve").animationCurveValue = AnimationCurve.Linear(0, 0, 1, 1);

        CollectEditorSegments();

        editorShowRotationOffsets.Add(false);
        editorShowSegmentEvents.Add(false);
        SerializedShowRotations.arraySize++;
        SerializedShowSegmentEvents.arraySize++;

        serializedObject.ApplyModifiedProperties();

        //Sample to new segment from current scene
        SampleFromScene(indexAdded_Segments);
    }

    private void RemoveSegment()
    {
        SerializedSegments.arraySize--;
        serializedObject.ApplyModifiedProperties();


        //If last segment to select was the one we are about to delete, set to previous segment
        if (lastSelectedState == editorSegments.Count - 1)
        {
            ApplyFromDatastore(editorSegments.Count - 2);
            lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = editorSegments.Count - 2;
        }

        editorSegments.RemoveAt(editorSegments.Count - 1);
        editorShowRotationOffsets.RemoveAt(editorSegments.Count - 1);
        editorShowSegmentEvents.RemoveAt(editorSegments.Count - 1);

        serializedObject.FindProperty("ShowRotations").arraySize--;
        serializedObject.FindProperty("ShowSegmentEvents").arraySize--;
    }

    public void ApplyFromDatastore(int segmentIndex)
    {
        if (segmentIndex == -1)
        {
            if (editorTransforms.Count == 0) Debug.Log("LAE: ApplyFromDataStore: EditorTransforms is empty");

            for (int i = 0; i < editorTransforms.Count; i++)
            {
                if (editorTransforms[i])
                {
                    editorTransforms[i].localPosition =
                        SerializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value;

                    editorTransforms[i].localRotation =
                        Quaternion.Euler(SerializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("offset").vector3Value);


                    editorTransforms[i].localScale =
                        SerializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value;
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
                    SerializedSegments.GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value;

                    Vector3 acculumatedRotationOffsett = Vector3.zero;

                    for (int j = 0; j <= segmentIndex; j++)
                        acculumatedRotationOffsett += SerializedSegments.GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("offset").vector3Value;


                    editorTransforms[i].localRotation = Quaternion.Euler(SerializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("offset").vector3Value) * Quaternion.Euler(acculumatedRotationOffsett);



                    editorTransforms[i].localScale =
                    SerializedSegments.GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value;
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
                    SerializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value
                    = editorTransforms[i].transform.localPosition;

                    SerializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("offset").vector3Value
                        = editorTransforms[i].transform.localRotation.eulerAngles;

                    SerializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value
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
                    SerializedSegments.GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value
                    = editorTransforms[i].transform.localPosition;


                    SerializedSegments.GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value
                        = editorTransforms[i].transform.localScale;
                }
            }

            serializedObject.ApplyModifiedProperties();
            CollectEditorSegments();
        }
    }

    #endregion

    #region Playback

    private void SampleInterSegmentRotations()
    {
        //Sample current rotations
        interSegmentRotations = new List<Quaternion>();

        foreach (Transform transform in editorTransforms)
            if (transform)
                interSegmentRotations.Add(transform.localRotation);
            
            //We need something in the array to keep the number of elements correct
            else interSegmentRotations.Add(Quaternion.identity);
    }

    double startTime;
    float lerpStep;
    int fromIndex;
    int toIndex;
    int indexStartedFrom;

    bool playbackRunning;

    public void StartEditorPlayback(int fromIndex)
    {
        if (SerializedSegments.arraySize == 0) return;

        ApplyFromDatastore(fromIndex);

        startTime = (float)EditorApplication.timeSinceStartup;

        nextLerpUpdate = startTime + lerpFrequency;

        this.fromIndex = indexStartedFrom = fromIndex;

        toIndex = fromIndex == -1 ? 0 : fromIndex + 1;

        SampleInterSegmentRotations();

        reciprocal = 1 / editorSegments[toIndex].duration;

        playbackRunning = true;
    }

    private List<Quaternion> interSegmentRotations;


    private readonly double lerpFrequency = 0.0166; //60 times per second
    private double nextLerpUpdate;
    private float reciprocal;
    private void OnEditorUpdate()
    {
        if (playbackRunning && EditorApplication.timeSinceStartup > nextLerpUpdate)
        {
            nextLerpUpdate += lerpFrequency;

            lerpStep = ((float)EditorApplication.timeSinceStartup - (float)startTime) * reciprocal;

            if (fromIndex == -1)
            {
                for (int i = 0; i < editorTransforms.Count; i++)
                {
                    if (editorTransforms[i])
                    {
                        editorTransforms[i].localPosition =
                        Vector3.Lerp(editorStartStates[i].position,
                                              editorSegments[toIndex].toTransformData[i].position,
                                              editorSegments[toIndex].curve.Evaluate(lerpStep));

                        if (editorSegments[toIndex].toTransformData[i].offset != Vector3.zero)
                        {
                            editorTransforms[i].localRotation = Quaternion.Euler(editorStartStates[i].offset) *
                                Quaternion.Euler(Vector3.Lerp(Vector3.zero, editorSegments[toIndex].toTransformData[i].offset, editorSegments[toIndex].curve.Evaluate(lerpStep))); 
                        }

                        editorTransforms[i].localScale =
                                Vector3.Lerp(editorStartStates[i].scale,
                                                      editorSegments[toIndex].toTransformData[i].scale,
                                                      editorSegments[toIndex].curve.Evaluate(lerpStep));
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
                                              editorSegments[toIndex].curve.Evaluate(lerpStep));


                        if (editorSegments[toIndex].toTransformData[i].offset != Vector3.zero)
                        {
                            editorTransforms[i].localRotation = interSegmentRotations[i] *
                                Quaternion.Euler(Vector3.Lerp(Vector3.zero, editorSegments[toIndex].toTransformData[i].offset, editorSegments[toIndex].curve.Evaluate(lerpStep)));

                        }
                        
                        editorTransforms[i].localScale =
                            Vector3.Lerp(editorSegments[fromIndex].toTransformData[i].scale,
                                                  editorSegments[toIndex].toTransformData[i].scale,
                                                  editorSegments[toIndex].curve.Evaluate(lerpStep));
                    }
                }
            }

            

            if (lerpStep > 1 )
            {
                //Was it the last segment?
                if (toIndex + 1 > editorSegments.Count -1)
                {
                    playbackRunning = false;
                    lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = toIndex;
                }

                //Go to next segment
                else
                {
                    

                    SampleInterSegmentRotations();

                    fromIndex = fromIndex == -1 ? 0 : ++fromIndex;
                    toIndex++;

                    reciprocal = 1 / editorSegments[toIndex].duration;

                    startTime = (float)EditorApplication.timeSinceStartup;

                    //Debug.Log("Nect duration = " + editorSegments[toIndex].duration);

                    //Debug.Log("Going to next segment. fromIndex " + fromIndex + ". toIndex =" + toIndex);
                }
            }
        }

        //Handles UndoRedo
        if (handlingUndoRedo && EditorApplication.timeSinceStartup - delayedCollectTimerStart > delayAmount)
        {
            CollectEditorTransforms();
            CollectEditorStartStates();
            CollectEditorSegments();

            handlingUndoRedo = false;
            processingData = false;
        }
    }

    #endregion
}
