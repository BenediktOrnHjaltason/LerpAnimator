using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LerpAnimator : MonoBehaviour
{
    public class Sequence
    {
        //List<Transform>
    }

    [SerializeField] public Transform[] TransformsToActOn;

    [SerializeField] public Transform[] StartTransforms;
}
