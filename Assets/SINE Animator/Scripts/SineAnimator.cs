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
            PositionLerp,
            ScaleLerp,
            RingOfMotion,
            WallOfMotion
        }

        public enum ValueMode
        {
            Value,
            AbsoluteValue
        }

        [Tooltip("Wether sequence should start when game starts")]
        [SerializeField] bool StartOnPlay;

        [Tooltip("The transforms that will be affected by this Sine Animator")]

        [SerializeField] List<Transform> TransformsToActOn;

        [SerializeField] AnimationMode animationMode;

        [SerializeField] ValueMode valueMode;

        [SerializeField] float speed;

        [SerializeField] float amplitude;


        //Ring of movement specific
        [SerializeField] float radius;


        private UnityEvent currentMode = new UnityEvent();




        private void Start()
        {
            currentMode.RemoveAllListeners();

            switch (animationMode)
            {
                case AnimationMode.PositionLerp:
                    currentMode.AddListener(PositionLerp);
                    break;
                case AnimationMode.ScaleLerp:
                    currentMode.AddListener(ScaleLerp);
                    break;
                case AnimationMode.RingOfMotion:
                    currentMode.AddListener(RingOfMotion);
                    startRotation = transform.rotation;
                    break;
                case AnimationMode.WallOfMotion:
                    currentMode.AddListener(WallOfMotion);
                    break;
            }

            if (StartOnPlay)
                StartAnimation();
        }

        private bool animationRunning;

        public void StartAnimation()
        {
            if (TransformsToActOn.Count < 1)
                return;

            animationRunning = true;

            StartCoroutine(RunAnimation());
        }

        public void StopAnimation()
        {
            animationRunning = false;
        }

        
        private IEnumerator RunAnimation()
        {
            if (animationMode == AnimationMode.RingOfMotion)
                InitializeRing();

            while(animationRunning)
            {
                currentMode?.Invoke();

                yield return null;
            }
        }
        

        private void PositionLerp()
        {

        }

        private void ScaleLerp()
        {

        }

        private void InitializeRing()
        {

        }

        Quaternion startRotation;
        private void RingOfMotion()
        {
            for (int i = 0; i < TransformsToActOn.Count; i++)
            {
                transform.rotation =
                 startRotation *
                    Quaternion.Euler(0, 0, (360 / TransformsToActOn.Count) * i);

                TransformsToActOn[i].position = transform.position + transform.right;
            }
        }

        private void WallOfMotion()
        {

        }
    }
}
