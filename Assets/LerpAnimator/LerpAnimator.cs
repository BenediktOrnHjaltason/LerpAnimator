using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class TransformData
{
    public TransformData(Vector3 pPosition, Vector3 pRotOffset, Vector3 pScale) { position = pPosition; offset = pRotOffset; scale = pScale; }

    public Vector3 position;

    //NOTE: For StartStates, this will actually be used to set rotation, but for segments, it is used to give an offsett from start values.
    public Vector3 offset;
    public Vector3 scale;
}

[System.Serializable]
public class Segment
{
    public List<TransformData> toTransformData;

    public string name;

    public float duration;
    public AnimationCurve curve;

    public UnityEvent OnSegmentStart;
}

public enum EEditorOrGame
{
    EDITOR,
    GAME
}

[System.Serializable]
public class LerpAnimator : MonoBehaviour
{
    [Tooltip("Wether sequence should start when game starts in this scene")]
    [SerializeField] bool StartOnPlay;

    [Tooltip("Wether sequence should loop when playing in game play mode")]
    [SerializeField] bool Loop;

    [Tooltip("The transforms that will be affected by this Lerp Animator")]
    [SerializeField] List<Transform> TransformsToActOn;

    [Tooltip("The start state for this animatic")]
    [SerializeField] List<TransformData> StartStates;

    public List<Segment> Segments;

    [Tooltip("The events that will be triggered when sequence ends in play mode")]
    [SerializeField] UnityEvent OnSequenceEnd;

    public int lastSelectedState;

    [SerializeField] List<bool> ShowRotations;

    [SerializeField] List<bool> ShowSegmentEvents;

    private void Start()
    {
        if (StartOnPlay) 
            StartSequence();
    }

    private void SampleInterSegmentRotations()
    {
        //Sample current rotations
        interSegmentRotations = new List<Quaternion>();

        foreach (Transform transform in TransformsToActOn)
            if (transform)
                interSegmentRotations.Add(transform.localRotation);

            //We need something in the array to keep the number of elements correct
            else interSegmentRotations.Add(Quaternion.identity);
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

        ApplyStartStates();

        SampleInterSegmentRotations();

        reciprocal = 1 / Segments[toIndex].duration;

        StartCoroutine(RunSegment());
    }

    private List<Quaternion> interSegmentRotations;

    public IEnumerator RunSegment()
    {
        Segments[toIndex].OnSegmentStart?.Invoke();

        while (CalculatingInterpolationStep(out lerpStep))
        {
            if (fromIndex == -1)
            {
                for(int i = 0; i < TransformsToActOn.Count; i++)
                {
                    if (StartStates[i].position != Segments[toIndex].toTransformData[i].position)
                    {
                        TransformsToActOn[i].localPosition = Vector3.Lerp(StartStates[i].position, 
                                                                          Segments[toIndex].toTransformData[i].position, 
                                                                          Segments[toIndex].curve.Evaluate(lerpStep));
                    }
                    

                    if (Segments[toIndex].toTransformData[i].offset != Vector3.zero)
                    {
                        TransformsToActOn[i].localRotation = Quaternion.Euler(StartStates[i].offset) *
                                Quaternion.Euler(Vector3.Lerp(Vector3.zero, Segments[toIndex].toTransformData[i].offset, Segments[toIndex].curve.Evaluate(lerpStep)));
                    }

                    if (StartStates[i].scale != Segments[toIndex].toTransformData[i].scale)
                    {
                        TransformsToActOn[i].localScale = Vector3.Lerp(StartStates[i].scale,
                                                                   Segments[toIndex].toTransformData[i].scale,
                                                                   Segments[toIndex].curve.Evaluate(lerpStep));
                    }
                }
            }

            else
            {
                for (int i = 0; i < TransformsToActOn.Count; i++)
                {
                    if (Segments[fromIndex].toTransformData[i].position != Segments[toIndex].toTransformData[i].position)
                    {
                        TransformsToActOn[i].localPosition = Vector3.Lerp(Segments[fromIndex].toTransformData[i].position, 
                                                                          Segments[toIndex].toTransformData[i].position, 
                                                                          Segments[toIndex].curve.Evaluate(lerpStep));
                    }
                    
                    if (Segments[toIndex].toTransformData[i].offset != Vector3.zero)
                    {
                        TransformsToActOn[i].localRotation = interSegmentRotations[i] *
                                Quaternion.Euler(Vector3.Lerp(Vector3.zero, Segments[toIndex].toTransformData[i].offset, Segments[toIndex].curve.Evaluate(lerpStep)));
                    }

                    if(Segments[fromIndex].toTransformData[i].scale != Segments[toIndex].toTransformData[i].scale)
                    {
                        TransformsToActOn[i].localScale = Vector3.Lerp(Segments[fromIndex].toTransformData[i].scale, 
                                                                       Segments[toIndex].toTransformData[i].scale, 
                                                                       Segments[toIndex].curve.Evaluate(lerpStep));
                    }
                    
                }
            }

            yield return null;
        }
        
        
        //Make sure segment arrived fully at destination
        for (int i = 0; i < TransformsToActOn.Count; i++)
        {
            TransformsToActOn[i].localPosition = Segments[toIndex].toTransformData[i].position;

            if (Segments[toIndex].toTransformData[i].offset != Vector3.zero)
            {
                TransformsToActOn[i].localRotation = interSegmentRotations[i] *
                                Quaternion.Euler(Vector3.Lerp(Vector3.zero, Segments[toIndex].toTransformData[i].offset, Segments[toIndex].curve.Evaluate(lerpStep)));
            }

            TransformsToActOn[i].localScale = Segments[toIndex].toTransformData[i].scale;
        }



        //Start next segment
        if (toIndex < Segments.Count - 1)
        {
            fromIndex = fromIndex == -1 ? 0 : ++fromIndex;
            toIndex++;
            timeOnSegmentStart = Time.time;

            reciprocal = 1 / Segments[toIndex].duration;

            SampleInterSegmentRotations();

            StartCoroutine(RunSegment());
        }

        else
        {
            OnSequenceEnd?.Invoke();

            if (Loop)
            {
                StartSequence();
            }
        }
    }

    bool CalculatingInterpolationStep(out float step)
    {
        step = (Time.time - timeOnSegmentStart) * reciprocal;

        return step < 1;
    }

    private void ApplyStartStates()
    {
        for (int i = 0; i < TransformsToActOn.Count; i++)
        {
            TransformsToActOn[i].localPosition = StartStates[i].position;
            TransformsToActOn[i].localRotation = Quaternion.Euler(StartStates[i].offset);
            TransformsToActOn[i].localScale = StartStates[i].scale;
        }
    }
}
