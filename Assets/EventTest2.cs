using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpheroidGames.LerpAnimator;

public class EventTest2 : MonoBehaviour
{
    [SerializeField] List<MeshRenderer> renderers; 

    // Start is called before the first frame update
    void Start()
    {
        LerpAnimator le = GetComponent<LerpAnimator>();

        if (le)
            le.PlaySequence("Spread 2");
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void TurnOnText()
    {
        foreach (var renderer in renderers)
        {
            renderer.enabled = true;
        }
    }
}
