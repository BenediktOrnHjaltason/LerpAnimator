using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public struct TransformData
{
    public TransformData(Vector3 pPosition, Quaternion pRotation, Vector3 pScale) { position = pPosition; rotation = pRotation; scale = pScale; }

    public Vector3 position;
    public Quaternion rotation;
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

public enum EEditorOrGame
{
    EDITOR,
    GAME
}

[System.Serializable]
public class LerpAnimator : MonoBehaviour
{
    [SerializeField] List<Transform> TransformsToActOn;

    /// <summary>
    /// Start states of the chosen transforms
    /// </summary>
    [SerializeField] List<TransformData> StartStates;

    /// <summary>
    /// Individual segments of complete sequence
    /// </summary>

    [SerializeField] List<Segment> Segments;

    [SerializeField] UnityEvent OnSequenceEnd;

    public int lastSelectedState;

    int fromIndex;
    int toIndex;
    float timeOnSegmentStart;
    float step;

    private void Start()
    {
        //StartSequence();
    }

    public void StartSequence()
    {
        fromIndex = -1;
        toIndex = 0;
        timeOnSegmentStart = Time.time;

        StartCoroutine(RunSegment());
    }

    

    public IEnumerator RunSegment()
    {
        Segments[toIndex].OnSegmentStart?.Invoke();


        while (CalculatingInterpolationStep(Segments[toIndex].duration, out step))
        {
            if (fromIndex == -1)
            {
                for(int i = 0; i < TransformsToActOn.Count; i++)
                {
                    TransformsToActOn[i].localPosition = Vector3.Lerp(StartStates[i].position, Segments[toIndex].toTransformData[i].position, Segments[toIndex].curve.Evaluate(step));

                    TransformsToActOn[i].localRotation = Quaternion.Lerp(StartStates[i].rotation, Segments[toIndex].toTransformData[i].rotation, Segments[toIndex].curve.Evaluate(step));

                    TransformsToActOn[i].localScale = Vector3.Lerp(StartStates[i].scale, Segments[toIndex].toTransformData[i].scale, Segments[toIndex].curve.Evaluate(step));
                }
            }

            else
            {
                for (int i = 0; i < Segments[fromIndex].toTransformData.Count; i++)
                {
                    TransformsToActOn[i].localPosition = Vector3.Lerp(Segments[fromIndex].toTransformData[i].position, Segments[toIndex].toTransformData[i].position, Segments[toIndex].curve.Evaluate(step));

                    TransformsToActOn[i].localRotation = Quaternion.Lerp(Segments[fromIndex].toTransformData[i].rotation, Segments[toIndex].toTransformData[i].rotation, Segments[toIndex].curve.Evaluate(step));

                    TransformsToActOn[i].localScale = Vector3.Lerp(StartStates[i].scale, Segments[toIndex].toTransformData[i].scale, Segments[toIndex].curve.Evaluate(step));
                }
            }

            yield return null;
        }


        //Start next segment
        if (toIndex < Segments.Count - 1)
        {
            fromIndex = fromIndex == -1 ? 0 : ++fromIndex;
            toIndex++;
            timeOnSegmentStart = Time.time;

            StartCoroutine(RunSegment());
        }

        else OnSequenceEnd?.Invoke();
    }

    bool CalculatingInterpolationStep(float duration, out float step)
    {
        step = (Time.time - timeOnSegmentStart) / duration;

        return step < 1;
    }

}
