﻿using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Events;

namespace SpheroidGames.SineAnimator
{
    [CustomEditor(typeof(SineAnimator))]
    public class SineAnimatorEditor : Editor
    {
        Texture logo;

        /// <summary>
        /// transforms references used to playback animation during edit mode. Saving a copy to avoid having find properties on serialized object in OnEditorUpdate() while animating
        /// </summary>
        private List<Transform> editorTransforms;


        //Properties for accessing parts of serializedObject

        /// <summary>
        /// whether animation should start when play in scene starts
        /// </summary>
        private SerializedProperty serializedStartOnPlay;

        /// <summary>
        /// whether sequence will loop in game play mode
        /// </summary>

        private SerializedProperty serializedTransforms;

        private SerializedProperty serializedAnimationMode;

        private SerializedProperty serializedValueMode;

        private SerializedProperty serializedRadius;
        private float editorRadius;

        private SerializedProperty serializedSpeed;
        private float editorSpeed;

        private SerializedProperty serializedAmplitude;
        private float editorAmplitude;

        private SerializedProperty serializedRingSpin;
        private float editorRingSpin;

        private Transform targetTransform;

        private UnityEvent currentAnimationFunction = new UnityEvent();


        /// <summary>
        /// Used for managing periodic checks for changes in serializedTransforms array
        /// </summary>
        private double nextChangeCheck;

        #region Events

        private void OnEnable()
        {
            logo = (Texture)AssetDatabase.LoadAssetAtPath("Assets/SINE Animator/Textures/T_SineAnimatorLogo.png", typeof(Texture));

            serializedStartOnPlay = serializedObject.FindProperty("StartOnPlay");

            serializedTransforms = serializedObject.FindProperty("TransformsToActOn");

            serializedAnimationMode = serializedObject.FindProperty("animationMode");
            serializedValueMode = serializedObject.FindProperty("valueMode");

            serializedRadius = serializedObject.FindProperty("radius");
            editorRadius = serializedRadius.floatValue;

            serializedSpeed = serializedObject.FindProperty("speed");
            editorSpeed = serializedSpeed.floatValue;

            serializedAmplitude = serializedObject.FindProperty("amplitude");
            editorAmplitude = serializedAmplitude.floatValue;

            serializedRingSpin = serializedObject.FindProperty("ringSpin");
            editorRingSpin = serializedRingSpin.floatValue;


            EditorApplication.update += OnEditorUpdate;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;

            editorTransforms = new List<Transform>();

            targetTransform = ((SineAnimator)target).gameObject.transform;

            CollectEditorTransforms();

            nextChangeCheck = EditorApplication.timeSinceStartup + 0.5f;

            SetAnimationFunction();
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

            if (editorPlaybackRunning)
                editorPlaybackRunning = false;

            delayedCollectTimerStart = (float)EditorApplication.timeSinceStartup;
        }

        private void SetAnimationFunction()
        {
            currentAnimationFunction.RemoveAllListeners();

            switch(serializedAnimationMode.enumValueIndex)
            {
                case 0:     //PositionLerp
                    break;

                case 1:     //ScaleLerp
                    break;

                case 2:     //RingOfMotion
                    CalculateDegreesDelta();
                    currentAnimationFunction.AddListener(RingOfMotion);
                    break;

                case 3:     //WallOfMotion
                    break;
            }
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

        #endregion

        #region GUI

        public override void OnInspectorGUI()
        {
            GUILayout.Box(logo);

            GUI.enabled = !editorPlaybackRunning && !EditorApplication.isPlaying;

            GUILayout.BeginVertical();

            GUILayout.Space(10);
            EditorGUILayout.PropertyField(serializedStartOnPlay);
            EditorGUILayout.PropertyField(serializedAnimationMode);
            EditorGUILayout.PropertyField(serializedValueMode);

            EditorGUILayout.EndVertical();

            GUI.enabled = true;

            GUILayout.Space(20);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedTransforms, true);

            if (EditorGUI.EndChangeCheck())
                CalculateDegreesDelta();

            GUILayout.Space(20);

            editorRadius = serializedRadius.floatValue = EditorGUILayout.Slider("Radius", serializedRadius.floatValue, 1, 400);
            GUILayout.Space(20);

            editorSpeed = serializedSpeed.floatValue = EditorGUILayout.Slider("Speed", serializedSpeed.floatValue, 1, 20);
            GUILayout.Space(20);

            editorAmplitude = serializedAmplitude.floatValue = EditorGUILayout.Slider("Amplitude", serializedAmplitude.floatValue, 1, 200);
            GUILayout.Space(20);

            editorRingSpin = serializedRingSpin.floatValue = EditorGUILayout.Slider("Ring spin", serializedRingSpin.floatValue, -10, 10);
            GUILayout.Space(20);


            GUI.enabled = !handlingUndoRedo;
            if (!editorPlaybackRunning && GUILayout.Button("Preview animation"))
            {
                StartEditorPlayback();
            }
            else if (editorPlaybackRunning && GUILayout.Button("Stop animation"))
            {
                StopEditorPlayback();
            }
            GUI.enabled = true;

            

            serializedObject.ApplyModifiedProperties();
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

                for (int j = 0; j < difference; j++)
                {
                    if (editorTransforms.Count > 0)
                    {
                        editorTransforms.RemoveAt(editorTransforms.Count - 1);
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

                        serializedObject.ApplyModifiedProperties();

                        break;
                    }
                }
            }
        }

