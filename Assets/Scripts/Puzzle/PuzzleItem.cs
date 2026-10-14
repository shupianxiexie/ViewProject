using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PuzzleItem : InteractableItem
{
    public int puzzleID;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PuzzleManager.Instance.CompletePuzzle(puzzleID);
            Destroy(gameObject);
        }
    }
}
