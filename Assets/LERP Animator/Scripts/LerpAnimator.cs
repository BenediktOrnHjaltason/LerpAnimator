using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class TransformData
{
    public TransformData(Vector3 pPosition, Vector3 pRotOffset, Vector3 pScale) { position = pPosition; offset = pRotOffset; scale = pScale; }

    public Vector3 position;

    //NOTE: For StartStates, this will actually be used to save rotation, but for segments, it is used to give an offsett from start rotation.
    public Vector3 offset;
    public Vector3 scale;
}

[System.Serializable]
public class Segment
{
    public UnityEvent OnLerpStart;

    public List<TransformData> toTransformData;

    public string name;

    public float duration;

    public float pauseAfter;

    public AnimationCurve curve;

    public UnityEvent OnLerpEnd;
}

public enum EEditorOrGame
{
    EDITOR,
    GAME
}

[System.Serializable]
public class LerpAnimator : MonoBehaviour
{
    [Tooltip("Wether sequence should start when game starts")]
    [SerializeField] bool StartOnPlay;

    [Tooltip("Wether sequence should loop")]
    [SerializeField] bool Loop;

    [Tooltip("The transforms that will be affected by this Lerp Animator")]
#if UNITY_2020_3_OR_NEWER
[NonReorderable]
#endif
    [SerializeField] List<Transform> TransformsToActOn;

    [Tooltip("The start states for this animatic")]
    [SerializeField] List<TransformData> StartStates;

    public List<Segment> Segments;

    public int lastSelectedState;

    [SerializeField] List<bool> ShowRotations;

    [SerializeField] List<bool> ShowSegmentEvents;

    private void Start()
    {
        if (StartOnPlay) 
            StartSequence();
    }

    private int fromIndex;
    private int toIndex;
    private float timeOnStart;
    private float lerpStep;
    private float timeOnPauseEnd;
    private float reciprocal;

    public void StartSequence()
    {
        if (Segments.Count < 1 || TransformsToActOn.Count < 1)
            return;

        fromIndex = -1;
        toIndex = 0;
        timeOnStart = Time.time;

        ApplyStartStates();

        SampleInterSegmentRotations();

        reciprocal = 1 / Segments[toIndex].duration;

        StartCoroutine(RunSegment());
    }

    private List<Quaternion> interSegmentRotations;

