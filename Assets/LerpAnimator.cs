using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public struct TransformData
{
    public TransformData(Vector3 pPosition, Vector3 pRotation, Vector3 pScale) { position = pPosition; rotation = pRotation; scale = pScale; }

    public Vector3 position;

    //NOTE: For start value, this will actually be used to set rotation, but for segments, it is used to give an offsett to use in Lerp'ing.
    public Vector3 rotation;
    public Vector3 scale;
}

[System.Serializable]
public struct Segment
{
    public List<TransformData> toTransformData;

    public float duration;
    public AnimationCurve curve;

    public UnityEvent OnSegmentStart;
}

[SerializeField]
public struct SegmentRotation
{
    public string transformName;
    public Vector3 rotationToAdd;
}

public enum EEditorOrGame
{
    EDITOR,
    GAME
}

[System.Serializable]
public class LerpAnimator : MonoBehaviour
{
    [SerializeField] bool StartOnPlay;

    [SerializeField] List<Transform> TransformsToActOn;

    /// <summary>
    /// Start states of the chosen transforms
    /// </summary>
    [SerializeField] List<TransformData> StartStates;

    /// <summary>
    /// Individual segments of complete sequence
    /// </summary>

    public List<Segment> Segments;

    [SerializeField] UnityEvent OnSequenceEnd;

    public int lastSelectedState;

    [SerializeField] List<bool> ShowRotations;

    [SerializeField] List<bool> ShowSegmentEvents;

    private void Start()
    {
        ApplyStartStates();

        if (StartOnPlay) StartSequence();
    }

    private void SampleInterSegmentRotations()
    {
        //Sample current rotations
        interSegmentRotations = new List<Quaternion>();

        foreach (Transform transform in TransformsToActOn)
            interSegmentRotations.Add(transform.localRotation);
    }

    int fromIndex;
    int toIndex;
    float timeOnSegmentStart;
    float lerpStep;

    float reciprocal;

    public void StartSequence()
    {
        fromIndex = -1;
        toIndex = 0;
        timeOnSegmentStart = Time.time;

        SampleInterSegmentRotations();

        StartCoroutine(RunSegment());
    }

    private List<Quaternion> interSegmentRotations;

    public IEnumerator RunSegment()
    {
        Segments[toIndex].OnSegmentStart?.Invoke();

        while (CalculatingInterpolationStep(Segments[toIndex].duration, out lerpStep))
        {
            if (fromIndex == -1)
            {
                for(int i = 0; i < TransformsToActOn.Count; i++)
                {
                    TransformsToActOn[i].localPosition = Vector3.Lerp(StartStates[i].position, Segments[toIndex].toTransformData[i].position, Segments[toIndex].curve.Evaluate(lerpStep));

                    if (Segments[toIndex].toTransformData[i].rotation != Vector3.zero)
                    {
                        TransformsToActOn[i].localRotation = Quaternion.Euler(StartStates[i].rotation) *
                                Quaternion.Euler(Vector3.Lerp(Vector3.zero, Segments[toIndex].toTransformData[i].rotation, Segments[toIndex].curve.Evaluate(lerpStep)));
                    }


                    TransformsToActOn[i].localScale = Vector3.Lerp(StartStates[i].scale, Segments[toIndex].toTransformData[i].scale, Segments[toIndex].curve.Evaluate(lerpStep));
                }
            }

            else
            {
                for (int i = 0; i < TransformsToActOn.Count; i++)
                {
                    TransformsToActOn[i].localPosition = Vector3.Lerp(Segments[fromIndex].toTransformData[i].position, Segments[toIndex].toTransformData[i].position, Segments[toIndex].curve.Evaluate(lerpStep));

                    if (Segments[toIndex].toTransformData[i].rotation != Vector3.zero)
                    {
                        TransformsToActOn[i].localRotation = interSegmentRotations[i] *
                                Quaternion.Euler(Vector3.Lerp(Vector3.zero, Segments[toIndex].toTransformData[i].rotation, Segments[toIndex].curve.Evaluate(lerpStep)));
                    }

                    TransformsToActOn[i].localScale = Vector3.Lerp(Segments[fromIndex].toTransformData[i].scale, Segments[toIndex].toTransformData[i].scale, Segments[toIndex].curve.Evaluate(lerpStep));
                }
            }

            yield return null;
        }
        
        
        //Make sure segment arrived fully at destination
        for (int i = 0; i < TransformsToActOn.Count; i++)
        {
            TransformsToActOn[i].localPosition = Segments[toIndex].toTransformData[i].position;

            if (Segments[toIndex].toTransformData[i].rotation != Vector3.zero)
            {
                TransformsToActOn[i].localRotation = interSegmentRotations[i] *
                                Quaternion.Euler(Vector3.Lerp(Vector3.zero, Segments[toIndex].toTransformData[i].rotation, Segments[toIndex].curve.Evaluate(lerpStep)));
            }

            TransformsToActOn[i].localScale = Segments[toIndex].toTransformData[i].scale;
        }
        


        //Start next segment
        if (toIndex < Segments.Count - 1)
        {
            fromIndex = fromIndex == -1 ? 0 : ++fromIndex;
            toIndex++;
            timeOnSegmentStart = Time.time;

            SampleInterSegmentRotations();

            StartCoroutine(RunSegment());
        }

        else OnSequenceEnd?.Invoke();
    }

    bool CalculatingInterpolationStep(float duration, out float step)
    {
        step = (Time.time - timeOnSegmentStart) / duration;

        return step < 1;
    }

    private void ApplyStartStates()
    {
        for (int i = 0; i < TransformsToActOn.Count; i++)
        {
            TransformsToActOn[i].localPosition = StartStates[i].position;
            TransformsToActOn[i].localRotation = Quaternion.Euler(StartStates[i].rotation);
            TransformsToActOn[i].localScale = StartStates[i].scale;
        }
    }
}
