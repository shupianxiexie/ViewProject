using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Key : InteractableItem
{
    public GameObject doorGameObject;
    // Start is called before the first frame update
    void Start()
    {
        doorGameObject.GetComponent<Door>().isLocked = true;
    }
    public override void Interact()
    {
        // Add your interaction logic here
        //捡起来（未实现）
        doorGameObject.GetComponent<Door>().isLocked = false;
        Destroy(gameObject);
    }
}
