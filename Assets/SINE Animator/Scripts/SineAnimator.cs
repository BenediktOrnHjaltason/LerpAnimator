using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace SpheroidGames.SineAnimator
{
    [System.Serializable]
    public class SineAnimator : MonoBehaviour
    {
        public enum AnimationMode
        {
            PositionBobber,
            ScaleBobber,
            RingPlane,
            RingCarousel,
            Wall
        }

        public enum ValueMode
        {
            Value,
            AbsoluteValue
        }

        public enum RingObjectsFace
        {
            NA,
            Inward,
            Outward
        }

        [Tooltip("Wether sequence should start when game starts")]
        [SerializeField] bool StartOnPlay;

        [Tooltip("The transforms that will be affected by this Sine Animator")]

        [SerializeField] List<Transform> TransformsToActOn;

        [SerializeField] private AnimationMode animationMode;

        [SerializeField] public ValueMode valueMode;

        [SerializeField] public float frequency;

        [SerializeField] public float amplitude;

        [SerializeField] public float radius;

        [SerializeField] public float ringSpin;

        [Tooltip("Places all objects on the same spot on the sine wave, producing uniform movement")]
        [SerializeField] public bool uniformMovement;

        [SerializeField] public float wallWidth;

        [SerializeField] private RingObjectsFace ringObjectsFace;

        [SerializeField] private GameObject objectToSpawn;
        [SerializeField] private int numberOfObjectsToSpawn;

        [SerializeField] private bool showGenerateObjects;

        [Tooltip("If true, gameobjects will be destroyed if removed from \"Transforms To Act On\" list")]
        [SerializeField] private bool destroyIfRemoved;

        [SerializeField] private RingObjectsFace lastRingObjectsFaceDirection;

        [Tooltip("If true, objects are auto alligned when removing or adding transforms to list, if any direction previously chosen (Inward or outward).")]
        [SerializeField] private bool autoAllign;

        private UnityEvent currentMode = new UnityEvent();

        private void Start()
        {
            if (StartOnPlay)
                StartAnimation();
        }

        public void SetAnimationFunction()
        {
            currentMode.RemoveAllListeners();

            switch (animationMode)
            {
                case AnimationMode.PositionBobber:
                    CollectOriginalPositions();
                    currentMode.AddListener(PositionBobber);
                    break;

                case AnimationMode.ScaleBobber:
                    CollectScales();
                    currentMode.AddListener(ScaleBobber);
                    break;

                case AnimationMode.RingPlane:
                    CalculateDegreesDelta();
                    currentMode.AddListener(RingPlane);
                    break;

                case AnimationMode.RingCarousel:
                    CalculateDegreesDelta();
                    currentMode.AddListener(RingCarousel);
                    break;

                case AnimationMode.Wall:
                    CalculateWallDistanceDelta();
                    currentMode.AddListener(Wall);
                    break;
            }
        }

        private bool animationRunning;

        public void StartAnimation()
        {
            if (TransformsToActOn.Count < 1)
                return;

            SetAnimationFunction();

            animationRunning = true;

            StartCoroutine(RunAnimation());
        }

        public void StopAnimation()
        {
            animationRunning = false;
        }
        
        private IEnumerator RunAnimation()
        {

            while(animationRunning)
            {
                currentMode?.Invoke();

                yield return null;
            }
        }

        #region Animation Functions

        #region Position Bobber
        private void PositionBobber()
        {
            for (int i = 0; i < TransformsToActOn.Count; i++)
            {
                if (TransformsToActOn[i] == null)
                    continue;

                TransformsToActOn[i].position =
                (valueMode == ValueMode.Value) ?
                originalPositions[i] - TransformsToActOn[i].forward * Mathf.Sin(Time.time * frequency) * amplitude :
                originalPositions[i] - TransformsToActOn[i].forward * Mathf.Abs(Mathf.Sin(Time.time * frequency)) * amplitude;
            }
        }

        private List<Vector3> originalPositions = new List<Vector3>();
        private void CollectOriginalPositions()
        {
            originalPositions.Clear();

            foreach (Transform tr in TransformsToActOn)
                originalPositions.Add(tr.position);
        }

        #endregion

        #region Scale Bobber
        private readonly List<Vector3> doubleScales = new List<Vector3>();
        private readonly List<Vector3> originalScales = new List<Vector3>();
        private void ScaleBobber()
        {
            for (int i = 0; i < TransformsToActOn.Count; i++)
            {
                if (TransformsToActOn[i] == null)
                    continue;

                TransformsToActOn[i].localScale =
                (valueMode == ValueMode.Value) ?
                Vector3.LerpUnclamped(originalScales[i], doubleScales[i], Mathf.Sin(Time.time * frequency) * amplitude) :
                Vector3.LerpUnclamped(originalScales[i], doubleScales[i], Mathf.Abs(Mathf.Sin(Time.time * frequency)) * amplitude);
            }
        }

        private void CollectScales()
        {
            originalScales.Clear();
            doubleScales.Clear();

            foreach (Transform tr in TransformsToActOn)
            {
                originalScales.Add(tr.localScale);
                doubleScales.Add(tr.localScale * 2);
            }
        }

        #endregion

        #region Rings

        private Quaternion rot;
        private Vector3 basePoint;

        /// <summary>
        /// Used to place the objects around the center
        /// </summary>
        private float degreesDelta;

        /// <summary>
        /// Used to move objects around with sine wave
        /// </summary>
        private float radiansDelta;

        /// <summary>
        /// Calculates the data neccessary to place objects on sine wave
        /// </summary>
        private void CalculateDegreesDelta()
        {
            degreesDelta = 360.0f / TransformsToActOn.Count;
            radiansDelta = uniformMovement ? 0 : (Mathf.PI * 2) / TransformsToActOn.Count;

            CalculateRingDistribution();
        }

        List<Vector3> directions = new List<Vector3>();

        /// <summary>
        /// Calculates the data neccessary to place objects around the ring
        /// </summary>
        private void CalculateRingDistribution()
        {
            directions.Clear();

            for(int i = 0; i < TransformsToActOn.Count; i++)
            {
                rot = transform.localRotation * Quaternion.Euler(0, 0, degreesDelta * (i + 1));
                basePoint = (transform.position + (rot * (Vector3.right) * 0.01f));

                directions.Add(transform.InverseTransformDirection(basePoint - transform.position));
            }
        }

        private void RingPlane()
        {
            for (int i = 0; i < TransformsToActOn.Count; i++)
            {
                if (TransformsToActOn[i] == null)
                    continue;

                TransformsToActOn[i].localPosition =
                    (directions[i] * radius) +
                    ((valueMode == ValueMode.Value) ?
                    (directions[i] * ((((Mathf.Sin((Time.time + (radiansDelta * i)) * frequency) + 1) / 2) * amplitude))) :
                    (directions[i] * (Mathf.Abs((Mathf.Sin((Time.time + (radiansDelta * i)) * frequency) * amplitude)))));
            }

            if (ringSpin != 0)
            {
                transform.Rotate(transform.forward, ringSpin * Time.deltaTime, Space.World);
            }
        }

        private void RingCarousel()
        {
            for (int i = 0; i < TransformsToActOn.Count; i++)
            {
                if (TransformsToActOn[i] == null)
                    continue;

                TransformsToActOn[i].localPosition =
                (directions[i] * radius) +
                ((valueMode == ValueMode.Value) ?
                (transform.InverseTransformDirection(transform.forward) * 0.01f * ((((Mathf.Sin((Time.time + (radiansDelta * i)) * frequency) + 1) / 2) * amplitude))) :
                (transform.InverseTransformDirection(transform.forward) * 0.01f * (Mathf.Abs((Mathf.Sin((Time.time + (radiansDelta * i)) * frequency) * amplitude)))));
            }

            if (ringSpin != 0)
            {
                transform.Rotate(transform.forward, ringSpin * Time.deltaTime, Space.World);
            }
        }
        #endregion

        #region Wall
        private float wallDistanceDelta;
        private float halfDistance;
        private void Wall()
        {
            for (int i = 0; i < TransformsToActOn.Count; i++)
            {
                if (TransformsToActOn[i] == null)
                    continue;

                TransformsToActOn[i].position = transform.position -
                    (transform.right * halfDistance) +
                    transform.right * wallDistanceDelta * i +
                    ((valueMode == ValueMode.Value) ?
                    (transform.up * (Mathf.Sin((Time.time + (radiansDelta * i)) * frequency) * amplitude)) :
                    (transform.up * (Mathf.Abs(Mathf.Sin((Time.time + (radiansDelta * i)) * frequency) * amplitude))));
            }
        }

        private void CalculateWallDistanceDelta()
        {
            wallDistanceDelta = wallWidth / TransformsToActOn.Count;
            halfDistance = wallWidth / 2;

            CalculateDegreesDelta();
        }
        #endregion

        #endregion
    }
}
