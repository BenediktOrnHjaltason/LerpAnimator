using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SpheroidGames.LerpAnimator
{
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
        //private List<TransformData> editorStartStates;
        /// <summary>
        /// Segments data used to playback animation during edit mode
        /// </summary>
        //private List<Segment> editorSegments;

        private List<Sequence> editorSequences;


        //private List<bool> editorShowRotationOffsets;
        //private List<bool> editorShowSegmentEvents;

        //Properties for accessing parts of serializedObject

        //private SerializedProperty serializedSequenceName;

        /// <summary>
        /// whether sequence should start when play in scene starts
        /// </summary>
        //private SerializedProperty serializedStartOnPlay;

        /// <summary>
        /// whether sequence will loop in game play mode
        /// </summary>
        //private SerializedProperty serializedLoop;

        private SerializedProperty serializedTransforms;
        //private SerializedProperty serializedStartStates;
        //private SerializedProperty serializedSegments;

        //To remember inspector fold out states for segment rotation offsets and events between LerpAnimator object selected/unselected
        //private SerializedProperty serializedShowRotations;
        //private SerializedProperty serializedShowSegmentEvents;

        private SerializedProperty serializedSequences;

        /// <summary>
        /// Used for displaying "|>" symbol on last previewed or sampled states (start states or segment states)
        /// </summary>
        private int lastSelectedSequence;
        private int lastSelectedSegment;

        /// <summary>
        /// Used for managing periodic checks for changes in serializedTransforms array
        /// </summary>
        private double nextChangeCheck;

        private GUIStyle labelStyle;

        private GUILayoutOption inputFieldLayoutOption;


        #region Events

        private void OnEnable()
        {
            logo = (Texture)AssetDatabase.LoadAssetAtPath("Assets/LERP Animator/Textures/T_LerpAnimatorLogo.png", typeof(Texture));
            toolHandleReminder = (Texture)AssetDatabase.LoadAssetAtPath("Assets/LERP Animator/Textures/T_ToolHandleReminder.png", typeof(Texture));

            //serializedSequenceName = serializedObject.FindProperty("SequenceName");

            //serializedStartOnPlay = serializedObject.FindProperty("StartOnPlay");
            //serializedLoop = serializedObject.FindProperty("Loop");

            //serializedTransforms = serializedObject.FindProperty("TransformsToActOn");
            //serializedStartStates = serializedObject.FindProperty("StartStates");
            //serializedSegments = serializedObject.FindProperty("Segments");

            serializedSequences = serializedObject.FindProperty("Sequences");

            //serializedShowRotations = serializedObject.FindProperty("ShowRotations");
            //serializedShowSegmentEvents = serializedObject.FindProperty("ShowSegmentEvents");

            if (serializedSequences.arraySize == 0 && serializedSegments.arraySize > 0)
                MigrateToMultiSequenceSystem();

            EditorApplication.update += OnEditorUpdate;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;

            editorTransforms = new List<Transform>();
            //editorStartStates = new List<TransformData>();
            //editorSegments = new List<Segment>();
            editorSequences = new List<Sequence>();


            //editorShowRotationOffsets = new List<bool>();
            //editorShowSegmentEvents = new List<bool>();

            labelStyle = new GUIStyle();
            labelStyle.alignment = TextAnchor.UpperCenter;
            labelStyle.normal.textColor = Color.white;

            //inputFieldLayoutOption = GUILayout.

            CollectEditorTransforms();
            //CollectEditorStartStates();
            //CollectEditorSegments();
            //CollectEditorShowRotations();
            //CollectEditorShowSegmentEvents();

            if (serializedSegments.arraySize < 1)
            {
                serializedObject.FindProperty("lastSelectedState").intValue = -1;
                serializedObject.ApplyModifiedProperties();
            }

            lastSelectedSequence = serializedObject.FindProperty("lastSelectedSequence").intValue;
            lastSelectedSegment = serializedObject.FindProperty("lastSelectedSegment").intValue;

            nextChangeCheck = EditorApplication.timeSinceStartup + 0.5f;
        }

        private void MigrateToMultiSequenceSystem()
        {
            //Add new sequence
            serializedSequences.arraySize++;

            //Get reference to new sequence
            SerializedProperty serializedSequence = serializedSequences.GetArrayElementAtIndex(0);

            //Add name
            serializedSequence.FindPropertyRelative("Name").stringValue = serializedSequenceName.stringValue;

            //Add StartOnPlay
            serializedSequence.FindPropertyRelative("StartOnPlay").boolValue = serializedStartOnPlay.boolValue;

            //Add Loop
            serializedSequence.FindPropertyRelative("Loop").boolValue = serializedLoop.boolValue;

            //Add start states
            SerializedProperty newStartStates = serializedSequence.FindPropertyRelative("StartStates");

            for(int i = 0; i < serializedStartStates.arraySize; i++)
            {
                newStartStates.arraySize++;

                SerializedProperty newStartState = newStartStates.GetArrayElementAtIndex(newStartStates.arraySize - 1);

                newStartState.FindPropertyRelative("position").vector3Value = 
                    serializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value;

                newStartState.FindPropertyRelative("offset").vector3Value =
                    serializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("offset").vector3Value;

                newStartState.FindPropertyRelative("scale").vector3Value =
                    serializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value;
            }

            //Add segments

            SerializedProperty newSegmentsArray = serializedSequence.FindPropertyRelative("Segments");

            for (int i = 0; i < serializedSegments.arraySize; i++)
            {
                newSegmentsArray.arraySize++;

                SerializedProperty newSegment = newSegmentsArray.GetArrayElementAtIndex(newSegmentsArray.arraySize - 1);

                SerializedProperty newSegmentTransformsDataArray = newSegment.FindPropertyRelative("toTransformData");

                SerializedProperty oldSegmentTransformsDataArray = serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData");

                for (int j = 0; j < oldSegmentTransformsDataArray.arraySize; j++)
                {
                    newSegmentTransformsDataArray.arraySize++;

                    SerializedProperty newSegmentToTransformsDataElement = newSegmentTransformsDataArray.GetArrayElementAtIndex(newSegmentTransformsDataArray.arraySize - 1);

                    newSegmentToTransformsDataElement.FindPropertyRelative("position").vector3Value = 
                        oldSegmentTransformsDataArray.GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value;

                    newSegmentToTransformsDataElement.FindPropertyRelative("offset").vector3Value =
                        oldSegmentTransformsDataArray.GetArrayElementAtIndex(i).FindPropertyRelative("offset").vector3Value;

                    newSegmentToTransformsDataElement.FindPropertyRelative("scale").vector3Value =
                        oldSegmentTransformsDataArray.GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value;


                    newSegment.FindPropertyRelative("showEvents").boolValue = serializedShowSegmentEvents.GetArrayElementAtIndex(i).boolValue;

                    newSegment.FindPropertyRelative("showRotationOffsets").boolValue = serializedShowRotations.GetArrayElementAtIndex(i).boolValue;
                }

                newSegment.FindPropertyRelative("Name").stringValue = serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("Name").stringValue;

                newSegment.FindPropertyRelative("curve").animationCurveValue = serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("curve").animationCurveValue;

                newSegment.FindPropertyRelative("duration").floatValue = serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("duration").floatValue;

                newSegment.FindPropertyRelative("pauseAfter").floatValue = serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("pauseAfter").floatValue;
            }

            serializedSegments.ClearArray();
            serializedStartStates.ClearArray();

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
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
        private bool handlingUndoRedo = false;
        private float delayedCollectTimerStart;
        private const float delayAmount = 0.05f;


        private void OnUndoRedoPerformed()
        {
            handlingUndoRedo = true;

            if (playingPauseAfterSegment)
                playingPauseAfterSegment = false;
            if (editorPlaybackRunning)
                editorPlaybackRunning = false;

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

        /*
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
        */

        private void CollectEditorSequences()
        {
            editorSequences.Clear();

            for (int i = 0; i < serializedSequences.arraySize; i++)
            {
                Sequence editorSequence = new Sequence();

                //Top level variables
                editorSequence.Name = serializedSequences.GetArrayElementAtIndex(i).FindPropertyRelative("Name").stringValue;
                editorSequence.Loop = serializedSequences.GetArrayElementAtIndex(i).FindPropertyRelative("Loop").boolValue;
                editorSequence.StartOnPlay = serializedSequences.GetArrayElementAtIndex(i).FindPropertyRelative("StartOnPlay").boolValue;
                editorSequence.ShowSegments = serializedSequences.GetArrayElementAtIndex(i).FindPropertyRelative("ShowSegments").boolValue;

                //Start States
                SerializedProperty serializedStartStates = serializedSequences.FindPropertyRelative("StartStates");

                for (int j = 0; j < serializedStartStates.arraySize; j++)
                {
                    SerializedProperty serializedToTransformData = serializedStartStates.GetArrayElementAtIndex(j);

                    TransformData editorStartState = new TransformData(serializedToTransformData.FindPropertyRelative("position").vector3Value,
                                                                       serializedToTransformData.FindPropertyRelative("offset").vector3Value,
                                                                       serializedToTransformData.FindPropertyRelative("scale").vector3Value);

                    editorSequence.StartStates.Add(editorStartState);

                }

                //Segments
                for (int j = 0; j < serializedSequences.FindPropertyRelative("Segments").arraySize; j++)
                {
                    SerializedProperty serializedSegment = serializedSequences.FindPropertyRelative("Segments").GetArrayElementAtIndex(j);

                    Segment editorSegment = new Segment();

                    editorSegment.Name = serializedSegment.FindPropertyRelative("Name").stringValue;
                    editorSegment.curve = serializedSegment.FindPropertyRelative("curve").animationCurveValue;
                    editorSegment.duration = serializedSegment.FindPropertyRelative("duration").floatValue;
                    editorSegment.pauseAfter = serializedSegment.FindPropertyRelative("pauseAfter").floatValue;
                    editorSegment.showEvents = serializedSegment.FindPropertyRelative("showEvents").boolValue;
                    editorSegment.showRotationOffsets = serializedSegment.FindPropertyRelative("showRotationOffsets").boolValue;

                    for (int k = 0; k < serializedSegment.FindPropertyRelative("toTransformData").arraySize; k++)
                    {
                        SerializedProperty serializedToTransformData = serializedSegment.FindPropertyRelative("toTransformData").GetArrayElementAtIndex(k);

                        editorSegment.toTransformData = new List<TransformData>();

                        editorSegment.toTransformData.Add(new TransformData(serializedToTransformData.FindPropertyRelative("position").vector3Value,
                                                                            serializedToTransformData.FindPropertyRelative("offset").vector3Value,
                                                                            serializedToTransformData.FindPropertyRelative("scale").vector3Value));
                    }

                    editorSequence.Segments.Add(editorSegment);
                }
            }
        }

        /*
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
        */
        #endregion

        #region GUI

        private string progressBarName;

        public override void OnInspectorGUI()
        {
            GUILayout.Box(logo);
            GUILayout.Space(-20);
            GUILayout.Box(toolHandleReminder);

            GUI.enabled = !editorPlaybackRunning && !playingPauseAfterSegment && !EditorApplication.isPlaying;

            GUILayout.BeginVertical();

            GUILayout.Space(10);
            
            EditorGUILayout.PropertyField(serializedStartOnPlay);
            EditorGUILayout.PropertyField(serializedLoop);
            EditorGUILayout.EndVertical();

            


            EditorGUILayout.PropertyField(serializedTransforms, true);
            GUI.enabled = true;

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            EditorGUILayout.PropertyField(serializedSequenceName);

            GUILayout.Space(20);

            GUILayout.Label("START STATES", labelStyle);

            GUILayout.BeginHorizontal("Box");
            EditorGUILayout.LabelField(lastSelectedState == -1 ? "|>" : "", GUILayout.Width(20));

            GUI.enabled = !EditorApplication.isPlaying;
            if (GUILayout.Button(new GUIContent("Preview", "Previews start states")))
            {
                lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = -1;
                serializedObject.ApplyModifiedProperties();

                editorPlaybackRunning = false;
                playingPauseAfterSegment = false;
                ApplyFromDatastore(-1);
            }
            GUI.enabled = true;

            GUI.enabled = !editorPlaybackRunning && !EditorApplication.isPlaying && !playingPauseAfterSegment;
            if (GUILayout.Button(new GUIContent("Sample scene", "Samples positions, rotations and scales")))
            {
                lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = -1;
                SampleAllFromScene(i,-1);
            }
            GUI.enabled = true;

            GUILayout.EndHorizontal();

            GUILayout.Space(20);

            GUILayout.Label("SEGMENTS", labelStyle);
            //EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Space(5);

            if (!handlingUndoRedo)
            {
                for (int i = 0; i < serializedSequences.arraySize; i++)
                {



                    for (int j = 0; j < serializedSegments.arraySize; j++)
                    {
                        GUILayout.BeginHorizontal();
                        GUI.enabled = !EditorApplication.isPlaying;
                        if (GUILayout.Button(new GUIContent((j + 1).ToString() + " : Play", "Play from segment to end of sequence"), GUILayout.Width(90)))
                        {


                            CollectEditorSequences();

                            lastSelectedSequence = serializedObject.FindProperty
                            lastSelectedSegment = serializedObject.FindProperty("lastSelectedSegment").intValue = j;


                            serializedObject.ApplyModifiedProperties();

                            StartEditorPlayback(j - 1);
                        }
                        GUI.enabled = true;

                        if (editorPlaybackRunning && j == toIndex)
                        {
                            var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                            EditorGUI.ProgressBar(rect, lerpStep, progressBarName);

                        }
                        else if (playingPauseAfterSegment && j == toIndex)
                        {
                            var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                            EditorGUI.ProgressBar(rect, 0, string.Format("Pause: {0:#0.0}", (((startTime + pauseAfterSegmentDuration - EditorApplication.timeSinceStartup)))));
                        }
                        else
                        {
                            GUI.enabled = !EditorApplication.isPlaying && !editorPlaybackRunning && !playingPauseAfterSegment;
                            EditorGUILayout.PropertyField(serializedSegments.GetArrayElementAtIndex(j).FindPropertyRelative("Name"));
                            GUI.enabled = true;
                        }

                        GUILayout.EndHorizontal();

                        GUI.enabled = !editorPlaybackRunning && !playingPauseAfterSegment && !EditorApplication.isPlaying;
                        bool showEvents = EditorGUILayout.Foldout(serializedShowSegmentEvents.GetArrayElementAtIndex(j).boolValue, "Events", true);

                        if (showEvents != editorShowSegmentEvents[j])
                        {
                            serializedShowSegmentEvents.GetArrayElementAtIndex(j).boolValue = showEvents;
                            serializedObject.ApplyModifiedPropertiesWithoutUndo();
                        }

                        editorShowSegmentEvents[j] = showEvents;

                        if (editorShowSegmentEvents[j])
                        {
                            EditorGUILayout.PropertyField(serializedSegments.GetArrayElementAtIndex(j).FindPropertyRelative("OnLerpStart"));
                            EditorGUILayout.PropertyField(serializedSegments.GetArrayElementAtIndex(j).FindPropertyRelative("OnLerpEnd"));
                        }

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(serializedSegments.GetArrayElementAtIndex(j).FindPropertyRelative("duration"));
                        EditorGUILayout.PropertyField(serializedSegments.GetArrayElementAtIndex(j).FindPropertyRelative("pauseAfter"));
                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.PropertyField(serializedSegments.GetArrayElementAtIndex(j).FindPropertyRelative("curve"));

                        bool showRotation = EditorGUILayout.Foldout(serializedShowRotations.GetArrayElementAtIndex(j).boolValue, "RotationOffsets", true);

                        if (showRotation != editorShowRotationOffsets[j])
                        {
                            serializedShowRotations.GetArrayElementAtIndex(j).boolValue = showRotation;
                            serializedObject.ApplyModifiedPropertiesWithoutUndo();
                        }

                        editorShowRotationOffsets[j] = showRotation;

                        if (editorShowRotationOffsets[j])
                        {
                            for (int k = 0; k < editorTransforms.Count; k++)
                            {
                                GUILayout.BeginHorizontal("Box");

                                EditorGUILayout.LabelField(editorTransforms[k] != null ? editorTransforms[k].name : "*NULL*", GUILayout.Width(100));

                                EditorGUI.BeginChangeCheck();
                                EditorGUILayout.PropertyField(serializedSegments.GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(k).FindPropertyRelative("offset"));
                                if (EditorGUI.EndChangeCheck())
                                {
                                    CollectEditorSegments();
                                    ApplyFromDatastore(i,j);

                                    lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = j;
                                    serializedObject.ApplyModifiedProperties();
                                }

                                GUILayout.EndHorizontal();
                            }
                        }
                        GUI.enabled = true;



                        GUILayout.BeginHorizontal("Box");

                        if (!EditorApplication.isPlaying)
                            EditorGUILayout.LabelField(lastSelectedState == j ? "|>" : "", GUILayout.Width(20));

                        GUI.enabled = !EditorApplication.isPlaying;
                        if (GUILayout.Button(new GUIContent("Preview", "Previews destination states for this segment")))
                        {
                            lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = j;
                            editorPlaybackRunning = false;
                            playingPauseAfterSegment = false;
                            ApplyFromDatastore(i,j);
                        }
                        GUI.enabled = true;

                        GUI.enabled = !editorPlaybackRunning && !EditorApplication.isPlaying && !playingPauseAfterSegment;
                        if (GUILayout.Button(new GUIContent("Sample scene", "Samples positions and scales (NOT rotations) from scene into segment")))
                        {
                            lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = j;
                            SampleAllFromScene(i,j);
                        }
                        GUI.enabled = true;

                        GUILayout.EndHorizontal();
                        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

                        GUILayout.Space(10);
                    }


                    GUILayout.BeginHorizontal("Box");

                    GUI.enabled = !EditorApplication.isPlaying && !editorPlaybackRunning && !playingPauseAfterSegment;
                    if (GUILayout.Button("Add segment"))
                        AddSegment(i);

                    if (GUILayout.Button("Remove segment"))
                    {
                        if (serializedSequences.GetArrayElementAtIndex(i).FindPropertyRelative("Segments").arraySize < 1)
                            return;

                        RemoveSegment(i);
                    }
                    GUI.enabled = true;

                    GUILayout.EndHorizontal();
                    EditorGUILayout.HelpBox("Adding segment auto samples from scene", MessageType.Info);

                    EditorGUILayout.Space(20);
                }

                EditorGUILayout.PropertyField(serializedSequences);

                serializedObject.ApplyModifiedProperties();
            }
        }

        #endregion

        #region Data consistency

        /// <summary>
        /// Checks if user increased or decreased array size by adjusting number, or replaced or nulled element
        /// </summary>
        private int undoGroupOnChangeChecked;
        private void CheckForTransformsArrayChanged()
        {
            undoGroupOnChangeChecked = Undo.GetCurrentGroup();

            //If user adjusts array count
            if (editorTransforms.Count != serializedTransforms.arraySize)
            {
                if (serializedTransforms.arraySize > editorTransforms.Count)
                {
                    OnUserIncreasedTransformsArraySize();

                    Undo.CollapseUndoOperations(undoGroupOnChangeChecked - 1);
                }
                else
                {
                    OnUserDecreasedTransformsArraySize();

                    Undo.CollapseUndoOperations(undoGroupOnChangeChecked - 1);
                }
            }

            //Else if user changed existing transform element
            else
            {
                for (int i = 0; i < serializedTransforms.arraySize; i++)
                {
                    Transform serializedTransform = (Transform)serializedTransforms.GetArrayElementAtIndex(i).objectReferenceValue;

                    if (editorTransforms[i] != serializedTransform)
                    {
                        if (serializedTransform == null)
                        {
                            //User nulled reference on list element

                            editorTransforms[i] = null;

                            ResetDataForSingleTransform(i);

                            Undo.CollapseUndoOperations(undoGroupOnChangeChecked - 1);
                        }

                        else if (IsDuplicate(i, serializedTransform))
                        {
                            Debug.LogWarning("LerpAnimator: Duplicate transform detected. There should only be one reference for each. Nulling element");

                            editorTransforms[i] = null;
                            serializedTransforms.GetArrayElementAtIndex(i).objectReferenceValue = null;

                            serializedObject.ApplyModifiedProperties();


                            Undo.CollapseUndoOperations(undoGroupOnChangeChecked - 1);
                        }

                        else
                        {
                            //User inserted new transform reference on list element");

                            editorTransforms[i] = serializedTransform;

                            SampleSingleFromSceneToStartStatesAndSegments(i);

                            Undo.CollapseUndoOperations(undoGroupOnChangeChecked - 1);
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
        private void ResetDataForSingleTransform(int transformIndex)
        {
            for (int i = 0; i < serializedSequences.arraySize; i++)
            {
                
                SerializedProperty serializedStartStates = serializedSequences.GetArrayElementAtIndex(i).FindPropertyRelative("StartStates");


                serializedStartStates.GetArrayElementAtIndex(transformIndex).FindPropertyRelative("position").vector3Value =
                editorSequences[i].StartStates[transformIndex].position =

                serializedStartStates.GetArrayElementAtIndex(transformIndex).FindPropertyRelative("offset").vector3Value =
                editorSequences[i].StartStates[transformIndex].offset =

                serializedStartStates.GetArrayElementAtIndex(transformIndex).FindPropertyRelative("scale").vector3Value =
                editorSequences[i].StartStates[transformIndex].scale = Vector3.zero;

                SerializedProperty serializedSegments = serializedSequences.GetArrayElementAtIndex(i).FindPropertyRelative("Segments");

                for (int j = 0; j < serializedSegments.arraySize; j++)
                {
                    serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(transformIndex).FindPropertyRelative("position").vector3Value =
                        editorSequences[i].Segments[j].toTransformData[transformIndex].position =

                    serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(transformIndex).FindPropertyRelative("offset").vector3Value =
                        editorSequences[i].Segments[j].toTransformData[transformIndex].offset =

                    serializedSegments.GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").GetArrayElementAtIndex(transformIndex).FindPropertyRelative("scale").vector3Value =
                        editorSequences[i].Segments[j].toTransformData[transformIndex].scale = Vector3.zero;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Samples position and scale from single transform to start states and all segments
        /// </summary>
        /// <param name="transformIndex"></param>
        private void SampleSingleFromSceneToStartStatesAndSegments(int transformIndex)
        {
            //Depends on editorTransforms being updated before calling this function
            
            for (int i = 0; i > serializedSequences.arraySize; i++)
            {
                SerializedProperty startStates = serializedSequences.GetArrayElementAtIndex(i).FindPropertyRelative("StartStates");

                startStates.GetArrayElementAtIndex(transformIndex).FindPropertyRelative("position").vector3Value =
                    editorSequences[i].StartStates[transformIndex].position = editorTransforms[transformIndex].localPosition;

                startStates.GetArrayElementAtIndex(transformIndex).FindPropertyRelative("scale").vector3Value =
                    editorSequences[i].StartStates[transformIndex].scale = editorTransforms[transformIndex].localScale;

                for (int j = 0; j < editorSequences[i].Segments.Count; j++)
                {
                    SerializedProperty segment = serializedSequences.GetArrayElementAtIndex(i).FindPropertyRelative("Segments").GetArrayElementAtIndex(j);
                    SerializedProperty toTransformData = segment.GetArrayElementAtIndex(j).FindPropertyRelative("toTransformData");

                    toTransformData.GetArrayElementAtIndex(transformIndex).FindPropertyRelative("position").vector3Value =
                        editorSequences[i].Segments[j].toTransformData[transformIndex].position = editorTransforms[transformIndex].localPosition;

                    toTransformData.GetArrayElementAtIndex(transformIndex).FindPropertyRelative("scale").vector3Value =
                        editorSequences[i].Segments[j].toTransformData[transformIndex].scale = editorTransforms[transformIndex].localScale;
                }
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
                {
                    serializedTransforms.GetArrayElementAtIndex(i).objectReferenceValue = null;
                }
            }

            serializedObject.ApplyModifiedProperties();

            CollectEditorTransforms();

            AddDataForNewlyAddedTransforms(difference);
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

            //If it does, user set lower array count. Delete transforms and data from end of collections
            if (editorSegmentsContainsSameTransforms)
            {
                //User deleted element(s) from end of list.

                int difference = editorTransforms.Count - serializedTransforms.arraySize;

                for (int i = 0; i < difference; i++)
                {
                    for (int j = 0; j < serializedSequences.arraySize; j++)
                    {
                        if (editorSequences[j].StartStates.Count > 0)
                        {
                            editorTransforms.RemoveAt(editorTransforms.Count - 1);

                            //Delete from end of start states
                            editorSequences[j].StartStates.RemoveAt(editorSequences[j].StartStates.Count - 1);

                            serializedSequences.GetArrayElementAtIndex(j).FindPropertyRelative("StartStates").arraySize--;
                        }

                        for (int k = 0; k < editorSequences[j].Segments.Count; k++)
                        {
                            //Delete from end of transforms data in segments
                            editorSequences[j].Segments[k].toTransformData.RemoveAt(editorSequences[j].Segments[k].toTransformData.Count - 1);

                            serializedSequences.GetArrayElementAtIndex(j).FindPropertyRelative("Segments").GetArrayElementAtIndex(k).FindPropertyRelative("toTransformData").arraySize--;
                        }
                    }

                    serializedObject.ApplyModifiedProperties();
                }
            }

            //Else if user deleted element other than last index(es)
            else
            {
                //User deleted element in middle of list

                for (int i = 0; i < serializedTransforms.arraySize; i++)
                {
                    if ((Transform)serializedTransforms.GetArrayElementAtIndex(i).objectReferenceValue != editorTransforms[i])
                    {
                        editorTransforms.RemoveAt(i);

                        for (int j = 0; j < editorSequences.Count; j++)
                        {
                            editorSequences[j].StartStates.RemoveAt(i);

                            serializedSequences.GetArrayElementAtIndex(j).FindPropertyRelative("StartStates").DeleteArrayElementAtIndex(i);

                            SerializedProperty serializedSegments = serializedSequences.GetArrayElementAtIndex(j).FindPropertyRelative("Segments");

                            for (int k = 0; k < serializedSegments.arraySize; k++)
                            {
                                editorSequences[j].Segments[k].toTransformData.RemoveAt(i);
                                serializedSegments.GetArrayElementAtIndex(k).FindPropertyRelative("toTransformData").DeleteArrayElementAtIndex(i);
                            }
                        }

                        serializedObject.ApplyModifiedProperties();

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Adds Start State data and Segments data when user increased transforms count, either by dropping one transform onto array header or by manually increasing array size by any amount.
        /// NOTE! Necessary even if null elements added
        /// </summary>
        /// <param name="difference"></param>
        private void AddDataForNewlyAddedTransforms(int difference)
        {
            for (int i = 0; i < difference; i++)
            {

                for (int j = 0; j < serializedSequences.arraySize; j++)
                {
                    SerializedProperty startStates = serializedSequences.GetArrayElementAtIndex(j).FindPropertyRelative("StartStates");

                    startStates.arraySize++;

                    int newStartStatesCount = startStates.arraySize;

                    //StartStates
                    startStates.GetEndProperty().FindPropertyRelative("position").vector3Value = editorTransforms[newStartStatesCount - 1] == null ? Vector3.zero : editorTransforms[newStartStatesCount - 1].localPosition;
                    startStates.GetEndProperty().FindPropertyRelative("offset").vector3Value = editorTransforms[newStartStatesCount - 1] == null ? Vector3.zero : editorTransforms[newStartStatesCount - 1].localRotation.eulerAngles;
                    startStates.GetEndProperty().FindPropertyRelative("scale").vector3Value = editorTransforms[newStartStatesCount - 1] == null ? Vector3.zero : editorTransforms[newStartStatesCount - 1].localScale;

                    SerializedProperty segments = serializedSequences.GetArrayElementAtIndex(j).FindPropertyRelative("Segments");

                    //Segments
                    for (int k = 0; k < segments.arraySize; k++)
                    {
                        SerializedProperty segment = segments.GetArrayElementAtIndex(k);

                        segment.FindPropertyRelative("toTransformData").arraySize++;

                        int newToTransformDataCount = segment.FindPropertyRelative("toTransformData").arraySize;

                        SerializedProperty toTransformData = segment.FindPropertyRelative("toTransformData");

                        toTransformData.GetEndProperty().FindPropertyRelative("position").vector3Value = editorTransforms[newToTransformDataCount - 1] == null ? Vector3.zero : editorTransforms[newToTransformDataCount - 1].localPosition;
                        toTransformData.GetEndProperty().FindPropertyRelative("offset").vector3Value = Vector3.zero;
                        toTransformData.GetEndProperty().FindPropertyRelative("scale").vector3Value = editorTransforms[newToTransformDataCount - 1] == null ? Vector3.zero : editorTransforms[newToTransformDataCount - 1].localScale;
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();

            CollectEditorSequences();
        }

        #endregion

        #region User Operation

        private int undoGroupOnSegmentAdjusted;

        private void AddSegment(int sequenceIndex)
        {
            undoGroupOnSegmentAdjusted = Undo.GetCurrentGroup();

            serializedSequences.GetArrayElementAtIndex(sequenceIndex).FindPropertyRelative("Segments").arraySize++;

            //NOTE! Does this actually get the last element?
            SerializedProperty newSegment = serializedSequences.GetArrayElementAtIndex(sequenceIndex).FindPropertyRelative("Segments").GetEndProperty();

            //serializedSegments.arraySize++;

            for (int i = 0; i < serializedTransforms.arraySize; i++)
            {
                if (i > newSegment.FindPropertyRelative("toTransformData").arraySize - 1)
                    newSegment.FindPropertyRelative("toTransformData").arraySize++;

                //Remove rotation offsets data in case inherited from previous segment
                newSegment.FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("offset").vector3Value = Vector3.zero;
            }


            //Insert data for transforms allready in array
            int newSegmentIndex = serializedSequences.GetArrayElementAtIndex(sequenceIndex).FindPropertyRelative("Segments").arraySize - 1;

            lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = newSegmentIndex;

            newSegment.FindPropertyRelative("duration").floatValue = 1;
            newSegment.FindPropertyRelative("pauseAfter").floatValue = 0;
            newSegment.FindPropertyRelative("curve").animationCurveValue = AnimationCurve.Linear(0, 0, 1, 1);


            //CollectEditorSegments();


            newSegment.FindPropertyRelative("showRotationOffsets").boolValue = false;
            newSegment.FindPropertyRelative("showEvents").boolValue = false;

            //Sample to new segment from current scene
            SampleAllFromScene(sequenceIndex, newSegmentIndex);


            CollectEditorSequences();

            Undo.CollapseUndoOperations(undoGroupOnSegmentAdjusted);
        }

        private void RemoveSegment(int sequenceIndex)
        {
            undoGroupOnSegmentAdjusted = Undo.GetCurrentGroup();

            SerializedProperty serializedSegments = serializedSequences.GetArrayElementAtIndex(sequenceIndex).FindPropertyRelative("Segments");

            serializedSegments.arraySize--;

            //serializedSegments.arraySize--;
            serializedObject.ApplyModifiedProperties();

            //If last segment to select was the one we are about to delete, set to previous segment
            if (lastSelectedState == (sequenceIndex, editorSequences[sequenceIndex].Segments.Count -1 ))
            {
                ApplyFromDatastore(sequenceIndex, editorSequences[sequenceIndex].Segments.Count - 2);
                lastSelectedState  = (sequenceIndex, editorSequences[sequenceIndex].Segments.Count - 2);

                serializedSequences.FindPropertyRelative("lastSelectedSequence").intValue = sequenceIndex;
                serializedSequences.FindPropertyRelative("lastSelectedSegment").intValue = editorSequences[sequenceIndex].Segments.Count - 2;
            }

            editorSequences[sequenceIndex].Segments.RemoveAt(editorSequences[sequenceIndex].Segments.Count - 1);

            Undo.CollapseUndoOperations(undoGroupOnSegmentAdjusted);
        }

        public void ApplyFromDatastore(int sequenceIndex, int segmentIndex)
        {
            if (segmentIndex == -1)
            {
                SerializedProperty serializedStartStates = serializedSequences.GetArrayElementAtIndex(sequenceIndex).FindPropertyRelative("StartStates");

                for (int i = 0; i < editorTransforms.Count; i++)
                {
                    if (editorTransforms[i] != null)
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
                SerializedProperty serializedSegment = serializedSequences.GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("Segments").GetArrayElementAtIndex(segmentIndex);
                SerializedProperty serializedStartStates = serializedSequences.GetArrayElementAtIndex(segmentIndex).FindPropertyRelative("StartStates");

                for (int i = 0; i < editorTransforms.Count; i++)
                {
                    if (editorTransforms[i] != null)
                    {
                        editorTransforms[i].localPosition =
                        serializedSegment.FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value;

                        Quaternion acculumatedRotationOffsett = Quaternion.Euler(serializedStartStates.GetArrayElementAtIndex(i).FindPropertyRelative("offset").vector3Value);

                        for (int j = 0; j <= segmentIndex; j++)
                            acculumatedRotationOffsett *= Quaternion.Euler(serializedSegment.FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("offset").vector3Value);

                        editorTransforms[i].localRotation = acculumatedRotationOffsett;

                        editorTransforms[i].localScale =
                        serializedSegment.FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value;
                    }
                }
            }
        }

        /// <summary>
        /// Samples states from all transforms to start state or specific segment
        /// </summary>
        /// <param name="segmentIndex"></param>
        public void SampleAllFromScene(int sequenceIndex, int segmentIndex)
        {
            if (segmentIndex == -1)
            {
                SerializedProperty serializedStartStates = serializedSequences.GetArrayElementAtIndex(sequenceIndex).FindPropertyRelative("StartStates");

                for (int i = 0; i < editorTransforms.Count; i++)
                {
                    if (editorTransforms[i] != null)
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
            }

            else
            {
                SerializedProperty newSegment = serializedSequences.GetArrayElementAtIndex(sequenceIndex).GetEndProperty();

                for (int i = 0; i < editorTransforms.Count; i++)
                {
                    if (editorTransforms[i] != null)
                    {
                        newSegment.FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("position").vector3Value
                        = editorTransforms[i].transform.localPosition;


                        newSegment.FindPropertyRelative("toTransformData").GetArrayElementAtIndex(i).FindPropertyRelative("scale").vector3Value
                            = editorTransforms[i].transform.localScale;
                    }
                }

                serializedObject.ApplyModifiedProperties();
            }

            CollectEditorSequences();
        }

        #endregion

        #region Playback

        private double startTime;
        private float lerpStep;
        private int fromIndex;
        private int toIndex;
        private bool editorPlaybackRunning;
        private float pauseAfterSegmentDuration;

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

            progressBarName = serializedSegments.GetArrayElementAtIndex(toIndex).FindPropertyRelative("Name").stringValue;

            playingPauseAfterSegment = false;
            editorPlaybackRunning = true;
        }

        private List<Quaternion> interSegmentRotations = new List<Quaternion>();
        private readonly double lerpFrequency = 0.0166; //60 times per second
        private double nextLerpUpdate;
        private float reciprocal;
        private bool playingPauseAfterSegment;

        private void OnEditorUpdate()
        {
            if (editorPlaybackRunning && EditorApplication.timeSinceStartup > nextLerpUpdate)
            {
                nextLerpUpdate += lerpFrequency;
                Repaint();

                lerpStep = (((float)EditorApplication.timeSinceStartup - (float)startTime)) * reciprocal;

                if (fromIndex == -1)
                {
                    for (int i = 0; i < editorTransforms.Count; i++)
                    {
                        if (editorTransforms[i] != null)
                        {
                            if (editorStartStates[i].position != editorSegments[toIndex].toTransformData[i].position)
                            {
                                editorTransforms[i].localPosition =
                                    Vector3.LerpUnclamped(editorStartStates[i].position,
                                                 editorSegments[toIndex].toTransformData[i].position,
                                                 editorSegments[toIndex].curve.Evaluate(lerpStep));
                            }

                            if (editorSegments[toIndex].toTransformData[i].offset != Vector3.zero)
                            {
                                editorTransforms[i].localRotation = Quaternion.Euler(editorStartStates[i].offset) *
                                    Quaternion.Euler(Vector3.LerpUnclamped(Vector3.zero, editorSegments[toIndex].toTransformData[i].offset, editorSegments[toIndex].curve.Evaluate(lerpStep)));
                            }

                            if (editorStartStates[i].scale != editorSegments[toIndex].toTransformData[i].scale)
                            {
                                editorTransforms[i].localScale =
                                    Vector3.LerpUnclamped(editorStartStates[i].scale,
                                                 editorSegments[toIndex].toTransformData[i].scale,
                                                 editorSegments[toIndex].curve.Evaluate(lerpStep));
                            }
                        }
                    }
                }

                else
                {
                    for (int i = 0; i < editorTransforms.Count; i++)
                    {
                        if (editorTransforms[i] != null)
                        {
                            if (editorSegments[fromIndex].toTransformData[i].position != editorSegments[toIndex].toTransformData[i].position)
                            {
                                editorTransforms[i].localPosition =
                                    Vector3.LerpUnclamped(editorSegments[fromIndex].toTransformData[i].position,
                                                 editorSegments[toIndex].toTransformData[i].position,
                                                 editorSegments[toIndex].curve.Evaluate(lerpStep));
                            }

                            if (editorSegments[toIndex].toTransformData[i].offset != Vector3.zero)
                            {
                                editorTransforms[i].localRotation = interSegmentRotations[i] *
                                    Quaternion.Euler(Vector3.LerpUnclamped(Vector3.zero, editorSegments[toIndex].toTransformData[i].offset, editorSegments[toIndex].curve.Evaluate(lerpStep)));
                            }

                            if (editorSegments[fromIndex].toTransformData[i].scale != editorSegments[toIndex].toTransformData[i].scale)
                            {
                                editorTransforms[i].localScale =
                                    Vector3.LerpUnclamped(editorSegments[fromIndex].toTransformData[i].scale,
                                                 editorSegments[toIndex].toTransformData[i].scale,
                                                 editorSegments[toIndex].curve.Evaluate(lerpStep));
                            }
                        }
                    }
                }

                if (lerpStep > 1)
                {
                    //Pause after segment?
                    pauseAfterSegmentDuration = serializedSegments.GetArrayElementAtIndex(toIndex).FindPropertyRelative("pauseAfter").floatValue;

                    if (pauseAfterSegmentDuration > 0)
                    {
                        editorPlaybackRunning = false;
                        playingPauseAfterSegment = true;
                        startTime = (float)EditorApplication.timeSinceStartup;
                    }

                    else NextSegmentOrStop();
                }
            }

            if (playingPauseAfterSegment)
            {
                Repaint();

                if (EditorApplication.timeSinceStartup > startTime + pauseAfterSegmentDuration)
                {
                    playingPauseAfterSegment = false;
                    editorPlaybackRunning = true;

                    NextSegmentOrStop();
                }
            }

            //Handles UndoRedo
            if (handlingUndoRedo && EditorApplication.timeSinceStartup - delayedCollectTimerStart > delayAmount)
            {
                CollectEditorTransforms();
                //CollectEditorStartStates();
                //CollectEditorSegments();
                CollectEditorSequences();

                //CollectEditorShowRotations();
                //CollectEditorShowSegmentEvents();

                handlingUndoRedo = false;
                Repaint();

                lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue;

                if (lastSelectedState == -1 || (lastSelectedState > -1 && lastSelectedState <= serializedStartStates.arraySize))
                    ApplyFromDatastore(lastSelectedState);
            }

            //Handles periodic checks for when user makes changes in serializedTransforms array
            if (!handlingUndoRedo && 
                !editorPlaybackRunning &&
                !playingPauseAfterSegment &&
                !EditorApplication.isPlaying &&
                EditorApplication.timeSinceStartup > nextChangeCheck)
            {
                nextChangeCheck = EditorApplication.timeSinceStartup + 0.3f;

                CheckForTransformsArrayChanged();
            }
        }

        private void SampleInterSegmentRotations()
        {
            //Sample current rotations
            interSegmentRotations.Clear();

            foreach (Transform transform in editorTransforms)
                if (transform)
                    interSegmentRotations.Add(transform.localRotation);

                //We need something in the array to keep the number of elements correct
                else interSegmentRotations.Add(Quaternion.identity);
        }

        private void NextSegmentOrStop()
        {
            SampleInterSegmentRotations();

            //Was it the last segment?
            if (toIndex + 1 > editorSegments.Count - 1)
            {
                if (serializedLoop.boolValue == true)
                {
                    lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = 0;
                    StartEditorPlayback(-1);
                }

                else editorPlaybackRunning = false;
            }

            else
            {
                fromIndex = fromIndex == -1 ? 0 : ++fromIndex;
                toIndex++;

                reciprocal = 1 / editorSegments[toIndex].duration;

                startTime = (float)EditorApplication.timeSinceStartup;

                lastSelectedState = serializedObject.FindProperty("lastSelectedState").intValue = toIndex;

                progressBarName = serializedSegments.GetArrayElementAtIndex(toIndex).FindPropertyRelative("Name").stringValue;
            }
        }

        #endregion
    }
}
