using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemGetAbility2DX : InteractableItem
{
    public GameObject playergameObject;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public override void Interact()
    {
        playergameObject.GetComponent<PlayerController>().turnTo2DX = true;
        Destroy(gameObject);
    }
}