        #endregion

        #region Animation Functions

        private Quaternion rot;
        private Vector3 basePoint;

        /// <summary>
        /// Used to place the objects around the center
        /// </summary>
        private float degreesDelta;

        /// <summary>
        /// Used to move objects around with sine wave
        /// </summary>
        private float radiansDelta;

        private Vector3 direction;

        private void RingOfMotion()
        {
            for (int i = 0; i < editorTransforms.Count; i++)
            {
                //Find the new rotation
                rot = targetTransform.rotation * Quaternion.Euler(0, 0, degreesDelta * (i + 1));
                basePoint = (targetTransform.position + (rot * (Vector3.right) * 0.01f));
                direction = (basePoint - targetTransform.position);

                editorTransforms[i].position = basePoint + (direction * editorRadius) + (direction * ((((Mathf.Sin(((float)EditorApplication.timeSinceStartup + (radiansDelta * i)) * editorSpeed) + 1) / 2) * editorAmplitude )));
            }        

            if (editorRingSpin != 0)
            {
                targetTransform.Rotate(targetTransform.forward, editorRingSpin);
            }
        }

        private void CalculateDegreesDelta()
        {
            degreesDelta = 360 / editorTransforms.Count;
            radiansDelta = (Mathf.PI * 2) / editorTransforms.Count; 
        }

        #endregion


        #region Playback

        private bool editorPlaybackRunning;
        private double startTime;


        public void StartEditorPlayback()
        {

            if (editorTransforms.Count == 0)
                return;

            startTime = (float)EditorApplication.timeSinceStartup;

            nextAnimationUpdate = startTime + animationFrequency;

            SetAnimationFunction();

            editorPlaybackRunning = true;
        }

        private void StopEditorPlayback()
        {
            editorPlaybackRunning = false;
        }

        private readonly double animationFrequency = 0.0166; //60 times per second
        private double nextAnimationUpdate;

        private void OnEditorUpdate()
        {
            if (editorPlaybackRunning && EditorApplication.timeSinceStartup > nextAnimationUpdate)
            {
                nextAnimationUpdate += animationFrequency;
                Repaint();

                currentAnimationFunction?.Invoke();
            }


            //Handles UndoRedo
            if (handlingUndoRedo && EditorApplication.timeSinceStartup - delayedCollectTimerStart > delayAmount)
            {
                CollectEditorTransforms();

                handlingUndoRedo = false;
                Repaint();
            }

            //Handles periodic checks for when user makes changes in serializedTransforms array
            if (!handlingUndoRedo && 
                !editorPlaybackRunning &&
                !EditorApplication.isPlaying &&
                EditorApplication.timeSinceStartup > nextChangeCheck)
            {
                nextChangeCheck = EditorApplication.timeSinceStartup + 0.3f;

                CheckForTransformsArrayChanged();
            }

        }

        #endregion
    }
}


//editorTransforms[i].position = basePoint + ((basePoint - targetTransform.position) * (((Mathf.Sin(((float)EditorApplication.timeSinceStartup + (radiansDelta * i)) ) + 1 ) / 2) * editorSpeed) * editorRadius); 