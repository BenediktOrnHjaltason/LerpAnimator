using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class TransformData
{
    public TransformData(Vector3 pPosition, Vector3 pRotation, Vector3 pScale) { position = pPosition; rotation = pRotation; scale = pScale; }

    public Vector3 position;
    public Vector3 rotation;
    public Vector3 scale;
}

[System.Serializable]
public class Segment
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
    public List<Transform> TransformsToActOn;

    /// <summary>
    /// Start states of the chosen transforms
    /// </summary>
    public List<TransformData> StartStates;

    /// <summary>
    /// Individual segments of complete sequence
    /// </summary>

    public List<Segment> Segments;

    public UnityEvent OnSequenceEnd;


    #region Validation

    public List<int> GetDeletedTransformsIndexes()
    {
        List<int> invalidTransformsIndexes = new List<int>();

        for (int i = 0; i < TransformsToActOn.Count; i++)
        {
            if (TransformsToActOn[i] == null)
                invalidTransformsIndexes.Add(i);
        }

        return invalidTransformsIndexes;
    }

    #endregion

}
