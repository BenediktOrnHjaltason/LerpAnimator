using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace SpheroidGames.LerpAnimator
{

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
        public string Name;

        public UnityEvent OnLerpStart;

        public List<TransformData> toTransformData;

        public float duration;

        public float pauseAfter;

        public AnimationCurve curve;

        public UnityEvent OnLerpEnd;

        public bool showEvents;

        public bool showRotationOffsets;
    }

    [System.Serializable]
    public class Sequence
    {
        public string Name;

        [Tooltip("Wether sequence should start when game starts")]
        public bool StartOnPlay;

        [Tooltip("Wether sequence should loop")]
        public bool Loop;

        [Tooltip("The start states for this sequence")]
        public List<TransformData> StartStates;

        public List<Segment> Segments;

        public bool ShowSegments;
    }

    [System.Serializable]
    public class LerpAnimator : MonoBehaviour
    {
        [SerializeField] string SequenceName;

        [Tooltip("Wether sequence should start when game starts")]
        [SerializeField] bool StartOnPlay;

        [Tooltip("Wether sequence should loop")]
        [SerializeField] bool Loop;

        [Tooltip("The transforms that will be affected by this Lerp Animator")]
#if UNITY_2020_2_OR_NEWER
[NonReorderable]
#endif
        [SerializeField] List<Transform> TransformsToActOn;

        [SerializeField] List<Sequence> Sequences;

        [SerializeField] int lastSelectedState;

        [SerializeField] int lastSelectedSequence;
        [SerializeField] int lastSelectedSegment;

        //Deprecated

        [Tooltip("The start states for this sequence")]
        [SerializeField] List<TransformData> StartStates;

        [SerializeField] List<Segment> Segments;

        [SerializeField] List<bool> ShowRotations;

        [SerializeField] List<bool> ShowSegmentEvents;

        private void Start()
        {
            for (int i = 0; i < Sequences.Count; i++)
            {
                if (Sequences[i].StartOnPlay)
                {
                    lastSelectedSequence = i;
                    StartSequence();
                }
            }
        }

        private int fromIndex;
        private int toIndex;
        private float timeOnStart;
        private float lerpStep;
        private float timeOnPauseEnd;
        private float reciprocal;

        public void StartSequence()
        {
            if (Sequences[lastSelectedSequence].Segments.Count < 1 || TransformsToActOn.Count < 1)
                return;

            StopAllCoroutines();
            playingSingleSegment = false;

            fromIndex = -1;
            toIndex = 0;
            timeOnStart = Time.time;

            ApplyStartStates();

            SampleInterSegmentRotations();

            reciprocal = 1 / Sequences[lastSelectedSequence].Segments[toIndex].duration;

            StartCoroutine(RunSegment());
        }

        private List<Quaternion> interSegmentRotations = new List<Quaternion>();

        private bool playingSingleSegment = false;
        /// <summary>
        /// Plays a single segment number (as displayed in inspector (#:Play))
        /// </summary>
        /// <param name="segmentNumber"></param>
        public void PlaySingleSegment(int segmentNumber)
        { 
            if (segmentNumber > Sequences[lastSelectedSequence].Segments.Count || segmentNumber < 1)
            {
                Debug.LogWarning($"LERP Animator: PlaySingleSegment() called with segment number out of bounds. There is no segment number {segmentNumber} on LERP Animator instance attached to {name}");
                return;
            }

            StopAllCoroutines();

            fromIndex = segmentNumber == 1 ? -1 : segmentNumber - 2;
            toIndex = fromIndex == -1 ? 0 : fromIndex + 1;

            reciprocal = 1 / Sequences[lastSelectedSequence].Segments[toIndex].duration;

            ApplyFromDatastore(fromIndex);
            SampleInterSegmentRotations();

            playingSingleSegment = true;

            StartCoroutine(RunSegment());
        }

        private IEnumerator RunSegment()
        {
            Sequences[lastSelectedSequence].Segments[toIndex].OnLerpStart?.Invoke();

            while (CalculatingInterpolationStep(out lerpStep))
            {
                if (fromIndex == -1)
                {
                    for (int i = 0; i < TransformsToActOn.Count; i++)
                    {
                        if (TransformsToActOn[i] != null)
                        {
                            if (Sequences[lastSelectedSequence].StartStates[i].position != Sequences[lastSelectedSequence].Segments[toIndex].toTransformData[i].position)
                            {
                                TransformsToActOn[i].localPosition = Vector3.LerpUnclamped(Sequences[lastSelectedSequence].StartStates[i].position,
                                                                                  Sequences[lastSelectedSequence].Segments[toIndex].toTransformData[i].position,
                                                                                  Sequences[lastSelectedSequence].Segments[toIndex].curve.Evaluate(lerpStep));
                            }

                            if (Sequences[lastSelectedSequence].Segments[toIndex].toTransformData[i].offset != Vector3.zero)
                            {
                                TransformsToActOn[i].localRotation = Quaternion.Euler(Sequences[lastSelectedSequence].StartStates[i].offset) *
                                        Quaternion.Euler(Vector3.LerpUnclamped(Vector3.zero, Sequences[lastSelectedSequence].Segments[toIndex].toTransformData[i].offset, Sequences[lastSelectedSequence].Segments[toIndex].curve.Evaluate(lerpStep)));
                            }

                            if (Sequences[lastSelectedSequence].StartStates[i].scale != Sequences[lastSelectedSequence].Segments[toIndex].toTransformData[i].scale)
                            {
                                TransformsToActOn[i].localScale = Vector3.LerpUnclamped(Sequences[lastSelectedSequence].StartStates[i].scale,
                                                                           Sequences[lastSelectedSequence].Segments[toIndex].toTransformData[i].scale,
                                                                           Sequences[lastSelectedSequence].Segments[toIndex].curve.Evaluate(lerpStep));
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
                            if (Sequences[lastSelectedSequence].Segments[fromIndex].toTransformData[i].position != Sequences[lastSelectedSequence].Segments[toIndex].toTransformData[i].position)
                            {
                                TransformsToActOn[i].localPosition = Vector3.LerpUnclamped(Sequences[lastSelectedSequence].Segments[fromIndex].toTransformData[i].position,
                                                                                  Sequences[lastSelectedSequence].Segments[toIndex].toTransformData[i].position,
                                                                                  Sequences[lastSelectedSequence].Segments[toIndex].curve.Evaluate(lerpStep));
                            }

                            if (Sequences[lastSelectedSequence].Segments[toIndex].toTransformData[i].offset != Vector3.zero)
                            {
                                TransformsToActOn[i].localRotation = interSegmentRotations[i] *
                                        Quaternion.Euler(Vector3.LerpUnclamped(Vector3.zero, Sequences[lastSelectedSequence].Segments[toIndex].toTransformData[i].offset, Sequences[lastSelectedSequence].Segments[toIndex].curve.Evaluate(lerpStep)));
                            }

                            if (Sequences[lastSelectedSequence].Segments[fromIndex].toTransformData[i].scale != Sequences[lastSelectedSequence].Segments[toIndex].toTransformData[i].scale)
                            {
                                TransformsToActOn[i].localScale = Vector3.LerpUnclamped(Sequences[lastSelectedSequence].Segments[fromIndex].toTransformData[i].scale,
                                                                               Sequences[lastSelectedSequence].Segments[toIndex].toTransformData[i].scale,
                                                                               Sequences[lastSelectedSequence].Segments[toIndex].curve.Evaluate(lerpStep));
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
                    TransformsToActOn[i].localPosition = Sequences[lastSelectedSequence].Segments[toIndex].toTransformData[i].position;

                    if (Sequences[lastSelectedSequence].Segments[toIndex].toTransformData[i].offset != Vector3.zero)
                    {
                        TransformsToActOn[i].localRotation = interSegmentRotations[i] *
                                        Quaternion.Euler(Vector3.Lerp(Vector3.zero, Sequences[lastSelectedSequence].Segments[toIndex].toTransformData[i].offset, Sequences[lastSelectedSequence].Segments[toIndex].curve.Evaluate(lerpStep)));
                    }

                    TransformsToActOn[i].localScale = Sequences[lastSelectedSequence].Segments[toIndex].toTransformData[i].scale;
                }
            }

            Sequences[lastSelectedSequence].Segments[toIndex].OnLerpEnd?.Invoke();

            if (playingSingleSegment)
            {
                playingSingleSegment = false;
                yield break;
            }

            //Start next segment
            if (toIndex < Sequences[lastSelectedSequence].Segments.Count - 1)
            {
                fromIndex = fromIndex == -1 ? 0 : ++fromIndex;
                toIndex++;
                timeOnStart = Time.time;

                reciprocal = 1 / Sequences[lastSelectedSequence].Segments[toIndex].duration;

                SampleInterSegmentRotations();

                if (Sequences[lastSelectedSequence].Segments[toIndex - 1].pauseAfter > 0)
                {
                    timeOnStart = Time.time;
                    timeOnPauseEnd = Time.time + Sequences[lastSelectedSequence].Segments[toIndex - 1].pauseAfter;

                    StartCoroutine(RunPauseAfterSegment());
                }

                else StartCoroutine(RunSegment());
            }

            else
            {
                if (Loop)
                {
                    if (Sequences[lastSelectedSequence].Segments[toIndex].pauseAfter > 0)
                    {
                        timeOnStart = Time.time;
                        timeOnPauseEnd = Time.time + Sequences[lastSelectedSequence].Segments[toIndex].pauseAfter;

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
                    TransformsToActOn[i].localPosition = Sequences[lastSelectedSequence].StartStates[i].position;
                    TransformsToActOn[i].localRotation = Quaternion.Euler(Sequences[lastSelectedSequence].StartStates[i].offset);
                    TransformsToActOn[i].localScale = Sequences[lastSelectedSequence].StartStates[i].scale;
                }
            }
        }

        public void ApplyFromDatastore(int segmentIndex)
        {
            if (segmentIndex == -1)
            {
                for (int i = 0; i < TransformsToActOn.Count; i++)
                {
                    if (TransformsToActOn[i] != null)
                    {
                        TransformsToActOn[i].localPosition = Sequences[lastSelectedSequence].StartStates[i].position;

                        TransformsToActOn[i].localRotation = Quaternion.Euler(Sequences[lastSelectedSequence].StartStates[i].offset);


                        TransformsToActOn[i].localScale = Sequences[lastSelectedSequence].StartStates[i].scale;
                    }
                }
            }

            else
            {
                for (int i = 0; i < TransformsToActOn.Count; i++)
                {
                    if (TransformsToActOn[i] != null)
                    {
                        TransformsToActOn[i].localPosition =
                        Sequences[lastSelectedSequence].Segments[segmentIndex].toTransformData[i].position;

                        Quaternion acculumatedRotationOffsett = Quaternion.Euler(Sequences[lastSelectedSequence].StartStates[i].offset);

                        for (int j = 0; j <= segmentIndex; j++)
                            acculumatedRotationOffsett *= Quaternion.Euler(Sequences[lastSelectedSequence].Segments[j].toTransformData[i].offset);

                        TransformsToActOn[i].localRotation = acculumatedRotationOffsett;

                        TransformsToActOn[i].localScale =
                        Sequences[lastSelectedSequence].Segments[segmentIndex].toTransformData[i].scale;
                    }
                }
            }
        }

        private void SampleInterSegmentRotations()
        {
            //Sample current rotations
            interSegmentRotations.Clear();

            foreach (Transform transform in TransformsToActOn)
                if (transform != null)
                    interSegmentRotations.Add(transform.localRotation);

                //We need something in the array to keep the number of elements correct
                else interSegmentRotations.Add(Quaternion.identity);
        }
    }
}
