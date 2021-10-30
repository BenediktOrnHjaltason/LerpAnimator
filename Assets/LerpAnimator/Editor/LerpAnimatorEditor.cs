using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Events;


[CustomEditor(typeof(LerpAnimator))]
public class LerpAnimatorEditor : Editor
{
    Texture logo;
    Texture toolHandleReminder;

    /// <summary>
    /// transforms references used to playback animation during edit mode. Saving a copy to avoid having find properties on serialized object in OnEditorUpdate() while animating
    /// </summary>
    private List<Transform> editorTransforms;
    /// <summary>
    /// Start states data used to playback animation during edit mode
    /// </summary>
    private List<TransformData> editorStartStates;
    /// <summary>
    /// Segments data used to playback animation during edit mode
    /// </summary>
    private List<Segment> editorSegments;


    private List<bool> editorShowRotationOffsets;
    private List<bool> editorShowSegmentEvents;

    //Properties for accessing parts of serializedObject
    private SerializedProperty serializedStartOnPlay;
    private SerializedProperty serializedTransforms;
    private SerializedProperty serializedStartStates;
    private SerializedProperty serializedSegments;

    //To remember inspector fold out states for segment rotation offsets and events between LerpAnimator object selected/unselected
    private SerializedProperty serializedShowRotations;
    private SerializedProperty serializedShowSegmentEvents;

    /// <summary>
    /// Used for displaying "|>" symbol on last previewed or sampled states (start states or segment states)
    /// </summary>
    private int lastSelectedState;

    /// <summary>
    /// Used for managing periodic checks for changes in serializedTransforms array
    /// </summary>
    private double nextChangeCheck;

    #region Events

    private void OnEnable()
    {
        logo = (Texture)AssetDatabase.LoadAssetAtPath("Assets/LerpAnimator/Editor/T_LerpAnimatorLogo.png", typeof(Texture));
        toolHandleReminder = (Texture)AssetDatabase.LoadAssetAtPath("Assets/LerpAnimator/Editor/T_ToolHandleReminder.png", typeof(Texture));

        serializedStartOnPlay = serializedObject.FindProperty("StartOnPlay");

        serializedTransforms = serializedObject.FindProperty("TransformsToActOn");
        serializedStartStates = serializedObject.FindProperty("StartStates");
        serializedSegments = serializedObject.FindProperty("Segments");

        serializedShowRotations = serializedObject.FindProperty("ShowRotations");
        serializedShowSegmentEvents = serializedObject.FindProperty("ShowSegmentEvents");


        EditorApplication.update += OnEditorUpdate;
        Undo.undoRedoPerformed += OnUndoRedoPerformed;

        editorTransforms = new List<Transform>();
        editorStartStates = new List<TransformData>();
        editorSegments = new List<Segment>();
        editorShowRotationOffsets = new List<bool>();
        editorShowSegmentEvents = new List<bool>();

        CollectEditorTransforms();
        CollectEditorStartStates();
        CollectEditorSegments();
        CollectEditorShowRotations();
        CollectEditorShowSegmentEvents();

        if (serializedSegments.arraySize < 1)
        {
            serializedShowRotations.arraySize++;
            AddSegment();
            
            serializedSegments.GetArrayElementAtIndex(serializedSegments.arraySize - 1).FindPropertyRelative("curve").animationCurveValue = AnimationCurve.Linear(0, 0, 1, 1);
            serializedSegments.GetArrayElementAtIndex(serializedSegments.arraySize - 1).FindPropertyRelative("duration").floatValue = 1;

            serializedObject.ApplyModifiedProperties();

            lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = -1;
        }

        lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue;

        nextChangeCheck = EditorApplication.timeSinceStartup + 0.5f;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
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

        delayedCollectTimerStart = (float)EditorApplication.timeSinceStartup; 
    }

    #endregion

    #region Editor data copies

