using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;



[CustomEditor(typeof(LerpAnimator))]
public class LerpAnimatorEditor : Editor
{
    LerpAnimator animator;

    static List<Transform> previousTransforms;

    int previousTransformsArrayCount;

    private void OnEnable()
    {
        animator = (LerpAnimator)target;

        EditorApplication.update += OnEditorUpdate;
        EditorApplication.hierarchyChanged += OnHierarchyChanged;

        RemoveInvalidReferencesAndTheirData();

        CollectTransformsReferences();
    }

    private void OnDisable()
    {
        //ApplyStartState();
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;

    }

    private void CollectTransformsReferences()
    {
        previousTransformsArrayCount = serializedObject.FindProperty("TransformsToActOn").arraySize;

        previousTransforms = new List<Transform>();

        for (int i = 0; i < previousTransformsArrayCount; i++)
        {
            Transform transform = serializedObject.FindProperty("TransformsToActOn").GetArrayElementAtIndex(i).objectReferenceValue as Transform;

            if (transform != null) previousTransforms.Add(transform);
        }

        foreach (Transform transform in previousTransforms)
        {
            if (transform != null) Debug.Log("previousTransforms contains valid transform reference to object" + transform.gameObject.name);
        }
    }


    private void OnHierarchyChanged()
    {
        Debug.Log("OnHierarchyChanged");
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
        if (GUILayout.Button("Sample"))
        {
            animator.SampleFromScene(-1);
        }

        if (GUILayout.Button("Preview"))
        {
           animator.ApplyFromDatastore(-1);
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
                if (GUILayout.Button("Sample"))
                {
                    Debug.Log("You pressed sample on segment" + i);
                }

                if (GUILayout.Button("Preview"))
                {
                    Debug.Log("You pressed Preview on segment " + i);
                    animator.ApplyFromDatastore(i);
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
        if (!OnGUIChangedCalled && currentTransformsArraySize != previousTransformsArrayCount)
        {
            Debug.Log("Detected user deleted array element");
            //detectedUserDeletedArrayElement = true;

            previousTransformsArrayCount = serializedObject.FindProperty("TransformsToActOn").arraySize;
        }
    }
    #endregion

    #region Data consistency
    private void OnGUIChanged()
    {
        Debug.Log("OnGUIChanged");

        //If user adjusts array count
        int currentTransformsArrayCount = serializedObject.FindProperty("TransformsToActOn").arraySize;

        if (previousTransforms.Count != currentTransformsArrayCount)
        {
            if (currentTransformsArrayCount > previousTransforms.Count)
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

        previousTransformsArrayCount = serializedObject.FindProperty("TransformsToActOn").arraySize;
    }

    

    private void RemoveInvalidReferencesAndTheirData()
    {
        if (animator)
        {
            int numberOfTransformsToActOn = serializedObject.FindProperty("TransformsToActOn").arraySize;
            List<int> deletedTransformsIndexes = new List<int>();

            for (int i = numberOfTransformsToActOn - 1; i >= 0; i--)
            {

                if (serializedObject.FindProperty("TransformsToActOn").GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    Debug.Log("Found missing or null object reference");
                    deletedTransformsIndexes.Add(i);
                }
            }

            foreach (int index in deletedTransformsIndexes)
            {
                Debug.Log("Attempting to delete array element");
                //serializedObject.FindProperty("TransformsToActOn").objectReferenceValue = null;
                //serializedObject.ApplyModifiedProperties();

                int transformsArrayCountBefore = serializedObject.FindProperty("TransformsToActOn").arraySize;

                serializedObject.FindProperty("TransformsToActOn").DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();

                int transformsArrayCountAfter = serializedObject.FindProperty("TransformsToActOn").arraySize;

                //If the property mas missing, delete again to ensure array element removed
                if (transformsArrayCountBefore == transformsArrayCountAfter)
                {
                    serializedObject.FindProperty("TransformsToActOn").DeleteArrayElementAtIndex(index);
                    serializedObject.ApplyModifiedProperties();
                }

                serializedObject.Update();


                //serializedObject.FindProperty("StartStates").DeleteArrayElementAtIndex(index);
            }


            /*
            int numberOfSegments = serializedObject.FindProperty("Segments").arraySize;

            for (int i = 0; i < numberOfSegments; i++)
            {
                foreach (int index in deletedTransformsIndexes)
                {
                    serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i).FindPropertyRelative("toTransformData").DeleteArrayElementAtIndex(index);
                }
            }
            */

            //serializedObject.ApplyModifiedProperties();

            CollectTransformsReferences();

        }
        else Debug.Log("LerpAnimatorEditor::OnHierarchyChanged: animator reference is not valid");
    }

    private void OnUserIncreasedTransformsArrayCount()
    {
        int numberOElements = serializedObject.FindProperty("TransformsToActOn").arraySize;

        for (int i = numberOElements -1; i > 0; i--)
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
    }

    private void OnUserDecreasedTransformsArrayCount()
    {
        CollectTransformsReferences();

        Debug.Log("transforms count after user decreased array size: " + previousTransforms.Count);
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
        animator.ApplyFromDatastore(index);
    }

    double startTime;
    double step;
    private void StartSequence()
    {
        if (animator.Segments.Count == 0) return;

        startTime = EditorApplication.timeSinceStartup;
    }
    /*
    private bool running = false;

    int fromIndex = 0;
    int toIndex = 0;
    public void RunSequence(int pFromIndex = -1)
    {
        if (animator.Segments.Count == 0) return;

        startTime = EditorApplication.timeSinceStartup;

        //If Starting from start states
        if (pFromIndex == -1)
        {
            fromIndex = 0; toIndex = 0;

            animator.Segments[0].OnSegmentStart?.Invoke();

            while (CalculatingInterpolationStep(startTime, animator.Segments[0].duration, out step))
            {
                for (int i = 0; i < animator.TransformsToActOn.Count; i++)
                {
                    animator.TransformsToActOn[i].localPosition =
                        Vector3.LerpUnclamped(animator.startStates[animator.TransformsToActOn[i]].position, animator.Segments[0].toTransformData[animator.TransformsToActOn[i]].position,
                        animator.Segments[0].curve.Evaluate((float)step));

                    animator.TransformsToActOn[i].localRotation =
                        Quaternion.Euler(Vector3.LerpUnclamped(animator.startStates[animator.TransformsToActOn[i]].rotation, animator.Segments[0].toTransformData[animator.TransformsToActOn[i]].rotation,
                        animator.Segments[0].curve.Evaluate((float)step)));

                    animator.TransformsToActOn[i].localScale =
                        Vector3.LerpUnclamped(animator.startStates[animator.TransformsToActOn[i]].scale, animator.Segments[0].toTransformData[animator.TransformsToActOn[i]].scale,
                        animator.Segments[0].curve.Evaluate((float)step));
                }

                yield return null;
            }
        }
        //If starting from any sequence
        else
        {
            if (fromIndex + 2 >= animator.Segments.Count) yield break;

            fromIndex = pFromIndex;
            toIndex = pFromIndex + 1;

            while (CalculatingInterpolationStep(startTime, animator.Segments[fromIndex].duration, out step))
            {
                for (int i = 0; i < animator.TransformsToActOn.Count; i++)
                {
                    animator.TransformsToActOn[i].localPosition =
                        Vector3.LerpUnclamped(animator.Segments[fromIndex].toTransformData[animator.TransformsToActOn[i]].position, animator.Segments[toIndex].toTransformData[animator.TransformsToActOn[i]].position,
                        animator.Segments[0].curve.Evaluate((float)step));

                    animator.TransformsToActOn[i].localRotation =
                        Quaternion.Euler(Vector3.LerpUnclamped(animator.Segments[fromIndex].toTransformData[animator.TransformsToActOn[i]].rotation, animator.Segments[toIndex].toTransformData[animator.TransformsToActOn[i]].rotation,
                        animator.Segments[0].curve.Evaluate((float)step)));

                    animator.TransformsToActOn[i].localScale =
                        Vector3.LerpUnclamped(animator.Segments[fromIndex].toTransformData[animator.TransformsToActOn[i]].scale, animator.Segments[0].toTransformData[animator.TransformsToActOn[i]].scale,
                        animator.Segments[0].curve.Evaluate((float)step));
                }

                yield return null;
            }
        }
    }

    */

    private bool CalculatingInterpolationStep(double startTime, double duration, out double step)
    {
        step = (EditorApplication.timeSinceStartup - startTime) / duration;

        return step < 1;
    }

    private void OnEditorUpdate()
    {

    }
}
