using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class EventTest : MonoBehaviour
{
    [SerializeField] TextMeshPro Event1;
    [SerializeField] TextMeshPro Event2;
    [SerializeField] TextMeshPro Event3;
    [SerializeField] TextMeshPro Event4;
    [SerializeField] TextMeshPro Event5;

    public void FireEvent1()
    {
        Event1.enabled = true;
    }

    public void FireEvent2()
    {
        Event2.enabled = true;
    }

    public void FireEvent3()
    {
        Event3.enabled = true;
    }

    public void FireEvent4()
    {
        Event4.enabled = true;
    }

    public void FireEvent5()
    {
        Event5.enabled = true;
    }
}
