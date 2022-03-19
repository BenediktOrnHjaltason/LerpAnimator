using System.Collections.Generic;
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
        private SineAnimator.AnimationMode editorAnimationMode;

        private SineAnimator.AnimationMode previousAnimationMode = SineAnimator.AnimationMode.PositionBobber;

        private SerializedProperty serializedValueMode;
        private SineAnimator.ValueMode editorValueMode;

        private SerializedProperty serializedRadius;
        private float editorRadius;

        private SerializedProperty serializedFrequency;
        private float editorFrequency;

        private SerializedProperty serializedAmplitude;
        private float editorAmplitude;

        private SerializedProperty serializedRingSpin;
        private float editorRingSpin;

        private SerializedProperty serializedRingUniformMovemement;
        private bool editorRingUniformMovement;

        private SerializedProperty serializedWallWidth;

        private SerializedProperty serializedObjectToSpawn;
        private SerializedProperty serializedNumberOfObjectsToSpawn;

        private Transform parentTransform;

        private UnityEvent currentAnimationFunction = new UnityEvent();

        private SerializedProperty serializedShowGenerateObjects;
        private bool editorShowGenerateObjects;




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
            editorAnimationMode = previousAnimationMode = (SineAnimator.AnimationMode)serializedAnimationMode.intValue;

            serializedValueMode = serializedObject.FindProperty("valueMode");
            editorValueMode = (SineAnimator.ValueMode)serializedValueMode.intValue;

            serializedRadius = serializedObject.FindProperty("radius");
            editorRadius = serializedRadius.floatValue;

            serializedFrequency = serializedObject.FindProperty("frequency");
            editorFrequency = serializedFrequency.floatValue;

            serializedAmplitude = serializedObject.FindProperty("amplitude");
            editorAmplitude = serializedAmplitude.floatValue;

            serializedRingSpin = serializedObject.FindProperty("ringSpin");
            editorRingSpin = serializedRingSpin.floatValue;

            serializedRingUniformMovemement = serializedObject.FindProperty("ringUniformMovement");
            editorRingUniformMovement = serializedRingUniformMovemement.boolValue;

            serializedWallWidth = serializedObject.FindProperty("wallWidth");


            serializedObjectToSpawn = serializedObject.FindProperty("objectToSpawn");
            serializedNumberOfObjectsToSpawn = serializedObject.FindProperty("numberOfObjectsToSpawn");

            serializedShowGenerateObjects = serializedObject.FindProperty("showGenerateObjects");



            EditorApplication.update += OnEditorUpdate;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;

            editorTransforms = new List<Transform>();

            parentTransform = ((SineAnimator)target).gameObject.transform;

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

        private bool showGenerateObjects = false;

        public override void OnInspectorGUI()
        {
            GUILayout.Box(logo);

            GUILayout.BeginVertical();

            GUILayout.Space(10);

            GUI.enabled = !EditorApplication.isPlaying && !handlingUndoRedo;

            EditorGUILayout.PropertyField(serializedStartOnPlay);

            EditorGUILayout.EndVertical();


            GUILayout.Space(20);

            showGenerateObjects = EditorGUILayout.Foldout(serializedShowGenerateObjects.boolValue, "Generate objects", true);

            if (showGenerateObjects != editorShowGenerateObjects)
            {
                serializedShowGenerateObjects.boolValue = showGenerateObjects;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            editorShowGenerateObjects = showGenerateObjects;

            if (showGenerateObjects)
            {
                EditorGUILayout.PropertyField(serializedObjectToSpawn);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(serializedNumberOfObjectsToSpawn);
                if (GUILayout.Button("Generate"))
                    GenerateAndAddTransforms();

                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(20);

             EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(serializedTransforms, true);

            if (EditorGUI.EndChangeCheck())
            {
                if (editorAnimationMode == SineAnimator.AnimationMode.PositionBobber)
                {
                    CollectOriginalPositions();
                }

                if (editorAnimationMode == SineAnimator.AnimationMode.RingPlane || editorAnimationMode == SineAnimator.AnimationMode.RingCarousel)
                {
                    CalculateDegreesDelta();
                }
            }

            GUILayout.Space(20);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedAnimationMode);
            if (EditorGUI.EndChangeCheck())
            {
                editorAnimationMode = (SineAnimator.AnimationMode)serializedAnimationMode.intValue;
                SetAnimationFunction();
                ((SineAnimator)target).SetAnimationFunction();

                if (previousAnimationMode == SineAnimator.AnimationMode.ScaleBobber)
                    ApplyOriginalScales();

                previousAnimationMode = editorAnimationMode;
            }


            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedValueMode);
            if (EditorGUI.EndChangeCheck())
            {
                editorValueMode = (SineAnimator.ValueMode)serializedValueMode.intValue;
            }

            GUILayout.Space(20);

            editorFrequency = serializedFrequency.floatValue = EditorGUILayout.Slider("Frequency", serializedFrequency.floatValue, 0, 30);
            GUILayout.Space(20);

            if (editorAnimationMode == SineAnimator.AnimationMode.ScaleBobber)
                editorAmplitude = serializedAmplitude.floatValue = EditorGUILayout.Slider("Amplitude", serializedAmplitude.floatValue, 0, 1);


            else if (editorAnimationMode == SineAnimator.AnimationMode.RingPlane || editorAnimationMode == SineAnimator.AnimationMode.RingCarousel)
                editorAmplitude = serializedAmplitude.floatValue = EditorGUILayout.Slider("Amplitude", serializedAmplitude.floatValue, 0.01f, 2000);

            else
                editorAmplitude = serializedAmplitude.floatValue = EditorGUILayout.Slider("Amplitude", serializedAmplitude.floatValue, 0.01f, 200);


            GUILayout.Space(20);

            if (editorAnimationMode == SineAnimator.AnimationMode.RingPlane || editorAnimationMode == SineAnimator.AnimationMode.RingCarousel)
                editorRadius = serializedRadius.floatValue = EditorGUILayout.Slider("Radius", serializedRadius.floatValue, 0, 600);

            GUILayout.Space(20);

            if (editorAnimationMode == SineAnimator.AnimationMode.RingPlane || editorAnimationMode == SineAnimator.AnimationMode.RingCarousel) 
                editorRingSpin = serializedRingSpin.floatValue = EditorGUILayout.Slider("Ring spin", serializedRingSpin.floatValue, -500, 500);

            else if (editorAnimationMode == SineAnimator.AnimationMode.Wall)
            {
                EditorGUI.BeginChangeCheck();
                serializedWallWidth.floatValue = EditorGUILayout.Slider("Wall width", serializedWallWidth.floatValue, 0.1f, 100);

                if (EditorGUI.EndChangeCheck())
                    CalculateWallDistanceDelta();
            }

            if (editorAnimationMode == SineAnimator.AnimationMode.RingPlane || editorAnimationMode == SineAnimator.AnimationMode.RingCarousel)
            {
                GUILayout.Space(20);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Objects face");


                if (GUILayout.Button("Outward"))
                    RingObjectsFaceDirection(SineAnimator.RingObjectsFace.Outward);

                if (GUILayout.Button("Inward"))
                    RingObjectsFaceDirection(SineAnimator.RingObjectsFace.Inward);

                EditorGUILayout.EndHorizontal();

                GUILayout.Space(20);

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(serializedRingUniformMovemement);

                if (EditorGUI.EndChangeCheck())
                {
                    editorRingUniformMovement = serializedRingUniformMovemement.boolValue;
                    CalculateDegreesDelta();
                }
            }

            

            GUILayout.Space(20);

            if (!editorPlaybackRunning && GUILayout.Button("Preview"))
            {
                StartEditorPlayback();
            }
            else if (editorPlaybackRunning && GUILayout.Button("Stop"))
            {
                StopEditorPlayback();
            }


            GUILayout.Space(20);

            GUI.enabled = true;

            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        private void GenerateAndAddTransforms()
        {
            for (int i = 0; i < serializedNumberOfObjectsToSpawn.intValue; i++)
            {
                GameObject temp = (GameObject)Instantiate(serializedObjectToSpawn.objectReferenceValue, parentTransform.position, parentTransform.rotation);

                serializedTransforms.arraySize++;
                serializedTransforms.GetArrayElementAtIndex(serializedTransforms.arraySize - 1).objectReferenceValue = temp;

                temp.transform.parent = parentTransform;
                
            }

            serializedObject.ApplyModifiedProperties();

            SetAnimationFunction();
        }

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

                SetAnimationFunction();
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

        private void SetAnimationFunction()
        {
            currentAnimationFunction.RemoveAllListeners();

            switch (serializedAnimationMode.enumValueIndex)
            {
                case 0:     //PositionLerp
                    CollectOriginalPositions();
                    currentAnimationFunction.AddListener(PositionBobber);
                    break;

                case 1:     //ScaleLerp
                    CollectScales();
                    currentAnimationFunction.AddListener(ScaleBobber);
                    break;

                case 2:     //RingPlane
                    CalculateDegreesDelta();
                    currentAnimationFunction.AddListener(RingPlane);
                    break;
                case 3: //RingCarousel
                    CalculateDegreesDelta();
                    currentAnimationFunction.AddListener(RingCarousel);
                    break;

                case 4:     //WallOfMotion
                    CalculateWallDistanceDelta();
                    currentAnimationFunction.AddListener(Wall);
                    break;
            }
        }

        private List<Vector3> originalPositions = new List<Vector3>();


        private void PositionBobber()
        {
            for(int i = 0; i < editorTransforms.Count; i++)
            {
                if (editorTransforms[i] == null)
                    continue;

                    editorTransforms[i].position = 
                    editorValueMode == SineAnimator.ValueMode.Value ?
                    originalPositions[i] - editorTransforms[i].forward * Mathf.Sin((float)EditorApplication.timeSinceStartup * editorFrequency) * editorAmplitude :
                    originalPositions[i] - editorTransforms[i].forward * Mathf.Abs(Mathf.Sin((float)EditorApplication.timeSinceStartup * editorFrequency)) * editorAmplitude;
            }
        }

        private void CollectOriginalPositions()
        {
            originalPositions.Clear();

            foreach (Transform tr in editorTransforms)
                originalPositions.Add(tr.position);
        }

        private readonly List<Vector3> doubleScales = new List<Vector3>();
        private readonly List<Vector3> originalScales = new List<Vector3>();
        private void ScaleBobber()
        {
            for (int i = 0; i < editorTransforms.Count; i++)
            {
                if (editorTransforms[i] == null)
                    continue;

                    editorTransforms[i].localScale = 
                    editorValueMode == SineAnimator.ValueMode.Value ? 
                    Vector3.LerpUnclamped(originalScales[i], doubleScales[i], Mathf.Sin((float)EditorApplication.timeSinceStartup * editorFrequency) * editorAmplitude) :
                    Vector3.LerpUnclamped(originalScales[i], doubleScales[i], Mathf.Abs(Mathf.Sin((float)EditorApplication.timeSinceStartup * editorFrequency)) * editorAmplitude);
            }
        }

        private void CollectScales()
        {
            originalScales.Clear();
            doubleScales.Clear();

            foreach (Transform tr in editorTransforms)
            {
                originalScales.Add(tr.localScale);
                doubleScales.Add(tr.localScale * 2);
            }
        }

        private void ApplyOriginalScales()
        {
            if (originalScales.Count != editorTransforms.Count)
                return;

            for (int i = 0; i < editorTransforms.Count; i++)
            {
                if (editorTransforms[i] == null) 
                    continue;

                editorTransforms[i].localScale = originalScales[i];
            }
        }


        private Quaternion rot;
        private Vector3 directionBasePoint;

        /// <summary>
        /// Used to place the objects around the center
        /// </summary>
        private float degreesDelta;

        /// <summary>
        /// Used to move objects around with sine wave
        /// </summary>
        private float radiansDelta;

        //private Vector3 direction;

        float previousTime;

        /// <summary>
        /// Calculates the data neccessary to place objects on sine wave
        /// </summary>
        private void CalculateDegreesDelta()
        {
            if (editorTransforms.Count < 1)
                return;

            degreesDelta = 360.0f / editorTransforms.Count;
            radiansDelta = editorRingUniformMovement ? 0 : (Mathf.PI * 2) / editorTransforms.Count;

            CalculateRingDistribution();
        }

        List<Vector3> directions = new List<Vector3>();

        /// <summary>
        /// Calculates the data neccessary to place objects around the ring
        /// </summary>
        private void CalculateRingDistribution()
        {
            directions.Clear();
            
            for (int i = 0; i < editorTransforms.Count; i++)
            {
                rot = parentTransform.localRotation * Quaternion.Euler(0, 0, degreesDelta * (i + 1));
                directionBasePoint = (parentTransform.position + (rot * (Vector3.right) * 0.01f));

                directions.Add(parentTransform.InverseTransformDirection(directionBasePoint - parentTransform.position));
            }
        }

        private void RingPlane()
        {
            for (int i = 0; i < editorTransforms.Count; i++)
            {
                if (editorTransforms[i] == null)
                    continue;

                editorTransforms[i].localPosition =
                    (directions[i] * editorRadius) + 
                    ((editorValueMode == SineAnimator.ValueMode.Value) ?
                    (directions[i] * ((((Mathf.Sin(((float)EditorApplication.timeSinceStartup + (radiansDelta * i)) * editorFrequency) + 1) / 2) * editorAmplitude ))) :
                    (directions[i] * (Mathf.Abs((Mathf.Sin(((float)EditorApplication.timeSinceStartup + (radiansDelta * i)) * editorFrequency) * editorAmplitude))))); 
            }        

            if (editorRingSpin != 0)
            {
                parentTransform.Rotate(parentTransform.forward, editorRingSpin * ((float)EditorApplication.timeSinceStartup - previousTime), Space.World);

                previousTime = (float)EditorApplication.timeSinceStartup;
            }
        }

        private void RingCarousel()
        {
            for (int i = 0; i < editorTransforms.Count; i++)
            {
                if (editorTransforms[i] == null)
                    continue;

                editorTransforms[i].localPosition =
                (directions[i] * editorRadius) + 
                ((editorValueMode == SineAnimator.ValueMode.Value) ?
                (parentTransform.InverseTransformDirection(parentTransform.forward) * 0.01f * ((((Mathf.Sin(((float)EditorApplication.timeSinceStartup + (radiansDelta * i)) * editorFrequency) + 1) / 2) * editorAmplitude))) :
                (parentTransform.InverseTransformDirection(parentTransform.forward) * 0.01f * (Mathf.Abs((Mathf.Sin(((float)EditorApplication.timeSinceStartup + (radiansDelta * i)) * editorFrequency) * editorAmplitude)))));
            }

            if (editorRingSpin != 0)
            {
                parentTransform.Rotate(parentTransform.forward, editorRingSpin * ((float)EditorApplication.timeSinceStartup - previousTime), Space.World);

                previousTime = (float)EditorApplication.timeSinceStartup;
            }
        }

        private void RingObjectsFaceDirection(SineAnimator.RingObjectsFace lookDirection)
        {
            for (int i = 0; i < editorTransforms.Count; i++)
            {
                if (lookDirection == SineAnimator.RingObjectsFace.Outward)
                    editorTransforms[i].rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(editorTransforms[i].transform.position - parentTransform.position, parentTransform.forward), parentTransform.forward);

                else if (lookDirection == SineAnimator.RingObjectsFace.Inward)
                    editorTransforms[i].rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(parentTransform.position - editorTransforms[i].transform.position, parentTransform.forward), parentTransform.forward);
            }
        }

        private float wallDistanceDelta;
        private float halfDistance;
        private void Wall()
        {
            for (int i = 0; i < editorTransforms.Count; i++)
            {
                if (editorTransforms[i] == null)
                    continue;

                editorTransforms[i].position = parentTransform.position -
                (parentTransform.right * halfDistance) +
                (parentTransform.right * wallDistanceDelta * i) +
                ((editorValueMode == SineAnimator.ValueMode.Value) ?
                (parentTransform.up * (Mathf.Sin(((float)EditorApplication.timeSinceStartup + (radiansDelta * i)) * editorFrequency) * editorAmplitude)) :
                (parentTransform.up * (Mathf.Abs(Mathf.Sin(((float)EditorApplication.timeSinceStartup + (radiansDelta * i)) * editorFrequency)) * editorAmplitude)));
            }
        }

        private void CalculateWallDistanceDelta()
        {
            wallDistanceDelta = serializedWallWidth.floatValue / editorTransforms.Count;
            halfDistance = serializedWallWidth.floatValue / 2;

            CalculateDegreesDelta();
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

            if (editorAnimationMode == SineAnimator.AnimationMode.ScaleBobber)
                ApplyOriginalScales();
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