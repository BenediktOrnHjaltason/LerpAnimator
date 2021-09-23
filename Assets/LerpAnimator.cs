using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public struct TransformData
{
    public TransformData(Vector3 pPosition, Vector3 pRotation, Vector3 pScale) { position = pPosition; rotation = pRotation; scale = pScale; }

    public Vector3 position;
    public Vector3 rotation;
    public Vector3 scale;
}

[System.Serializable]
public class Segment
{
    [HideInInspector] public Dictionary<Transform, TransformData> toTransformData;

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
    EEditorOrGame editorOrGame = EEditorOrGame.EDITOR;

    public List<Transform> TransformsToActOn;

    /// <summary>
    /// Start states of the chosen transforms
    /// </summary>
    public Dictionary<Transform,TransformData> startStates;

    /// <summary>
    /// Individual segments of complete sequence
    /// </summary>

    public List<Segment> Segments;

    public UnityAction OnSequenceEnd;

    public void EnsureTransformsAndDataConsistency()
    {
        List<Transform> transformsToRemove = new List<Transform>();

        //If data store contains data for removed transform, remove from data store
        foreach(KeyValuePair<Transform, TransformData> pair in startStates)
        {
            if (!TransformsToActOn.Contains(pair.Key)) transformsToRemove.Add(pair.Key);
        }

        foreach (Transform transform in transformsToRemove)
        {
            startStates.Remove(transform);
            
            foreach(Segment segment in Segments)
            {
                segment.toTransformData.Remove(transform);
            }
        }
    }

    //EditorApplication.timeSinceStartup
}