    private void CollectEditorTransforms()
    {
        editorTransforms.Clear();

        for (int i = 0; i < serializedTransforms.arraySize; i++)
        {
            Transform transform = serializedTransforms.GetArrayElementAtIndex(i).objectReferenceValue as Transform;

            editorTransforms.Add(transform);
        }
    }

    private void CollectEditorStartStates()
    {
        editorStartStates.Clear();

        for (int i = 0; i < serializedStartStates.arraySize; i++)
        {
            Vector3 position = serializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value;
            Vector3 rotation = serializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("offset").vector3Value;
            Vector3 scale = serializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value;

            editorStartStates.Add(new TransformData(position, rotation, scale));
        }
    }

    private void CollectEditorSegments()
    {
        editorSegments.Clear();

        int numberOfSegments = serializedSegments.arraySize;

        for (int i = 0; i < numberOfSegments; i++)
        {
            Segment segment = new Segment();

            int toTransformDataCount = serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").arraySize;

            List<TransformData> toTransformData = new List<TransformData>();

            for (int j = 0; j < toTransformDataCount; j++)
            {
                Vector3 position = serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(j).FindPropertyRelative("position").vector3Value;
                Vector3 rotation = serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(j).FindPropertyRelative("offset").vector3Value;
                Vector3 scale = serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(j).FindPropertyRelative("scale").vector3Value;

                toTransformData.Add(new TransformData(position, rotation, scale));
            }

            segment.toTransformData = toTransformData;
            segment.duration = serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("duration").floatValue;
            segment.curve = serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("curve").animationCurveValue;

            editorSegments.Add(segment);
        }
    }

    private void CollectEditorShowRotations()
    {
        editorShowRotationOffsets.Clear();

        for (int i = 0; i < serializedShowRotations.arraySize; i++)
        {
            editorShowRotationOffsets.Add(serializedShowRotations.GetArrayElementAtIndex(i).boolValue);
        }
    }

    private void CollectEditorShowSegmentEvents()
    {
        editorShowSegmentEvents.Clear();

        for (int i = 0; i < serializedShowSegmentEvents.arraySize; i++)
        {
            editorShowSegmentEvents.Add(serializedShowSegmentEvents.GetArrayElementAtIndex(i).boolValue);
        }
    }
    #endregion

    #region GUI
    

