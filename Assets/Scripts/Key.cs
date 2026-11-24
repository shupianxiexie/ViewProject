using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Key : InteractableItem
{
    public GameObject doorGameObject;
    // Start is called before the first frame update
    void Start()
    {
        doorGameObject.GetComponentInChildren<Door>().isLocked = true;
    }
    public override void Interact()
    {
        View view = FindObjectOfType<View>();
        if (view != null && view.IsItemBlockedByBox(transform))
        {
            return;
        }

        //捡起来（未实现）
        doorGameObject.GetComponentInChildren<Door>().isLocked = false;
        Destroy(gameObject);
    }
}
