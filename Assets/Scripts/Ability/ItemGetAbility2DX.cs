using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemGetAbility2DX : InteractableItem
{
    public GameObject playergameObject;
    public QRemind_UI qRemind;
    public RRemind_UI rRemind;

    public override void Interact()
    {
        playergameObject.GetComponent<PlayerController>().turnTo2DX = true;
        Destroy(gameObject);
        qRemind.ShowHint();
        rRemind.HideHint();
    }
}