    public override void OnInspectorGUI()
    {
        GUILayout.Box(logo);

        EditorGUILayout.PropertyField(serializedStartOnPlay);

        GUILayout.Space(20);

        GUI.enabled = !editorPlaybackRunning;
        EditorGUILayout.PropertyField(serializedTransforms, true);
        GUI.enabled = true;

        GUILayout.Space(20);

        GUILayout.Label("START STATES - Samples location, rotation and scale");

        GUILayout.BeginHorizontal("Box");
        EditorGUILayout.LabelField(lastSelectedState == -1 ? "|>" : "", GUILayout.Width(20));

        GUI.enabled = !EditorApplication.isPlaying;
        if (GUILayout.Button("Preview"))
        {
            lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = -1;
            serializedObject.ApplyModifiedProperties();
            editorPlaybackRunning = false;
            ApplyFromDatastore(-1);
        }
        GUI.enabled = true;

        GUI.enabled = !editorPlaybackRunning && !EditorApplication.isPlaying;
        if (GUILayout.Button("Sample scene"))
        {
            lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = -1;
            SampleAllFromScene(-1);
        }
        GUI.enabled = true;

        GUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(serializedStartStates, true);

        GUILayout.Space(20);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        GUILayout.Box(toolHandleReminder);
        GUILayout.Label("SEGMENTS - Samples location and scale");
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        

        if (!handlingUndoRedo)
        {
            for (int i = 0; i < serializedSegments.arraySize; i++)
            {
                GUILayout.BeginHorizontal();
                GUI.enabled = !EditorApplication.isPlaying;
                if (GUILayout.Button((i + 1).ToString() + " : Play", GUILayout.Width(90)))
                {
                    CollectEditorSegments();

                    lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = i;
                    ApplyFromDatastore(i - 1);
                    StartEditorPlayback(i - 1);
                }
                GUI.enabled = true;

                if (editorPlaybackRunning && i == toIndex)
                {
                    var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                    EditorGUI.ProgressBar(rect, lerpStep, "");

                }
                GUILayout.EndHorizontal();


                bool showEvents = EditorGUILayout.Foldout(serializedShowSegmentEvents.GetArrayElementAtIndex(i).boolValue, "EventsOnStart", true);

                if (showEvents != editorShowSegmentEvents[i])
                {
                    serializedObject.FindProperty("ShowSegmentEvents").GetArrayElementAtIndex(i).boolValue = showEvents;
                }

                editorShowSegmentEvents[i] = showEvents;

                if (editorShowSegmentEvents[i])
                {
                    EditorGUILayout.PropertyField(serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("OnSegmentStart"));
                }

                //EditorGUILayout.PropertyField(serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("name"));
                EditorGUILayout.PropertyField(serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("duration"));

                EditorGUILayout.PropertyField(serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("curve"));

                EditorGUILayout.PropertyField(serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData"));

                bool showRotation = EditorGUILayout.Foldout(serializedShowRotations.GetArrayElementAtIndex(i).boolValue, "RotationOffsets", true);

                if (showRotation != editorShowRotationOffsets[i])
                {
                    serializedShowRotations.GetArrayElementAtIndex(i).boolValue = showRotation;
                }

                editorShowRotationOffsets[i] = showRotation;

                if (editorShowRotationOffsets[i])
                {
                    for (int j = 0; j < editorTransforms.Count; j++)
                    {

                        GUILayout.BeginHorizontal("Box");

                        
                        EditorGUILayout.LabelField(editorTransforms[j] != null ? editorTransforms[j].name : "*NULL*", GUILayout.Width(100));

                        

                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.PropertyField(serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(j).FindPropertyRelative("offset"));
                        if (EditorGUI.EndChangeCheck())
                        {
                            CollectEditorSegments();
                            ApplyFromDatastore(i);

                            lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = i;
                        }

                        GUILayout.EndHorizontal();
                    }
                }

                serializedObject.ApplyModifiedProperties();

                GUILayout.BeginHorizontal("Box");

                if (!EditorApplication.isPlaying)
                    EditorGUILayout.LabelField(lastSelectedState == i ? "|>" : "", GUILayout.Width(20));

                GUI.enabled = !EditorApplication.isPlaying;
                if (GUILayout.Button("Preview"))
                {
                    lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = i;
                    editorPlaybackRunning = false;
                    ApplyFromDatastore(i);
                }
                GUI.enabled = true;


                GUI.enabled = !editorPlaybackRunning && !EditorApplication.isPlaying;
                if (GUILayout.Button("Sample scene"))
                {
                    lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = i;
                    SampleAllFromScene(i);
                }
                GUI.enabled = true;

                GUILayout.EndHorizontal();
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                GUILayout.Space(15);
                
            }
        }

        GUILayout.BeginHorizontal("Box");

        GUI.enabled = !EditorApplication.isPlaying && !editorPlaybackRunning;
        if (GUILayout.Button("Add segment"))
            AddSegment();


        if (GUILayout.Button("Remove segment"))
        {
            if (serializedSegments.arraySize < 2) 
                return;

            RemoveSegment();
        }
        GUI.enabled = true;

        GUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("NOTE! Adding segment auto samples from scene", MessageType.Info);


        EditorGUILayout.Space(20);

        GUI.enabled = !EditorApplication.isPlaying && !editorPlaybackRunning;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("OnSequenceEnd"));
        GUI.enabled = true;


        serializedObject.ApplyModifiedProperties();
    }

