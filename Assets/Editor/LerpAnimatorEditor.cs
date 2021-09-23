using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;



[CustomEditor(typeof(LerpAnimator))]
public class LerpAnimatorEditor : Editor
{
    LerpAnimator animator;

    private void OnEnable()
    {
        animator = (LerpAnimator)target;

        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        //ApplyStartState();
        EditorApplication.update -= OnEditorUpdate;
    }

    bool justModifiedSegmentsNumer = false;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("TransformsToActOn"), true);

        if (GUILayout.Button("Sample"))
        {
            SampleStartState();
        }

        if (GUILayout.Button("Apply"))
        {
            ApplyStartState();
        }

        if(animator.Segments.Count > 0)
        {
            if (GUILayout.Button("Play from start"))
            {
                ApplyStartState();
            }
        }



        GUILayout.Label("Segments");

        int numberOfSegments = serializedObject.FindProperty("Segments").arraySize;

        if (GUILayout.Button("Add segment"))
        {
            Debug.Log("Pressed Add segment");
            AddSegment();
            justModifiedSegmentsNumer = true;
            
        }

        if (GUILayout.Button("Remove segment"))
        {
            if (numberOfSegments < 2) return;

            Debug.Log("Pressed Remove segment");
            RemoveSegment();
            justModifiedSegmentsNumer = true;
        }

        //EditorGUILayout.PropertyField(serializedObject.FindProperty("Segments"));

        
        if (!justModifiedSegmentsNumer)
        {
            

            for (int i = 0; i < numberOfSegments; i++)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("Segments").GetArrayElementAtIndex(i));

                if (GUILayout.Button("Sample"))
                {
                }

                if (GUILayout.Button("Apply"))
                {
                }

                if (GUILayout.Button("Play from here"))
                {
                }
            }

            
        }
        justModifiedSegmentsNumer = false;

        serializedObject.ApplyModifiedProperties();
    }

    private void SampleStartState()
    {
        animator.startStates = new Dictionary<Transform,TransformData>();

        foreach(Transform transform in animator.TransformsToActOn)
        {
            animator.startStates[transform] = new TransformData(transform.localPosition, transform.localRotation.eulerAngles, transform.localScale);
        }
    }

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
        if (index == -1)
        {
            for (int i = 0; i < animator.TransformsToActOn.Count; i++)
            {
                animator.TransformsToActOn[i].localPosition = animator.startStates[animator.TransformsToActOn[i]].position;
                animator.TransformsToActOn[i].localRotation = Quaternion.Euler(animator.startStates[animator.TransformsToActOn[i]].rotation);
                animator.TransformsToActOn[i].localScale = animator.startStates[animator.TransformsToActOn[i]].scale;
            }
        }
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
