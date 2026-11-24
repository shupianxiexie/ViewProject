using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class spaceHintTrigger : MonoBehaviour
{
    public spaceRemind_UI spaceRemind;
    void OnTriggerEnter()
    {
        spaceRemind.ShowHint();
    }
}
