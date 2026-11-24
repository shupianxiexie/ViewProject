using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RHintTrigger : MonoBehaviour
{
    public RRemind_UI rRemind;
    void OnTriggerEnter()
    {
        rRemind.ShowHint();
    }
}
