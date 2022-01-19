using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpheroidGames.SineAnimator
{
    public enum AnimationMode
    {
        Position,
        Scale,
        RingOfMotion,
        WallOfMotion
    }

    public enum ValueMode
    {
        Value,
        AbsoluteValue
    }

    [System.Serializable]
    public class SineAnimator : MonoBehaviour
    {
        [Tooltip("Wether sequence should start when game starts")]
        [SerializeField] bool StartOnPlay;

        [Tooltip("The transforms that will be affected by this Lerp Animator")]

        [SerializeField] List<Transform> TransformsToActOn;

        [SerializeField] AnimationMode animationMode;

        [SerializeField] ValueMode valueMode;


        private void Start()
        {
            if (StartOnPlay)
                StartAnimation();
        }

        public void StartAnimation()
        {
            //StartCoroutine(RunAnimation());
        }

        /*
        private IEnumerator RunAnimation()
        {

            
        }
        */
    }
}
