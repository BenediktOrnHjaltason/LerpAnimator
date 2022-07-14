using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EventTest2 : MonoBehaviour
{
    [SerializeField] List<MeshRenderer> renderers; 

    // Start is called before the first frame update
    void Start()
    {
        
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
