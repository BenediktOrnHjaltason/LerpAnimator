﻿using System.Collections;
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

    List<bool> showRotationOffsets;
    List<bool> showSegmentEvents;

    SerializedProperty SerializedTransforms;
    SerializedProperty SerializedSegments;
    SerializedProperty SerializedStartStates;

    SerializedProperty SerializedShowRotations;
    SerializedProperty SerializedShowSegmentEvents;

    int serializedArrayCount;

    #region Events

    private void OnEnable()
    {
        SerializedTransforms = serializedObject.FindProperty("TransformsToActOn");
        SerializedStartStates = serializedObject.FindProperty("StartStates");
        SerializedSegments = serializedObject.FindProperty("Segments");

        SerializedShowRotations = serializedObject.FindProperty("ShowRotations");
        SerializedShowSegmentEvents = serializedObject.FindProperty("ShowSegmentEvents");


        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;

        showRotationOffsets = new List<bool>();
        showSegmentEvents = new List<bool>();

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

        lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue;

        for (int i = 0; i < SerializedSegments.arraySize; i++)
        {
            showRotationOffsets.Add(SerializedShowRotations.GetArrayElementAtIndex(i).boolValue);
            showSegmentEvents.Add(SerializedShowSegmentEvents.GetArrayElementAtIndex(i).boolValue);
        }
            

    }

    private void OnDisable()
    {
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
    bool handlingUndoRedo = false;
    float delayedCollectTimerStart;
    const float delayAmount = 0.05f;
    private void OnUndoRedoPerformed()
    {
        handlingUndoRedo = true;

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

        int numberOfStartStates = SerializedStartStates.arraySize;

        for (int i = 0; i < numberOfStartStates; i++)
        {
            Vector3 position = SerializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value;
            Vector3 rotation = SerializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("rotation").vector3Value;
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
                Vector3 rotation = SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(j).FindPropertyRelative("rotation").vector3Value;
                Vector3 scale = SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(j).FindPropertyRelative("scale").vector3Value;

                toTransformData.Add(new TransformData(position, rotation, scale));
            }

            segment.toTransformData = toTransformData;
            segment.duration = SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("duration").floatValue;
            segment.curve = SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("curve").animationCurveValue;

            editorSegments.Add(segment);
        }
    }
    #endregion

    #region GUI

    bool modifyingSegmentsNumber = false;
    bool OnGUIChangedCalled = false;

    int lastSelectedState;


    bool handlingUserDeletedElement = false;
    public override void OnInspectorGUI()
    {
        if (handlingUserDeletedElement) return;

        EditorGUILayout.HelpBox("\"Sample\" fetches positions and scales from scene. \n Rotations entered will be offsett from start rotations", MessageType.Info);

        EditorGUILayout.PropertyField(SerializedTransforms, true);


        GUILayout.Space(20);
        GUILayout.Label("START STATES");

        GUILayout.BeginHorizontal("Box");
        EditorGUILayout.LabelField(lastSelectedState == -1 ? "|>" : "", GUILayout.Width(20));

        if (GUILayout.Button("Preview"))
        {
            lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = -1;
            serializedObject.ApplyModifiedProperties();
            playbackRunning = false;
            ApplyFromDatastore(-1);
        }
        
        if (GUILayout.Button("Sample (pos,rot,scale)"))
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
                StartPlayback(-1);
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

        EditorGUILayout.PropertyField(SerializedStartStates, true);


        GUILayout.Space(20);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        GUILayout.Label("SEGMENTS");
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        int numberOfSegments = SerializedSegments.arraySize;


        EditorGUILayout.Space(10);

        if (!modifyingSegmentsNumber && !handlingUndoRedo)
        {

            for (int i = 0; i < numberOfSegments; i++)
            {
                GUILayout.Label((i+1).ToString());

                bool showEvents = EditorGUILayout.Foldout(SerializedShowSegmentEvents.GetArrayElementAtIndex(i).boolValue, "EventsOnStart", true);

                if (showEvents != showSegmentEvents[i])
                {
                    serializedObject.FindProperty("ShowSegmentEvents").GetArrayElementAtIndex(i).boolValue = showEvents;
                }

                showSegmentEvents[i] = showEvents;

                if (showSegmentEvents[i])
                {
                    EditorGUILayout.PropertyField(SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("OnSegmentStart"));
                }

                EditorGUILayout.PropertyField(SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("duration"));
                EditorGUILayout.PropertyField(SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("curve"));

                bool showRotation = EditorGUILayout.Foldout(SerializedShowRotations.GetArrayElementAtIndex(i).boolValue, "RotationOffsets", true);

                if (showRotation != showRotationOffsets[i])
                {
                    serializedObject.FindProperty("ShowRotations").GetArrayElementAtIndex(i).boolValue = showRotation;
                }

                showRotationOffsets[i] = showRotation;

                if (showRotationOffsets[i])
                {
                    for (int j = 0; j < editorTransforms.Count; j++)
                    {

                        GUILayout.BeginHorizontal("Box");

                        
                        EditorGUILayout.LabelField(editorTransforms[j] != null ? editorTransforms[j].name : "*NULL*", GUILayout.Width(100));

                        

                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.PropertyField(SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(j).FindPropertyRelative("rotation"));
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

                

                if (GUILayout.Button("Sample (pos & scale)"))
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
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            }
        }

        GUILayout.BeginHorizontal("Box");

        if (GUILayout.Button("Add segment"))
        {
            modifyingSegmentsNumber = true;
            AddSegment();
            modifyingSegmentsNumber = false;


        }

        if (GUILayout.Button("Remove segment"))
        {
            if (numberOfSegments < 2) return;

            modifyingSegmentsNumber = true;
            RemoveSegment();
            modifyingSegmentsNumber = false;


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
            SerializedStartStates.GetArrayElementAtIndex(newStartStatesCount - 1).FindPropertyRelative("rotation").vector3Value = Vector3.zero;
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
                SerializedSegments.GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(newToTransformDataCount - 1).FindPropertyRelative("rotation").vector3Value =  Vector3.zero;
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
        SerializedStartStates.GetArrayElementAtIndex(index).FindPropertyRelative("rotation").vector3Value = Vector3.zero;
        SerializedStartStates.GetArrayElementAtIndex(index).FindPropertyRelative("scale").vector3Value = editorTransforms[index] == null ? Vector3.zero : editorTransforms[index].localScale;

        //Segments
        int segmentsCount = serializedObject.FindProperty("Segments").arraySize;

        for (int i = 0; i < segmentsCount; i++)
        {
            SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(index).FindPropertyRelative("position").vector3Value = editorTransforms[index] == null ? Vector3.zero : editorTransforms[index].localPosition;
            SerializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(index).FindPropertyRelative("rotation").vector3Value = Vector3.zero;
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

        SerializedSegments.GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("duration").floatValue = 1;
        SerializedSegments.GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("curve").animationCurveValue = AnimationCurve.Linear(0, 0, 1, 1);


        int numberOfTransforms = SerializedTransforms.arraySize;
        
        //Clear pre filled array elements
        SerializedSegments.GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").ClearArray();

        for (int i = 0; i < numberOfTransforms; i++)
        {

            SerializedSegments.GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").arraySize++;

            serializedObject.ApplyModifiedProperties();

            int indexAdded_Data = SerializedSegments.GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").arraySize - 1;

            if (indexAdded_Segments == 0)
            {
                SerializedSegments.GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("position").vector3Value =
                SerializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value;

                SerializedSegments.GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("rotation").vector3Value =
                SerializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("rotation").vector3Value;

                SerializedSegments.GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("scale").vector3Value =
                SerializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value;
            }

            else
            {
                SerializedSegments.GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("position").vector3Value =
                SerializedSegments.GetArrayElementAtIndex(indexAdded_Segments - 1).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("position").vector3Value;

                SerializedSegments.GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("rotation").vector3Value = Vector3.zero;
                //serializedObject.FindProperty("Segments").GetArrayElementAtIndex(indexAdded_Segments - 1).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("rotation").vector3Value;

                SerializedSegments.GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("scale").vector3Value =
                SerializedSegments.GetArrayElementAtIndex(indexAdded_Segments - 1).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexAdded_Data).FindPropertyRelative("scale").vector3Value;
            }
            

            serializedObject.ApplyModifiedProperties();
        }

        CollectEditorSegments();

        showRotationOffsets.Add(false);
        showSegmentEvents.Add(false);
        serializedObject.FindProperty("ShowRotations").arraySize++;
        serializedObject.FindProperty("ShowSegmentEvents").arraySize++;
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
        showRotationOffsets.RemoveAt(editorSegments.Count - 1);
        showSegmentEvents.RemoveAt(editorSegments.Count - 1);

        serializedObject.FindProperty("ShowRotations").arraySize--;
        serializedObject.FindProperty("ShowSegmentEvents").arraySize--;
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
                    SerializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value;

                    if (editorTransforms[i].parent == null)
                    editorTransforms[i].rotation =
                        Quaternion.Euler(SerializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("rotation").vector3Value);

                    else editorTransforms[i].localRotation =
                        Quaternion.Euler(SerializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("rotation").vector3Value);


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
                        acculumatedRotationOffsett += SerializedSegments.GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("rotation").vector3Value;


                    editorTransforms[i].localEulerAngles = SerializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("rotation").vector3Value + acculumatedRotationOffsett;



                    //editorTransforms[i].localRotation 

                    /*
                    editorTransforms[i].localRotation =
                    Quaternion.Euler(serializedObject.FindProperty("Segments").GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("rotation").vector3Value);
                    */


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

                    SerializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("rotation").vector3Value
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

    double startTime;
    double lerpStep;
    int fromIndex;
    int toIndex;
    int indexStartedFrom;

    bool playbackRunning;

    private void StartPlayback(int fromIndex)
    {
        if (SerializedSegments.arraySize == 0) return;

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

                        if (editorSegments[toIndex].toTransformData[i].rotation != Vector3.zero)
                        {
                            editorTransforms[i].localRotation =
                                Quaternion.Euler(Vector3.Lerp(editorStartStates[i].rotation,
                                                      editorStartStates[i].rotation + editorSegments[toIndex].toTransformData[i].rotation,
                                                      editorSegments[toIndex].curve.Evaluate((float)lerpStep)));
                        }

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


                        if (editorSegments[toIndex].toTransformData[i].rotation != Vector3.zero)
                        {
                            Vector3 accumulatedPreviousRotationOffsets = Vector3.zero;
                            for (int j = 0; j < toIndex; j++)
                                accumulatedPreviousRotationOffsets += editorSegments[j].toTransformData[i].rotation;

                            editorTransforms[i].localRotation =
                            Quaternion.Euler(Vector3.Lerp(editorStartStates[i].rotation + accumulatedPreviousRotationOffsets,
                                                  editorStartStates[i].rotation + accumulatedPreviousRotationOffsets + editorSegments[toIndex].toTransformData[i].rotation,
                                                  editorSegments[toIndex].curve.Evaluate((float)lerpStep)));
                        }
                        
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
        if (handlingUndoRedo && EditorApplication.timeSinceStartup - delayedCollectTimerStart > delayAmount)
        {
            CollectEditorTransforms();
            CollectEditorStartStates();
            CollectEditorSegments();

            handlingUndoRedo = false;
        }
    }

    #endregion
}