    private IEnumerator RunSegment()
    {
        Segments[toIndex].OnLerpStart?.Invoke();

        while (CalculatingInterpolationStep(out lerpStep))
        {
            if (fromIndex == -1)
            {
                for(int i = 0; i < TransformsToActOn.Count; i++)
                {
                    if (TransformsToActOn[i] != null)
                    {
                        if (StartStates[i].position != Segments[toIndex].toTransformData[i].position)
                        {
                            TransformsToActOn[i].localPosition = Vector3.LerpUnclamped(StartStates[i].position,
                                                                              Segments[toIndex].toTransformData[i].position,
                                                                              Segments[toIndex].curve.Evaluate(lerpStep));
                        }

                        if (Segments[toIndex].toTransformData[i].offset != Vector3.zero)
                        {
                            TransformsToActOn[i].localRotation = Quaternion.Euler(StartStates[i].offset) *
                                    Quaternion.Euler(Vector3.LerpUnclamped(Vector3.zero, Segments[toIndex].toTransformData[i].offset, Segments[toIndex].curve.Evaluate(lerpStep)));
                        }

                        if (StartStates[i].scale != Segments[toIndex].toTransformData[i].scale)
                        {
                            TransformsToActOn[i].localScale = Vector3.LerpUnclamped(StartStates[i].scale,
                                                                       Segments[toIndex].toTransformData[i].scale,
                                                                       Segments[toIndex].curve.Evaluate(lerpStep));
                        }
                    }
                }
            }

            else
            {
                for (int i = 0; i < TransformsToActOn.Count; i++)
                {
                    if (TransformsToActOn[i] != null)
                    {
                        if (Segments[fromIndex].toTransformData[i].position != Segments[toIndex].toTransformData[i].position)
                        {
                            TransformsToActOn[i].localPosition = Vector3.LerpUnclamped(Segments[fromIndex].toTransformData[i].position,
                                                                              Segments[toIndex].toTransformData[i].position,
                                                                              Segments[toIndex].curve.Evaluate(lerpStep));
                        }

                        if (Segments[toIndex].toTransformData[i].offset != Vector3.zero)
                        {
                            TransformsToActOn[i].localRotation = interSegmentRotations[i] *
                                    Quaternion.Euler(Vector3.LerpUnclamped(Vector3.zero, Segments[toIndex].toTransformData[i].offset, Segments[toIndex].curve.Evaluate(lerpStep)));
                        }

                        if (Segments[fromIndex].toTransformData[i].scale != Segments[toIndex].toTransformData[i].scale)
                        {
                            TransformsToActOn[i].localScale = Vector3.LerpUnclamped(Segments[fromIndex].toTransformData[i].scale,
                                                                           Segments[toIndex].toTransformData[i].scale,
                                                                           Segments[toIndex].curve.Evaluate(lerpStep));
                        }
                    }
                }
            }

            yield return null;
        }

        //Make sure segment arrived fully at destination
        for (int i = 0; i < TransformsToActOn.Count; i++)
        {
            if (TransformsToActOn[i] != null)
            {
                TransformsToActOn[i].localPosition = Segments[toIndex].toTransformData[i].position;

                if (Segments[toIndex].toTransformData[i].offset != Vector3.zero)
                {
                    TransformsToActOn[i].localRotation = interSegmentRotations[i] *
                                    Quaternion.Euler(Vector3.Lerp(Vector3.zero, Segments[toIndex].toTransformData[i].offset, Segments[toIndex].curve.Evaluate(lerpStep)));
                }

                TransformsToActOn[i].localScale = Segments[toIndex].toTransformData[i].scale;
            }
        }

        Segments[toIndex].OnLerpEnd?.Invoke();

        //Start next segment
        if (toIndex < Segments.Count - 1)
        {
            fromIndex = fromIndex == -1 ? 0 : ++fromIndex;
            toIndex++;
            timeOnStart = Time.time;

            reciprocal = 1 / Segments[toIndex].duration;

            SampleInterSegmentRotations();

            if(Segments[toIndex - 1].pauseAfter > 0)
            {
                timeOnStart = Time.time;
                timeOnPauseEnd = Time.time + Segments[toIndex - 1].pauseAfter;

                StartCoroutine(RunPauseAfterSegment());
            }

            else StartCoroutine(RunSegment());
        }

        else
        {
            if (Loop)
            {
                if (Segments[toIndex - 1].pauseAfter > 0)
                {
                    timeOnStart = Time.time;
                    timeOnPauseEnd = Time.time + Segments[toIndex].pauseAfter;

                    StartCoroutine(RunLastSegmentPause());
                }

                else StartSequence();
            }
        }
    }

    private IEnumerator RunPauseAfterSegment()
    {
        while (Time.time < timeOnPauseEnd)
            yield return null;

        timeOnStart = Time.time;

        StartCoroutine(RunSegment());
    }

    private IEnumerator RunLastSegmentPause()
    {
        while (Time.time < timeOnPauseEnd)
            yield return null;

        if (Loop)
            StartSequence();
    }

    private bool CalculatingInterpolationStep(out float step)
    {
        step = (Time.time - timeOnStart) * reciprocal;

        return step < 1;
    }

    private void ApplyStartStates()
    {
        for (int i = 0; i < TransformsToActOn.Count; i++)
        {
            if (TransformsToActOn[i] != null)
            {
                TransformsToActOn[i].localPosition = StartStates[i].position;
                TransformsToActOn[i].localRotation = Quaternion.Euler(StartStates[i].offset);
                TransformsToActOn[i].localScale = StartStates[i].scale;
            }
        }
    }

    private void SampleInterSegmentRotations()
    {
        //Sample current rotations
        interSegmentRotations = new List<Quaternion>();

        foreach (Transform transform in TransformsToActOn)
            if (transform != null)
                interSegmentRotations.Add(transform.localRotation);

            //We need something in the array to keep the number of elements correct
            else interSegmentRotations.Add(Quaternion.identity);
    }
}