    #endregion

    #region Data consistency

    /// <summary>
    /// Checks if user increased or decreased array size by adjusting number, or replaced or nulled element
    /// </summary>
    private void CheckForTransformsArrayChanged()
    {
        //If user adjusts array count
        if (editorTransforms.Count != serializedTransforms.arraySize)
        {
            if (serializedTransforms.arraySize > editorTransforms.Count)
            {
                Debug.Log("User increased array size");

                OnUserIncreasedTransformsArraySize();
            }
            else
            {
                Debug.Log("User decreased array size");

                OnUserDecreasedTransformsArraySize();
            }
        }

        //Else if user changed existing transform element
        else
        {
            for(int i = 0; i < serializedTransforms.arraySize; i++)
            {
                Transform serializedTransform = (Transform)serializedTransforms.GetArrayElementAtIndex(i).objectReferenceValue;

                if (editorTransforms[i] != serializedTransform)
                {
                    if (serializedTransform == null)
                    {
                        Debug.Log("User nulled reference on list element");

                        editorTransforms[i] = null;

                        ResetDataForSingleElement(i);
                    }

                    else if(IsDuplicate(i, serializedTransform))
                    {
                        Debug.LogWarning("LerpAnimator: Duplicate transform detected. There should only be one reference for each. Nulling element");

                        editorTransforms[i] = null;
                        serializedTransforms.GetArrayElementAtIndex(i).objectReferenceValue = null;

                        serializedObject.ApplyModifiedProperties();
                    }

                    else
                    {
                        Debug.Log("User inserted new transform reference on list element");

                        editorTransforms[i] = serializedTransform;

                        SampleSingleFromSceneToStartStatesAndSegments(i);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks if newly added transform allready exists elsewhere in the collection
    /// </summary>
    /// <param name="newTransformIndex"></param>
    /// <param name="newTransform"></param>
    /// <returns></returns>
    private bool IsDuplicate(int newTransformIndex, Transform newTransform)
    {
        for (int i = 0; i < serializedTransforms.arraySize; i++)
        {
            if (i != newTransformIndex && newTransform == (Transform)serializedTransforms.GetArrayElementAtIndex(i).objectReferenceValue)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Initializes data in start states and segments for single element
    /// </summary>
    /// <param name="indexToReset"></param>
    private void ResetDataForSingleElement(int indexToReset)
    {
        serializedStartStates.GetArrayElementAtIndex(indexToReset).FindPropertyRelative("position").vector3Value =
        editorStartStates[indexToReset].position =

        serializedStartStates.GetArrayElementAtIndex(indexToReset).FindPropertyRelative("offset").vector3Value =
        editorStartStates[indexToReset].offset =

        serializedStartStates.GetArrayElementAtIndex(indexToReset).FindPropertyRelative("scale").vector3Value =
        editorStartStates[indexToReset].scale = Vector3.zero;

        for (int i = 0; i < serializedSegments.arraySize; i++)
        {
            serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexToReset).FindPropertyRelative("position").vector3Value =
                editorSegments[i].toTransformData[indexToReset].position =

            serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexToReset).FindPropertyRelative("offset").vector3Value =
                editorSegments[i].toTransformData[indexToReset].offset =

            serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(indexToReset).FindPropertyRelative("scale").vector3Value =
            editorSegments[i].toTransformData[indexToReset].scale = Vector3.zero;
        }

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Samples position and scale from single transform to start states and all segments
    /// </summary>
    /// <param name="transformIndex"></param>
    private void SampleSingleFromSceneToStartStatesAndSegments(int transformIndex)
    {
        //Depends on editorTransform being updated before calling this function
        serializedStartStates.GetArrayElementAtIndex(transformIndex).FindPropertyRelative("position").vector3Value = 
            editorStartStates[transformIndex].position = editorTransforms[transformIndex].localPosition;

        serializedStartStates.GetArrayElementAtIndex(transformIndex).FindPropertyRelative("scale").vector3Value =
            editorStartStates[transformIndex].scale = editorTransforms[transformIndex].localScale;

        for (int i = 0; i < editorSegments.Count; i++)
        {
            serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(transformIndex).FindPropertyRelative("position").vector3Value =
                editorSegments[i].toTransformData[transformIndex].position = editorTransforms[transformIndex].localPosition;

            serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(transformIndex).FindPropertyRelative("scale").vector3Value =
                editorSegments[i].toTransformData[transformIndex].scale = editorTransforms[transformIndex].localScale;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void OnUserIncreasedTransformsArraySize()
    {
        int difference = serializedTransforms.arraySize - editorTransforms.Count;

        for (int i = serializedTransforms.arraySize - 1; i > 0; i--)
        {
            Transform higherIndexTransform = (Transform)serializedTransforms.GetArrayElementAtIndex(i).objectReferenceValue;
            Transform lowerIndexTransform = (Transform)serializedTransforms.GetArrayElementAtIndex(i - 1).objectReferenceValue;

            if (higherIndexTransform != null && lowerIndexTransform != null && higherIndexTransform == lowerIndexTransform)
                serializedTransforms.GetArrayElementAtIndex(i).objectReferenceValue = null;
        }
            
        serializedObject.ApplyModifiedProperties();

        CollectEditorTransforms();

        AddDataElementsForNewlyAddedTransformsElements(difference);
    }
    
    private void OnUserDecreasedTransformsArraySize()
    {
        //First find out if editor transforms are equal up to new size of serialized transforms (User deleted last element or down adjusted array size of transforms)
        bool editorSegmentsContainsSameTransforms = true;

        for (int i = 0; i < serializedTransforms.arraySize; i++)
        {
            if (serializedTransforms.GetArrayElementAtIndex(i).objectReferenceValue != editorTransforms[i])
                editorSegmentsContainsSameTransforms = false;
        }

        //If it does, delete transforms and data from end of collections
        if (editorSegmentsContainsSameTransforms)
        {
            Debug.Log("User deleted element(s) from end of list");

            int difference = editorTransforms.Count - serializedTransforms.arraySize;

            for (int j = 0; j < difference; j++)
            {
                if (editorStartStates.Count > 0 && editorSegments.Count > 0)
                {
                    editorTransforms.RemoveAt(editorTransforms.Count - 1);

                    //Delete from end of start states
                    editorStartStates.RemoveAt(editorStartStates.Count - 1);
                    serializedStartStates.arraySize--;

                    for (int k = 0; k < editorSegments.Count; k++)
                    {
                        //Delete from end of transforms data in segments
                        editorSegments[k].toTransformData.RemoveAt(editorSegments[k].toTransformData.Count - 1);

                        serializedSegments.GetArrayElementAtIndex(k).FindPropertyRelative("toTransformData").arraySize--;
                    }

                    serializedObject.ApplyModifiedProperties();
                }
            }
        }

        //Else if user deleted element other than last index(es)
        else
        {
            Debug.Log("User deleted element in middle of list");

            for (int i = 0; i < serializedTransforms.arraySize; i++)
            {
                if ((Transform)serializedTransforms.GetArrayElementAtIndex(i).objectReferenceValue != editorTransforms[i])
                {
                    editorTransforms.RemoveAt(i);
                    editorStartStates.RemoveAt(i);

                    serializedStartStates.DeleteArrayElementAtIndex(i);

                    for (int j = 0; j < serializedSegments.arraySize; j++)
                    {
                        editorSegments[j].toTransformData.RemoveAt(i);
                        serializedSegments.GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").DeleteArrayElementAtIndex(i);
                    }

                    serializedObject.ApplyModifiedProperties();

                    break;
                }
            }
        }
    }

    /// <summary>
    /// Adds Start State data and Segments data when user increased transforms count, either by dropping one transform onto array or by manually increasing array size by any amount.
    /// NOTE! Necessary even if null elements added
    /// </summary>
    /// <param name="difference"></param>
    private void AddDataElementsForNewlyAddedTransformsElements(int difference)
    {
        for (int i = 0; i < difference; i++)
        {
            serializedStartStates.arraySize++;

            int newStartStatesCount = serializedObject.FindProperty("StartStates").arraySize;

            //--- Add states, initialized to object added
            //StartStates
            serializedStartStates.GetArrayElementAtIndex(newStartStatesCount - 1).FindPropertyRelative("position").vector3Value = editorTransforms[newStartStatesCount - 1] == null ? Vector3.zero : editorTransforms[newStartStatesCount - 1].localPosition;
            serializedStartStates.GetArrayElementAtIndex(newStartStatesCount - 1).FindPropertyRelative("offset").vector3Value = Vector3.zero;
            serializedStartStates.GetArrayElementAtIndex(newStartStatesCount - 1).FindPropertyRelative("scale").vector3Value = editorTransforms[newStartStatesCount - 1] == null ? Vector3.zero : editorTransforms[newStartStatesCount - 1].localScale;

            //Segments
            for (int j = 0; j < serializedSegments.arraySize; j++)
            {
                serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").arraySize++;

                int newToTransformDataCount = serializedObject.FindProperty("Segments").GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").arraySize;

                serializedSegments.GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(newToTransformDataCount - 1).FindPropertyRelative("position").vector3Value = editorTransforms[newToTransformDataCount - 1] == null ? Vector3.zero : editorTransforms[newToTransformDataCount - 1].localPosition;
                serializedSegments.GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(newToTransformDataCount - 1).FindPropertyRelative("offset").vector3Value =  Vector3.zero;
                serializedSegments.GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(newToTransformDataCount - 1).FindPropertyRelative("scale").vector3Value = editorTransforms[newToTransformDataCount - 1] == null ? Vector3.zero : editorTransforms[newToTransformDataCount - 1].localScale;
            }
        }

        serializedObject.ApplyModifiedProperties();

        CollectEditorStartStates();
        CollectEditorSegments();
    }

    private void InsertDataForNewlyOverriddenTransform(int index)
    {
        //Start states
        serializedStartStates.GetArrayElementAtIndex(index).FindPropertyRelative("position").vector3Value = editorTransforms[index] == null ? Vector3.zero : editorTransforms[index].localPosition;
        serializedStartStates.GetArrayElementAtIndex(index).FindPropertyRelative("offset").vector3Value = Vector3.zero;
        serializedStartStates.GetArrayElementAtIndex(index).FindPropertyRelative("scale").vector3Value = editorTransforms[index] == null ? Vector3.zero : editorTransforms[index].localScale;

        //Segments
        int segmentsCount = serializedObject.FindProperty("Segments").arraySize;

        for (int i = 0; i < segmentsCount; i++)
        {
            serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(index).FindPropertyRelative("position").vector3Value = editorTransforms[index] == null ? Vector3.zero : editorTransforms[index].localPosition;
            serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(index).FindPropertyRelative("offset").vector3Value = Vector3.zero;
            serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(index).FindPropertyRelative("scale").vector3Value = editorTransforms[index] == null ? Vector3.zero : editorTransforms[index].localScale;
        }

        serializedObject.ApplyModifiedProperties();
    }

    #endregion

    #region User Operation

    private void AddSegment()
    {
        serializedSegments.arraySize++;

        //Remove rotation offsets data if inherited from previous segment
        for (int i = 0; i < serializedTransforms.arraySize; i++)
            serializedSegments.GetArrayElementAtIndex(serializedSegments.arraySize - 1).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("offset").vector3Value = Vector3.zero;

        serializedObject.ApplyModifiedProperties();

        //Insert data for transforms allready in array

        int indexAdded_Segments = serializedSegments.arraySize -1;

        lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = indexAdded_Segments;

        serializedSegments.GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("duration").floatValue = 1;
        serializedSegments.GetArrayElementAtIndex(indexAdded_Segments).FindPropertyRelative("curve").animationCurveValue = AnimationCurve.Linear(0, 0, 1, 1);

        
        CollectEditorSegments();


        editorShowRotationOffsets.Add(false);
        editorShowSegmentEvents.Add(false);
        serializedShowRotations.arraySize++;
        serializedShowSegmentEvents.arraySize++;

        serializedObject.ApplyModifiedProperties();

        //Sample to new segment from current scene
        SampleAllFromScene(indexAdded_Segments);
    }

    private void RemoveSegment()
    {
        serializedSegments.arraySize--;
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
            for (int i = 0; i < editorTransforms.Count; i++)
            {
                if (editorTransforms[i])
                {
                    editorTransforms[i].localPosition =
                        serializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value;

                    editorTransforms[i].localRotation =
                        Quaternion.Euler(serializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("offset").vector3Value);


                    editorTransforms[i].localScale =
                        serializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value;
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
                    serializedSegments.GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value;

                    Vector3 acculumatedRotationOffsett = Vector3.zero;

                    for (int j = 0; j <= segmentIndex; j++)
                        acculumatedRotationOffsett += serializedSegments.GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("offset").vector3Value;


                    editorTransforms[i].localRotation = Quaternion.Euler(serializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("offset").vector3Value) * Quaternion.Euler(acculumatedRotationOffsett);



                    editorTransforms[i].localScale =
                    serializedSegments.GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value;
                }
            }
        }
    }

    /// <summary>
    /// Samples states from all transforms to start state or specific segment
    /// </summary>
    /// <param name="segmentIndex"></param>
    public void SampleAllFromScene(int segmentIndex)
    {
        if (segmentIndex == -1)
        {
            for (int i = 0; i < editorTransforms.Count; i++)
            {
                if (editorTransforms[i])
                {
                    serializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value
                    = editorTransforms[i].transform.localPosition;

                    serializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("offset").vector3Value
                        = editorTransforms[i].transform.localRotation.eulerAngles;

                    serializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value
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
                    serializedSegments.GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value
                    = editorTransforms[i].transform.localPosition;


                    serializedSegments.GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value
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

    private double startTime;
    private float lerpStep;
    private int fromIndex;
    private int toIndex;
    private bool editorPlaybackRunning;

    public void StartEditorPlayback(int fromIndex)
    {
        if (serializedSegments.arraySize == 0) return;

        ApplyFromDatastore(fromIndex);

        startTime = (float)EditorApplication.timeSinceStartup;

        nextLerpUpdate = startTime + lerpFrequency;

        this.fromIndex = fromIndex;

        toIndex = fromIndex == -1 ? 0 : fromIndex + 1;

        SampleInterSegmentRotations();

        reciprocal = 1 / editorSegments[toIndex].duration;

        editorPlaybackRunning = true;
    }

    private List<Quaternion> interSegmentRotations;


    private readonly double lerpFrequency = 0.0166; //60 times per second
    private double nextLerpUpdate;
    private float reciprocal;

    

    private void OnEditorUpdate()
    {
        if (editorPlaybackRunning && EditorApplication.timeSinceStartup > nextLerpUpdate)
        {
            nextLerpUpdate += lerpFrequency;
            Repaint();

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
                    editorPlaybackRunning = false;
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

                    lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = toIndex;
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

        //Handles periodic checks for when user makes changes in serializedTransforms array
        if(!handlingUndoRedo && EditorApplication.timeSinceStartup > nextChangeCheck)
        {
            nextChangeCheck = EditorApplication.timeSinceStartup + 0.5f;

            CheckForTransformsArrayChanged();
        }
    }

    #endregion
}
