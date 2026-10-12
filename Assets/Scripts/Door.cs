using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Door : InteractableItem
{
    public string nextSceneName;
    public bool isLocked = false;
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
        if (!isLocked)
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.Log("The gate is locked.");
        }
    }
}
