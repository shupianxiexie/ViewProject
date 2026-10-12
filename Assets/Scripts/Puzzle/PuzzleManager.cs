using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class PuzzleManager : MonoBehaviour
{
    public static PuzzleManager Instance;
    public int totalPuzzles = 12;
    private List<int> collectPuzzles = new List<int>();
    public bool isPuzzleCompleted = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    public void CollectPuzzle(int puzzleID)
    {
        if (!collectPuzzles.Contains(puzzleID))
        {
            collectPuzzles.Add(puzzleID);
            UnityEngine.Debug.Log("Collected puzzle " + puzzleID + ". Total collected: " + collectPuzzles.Count);/////////////////
            /////////////////////////////
            ///
            CheckPuzzleCount();
        }
    }
    public void CheckPuzzleCount()
    {
        if (collectPuzzles.Count >= totalPuzzles)
        {
            isPuzzleCompleted = true;
            UnityEngine.Debug.Log("All puzzles collected! Puzzle completed!");//////////////////////////
            /////////////////////////////
            ////////////////////////////
        }
    }
    public void CompletePuzzle(int puzzleID)
    {
        if(isPuzzleCompleted)
        {
            return;
        }
        isPuzzleCompleted = true;
        UnityEngine.Debug.Log("Puzzle completed!");
    }
    public bool HasPuzzle(int puzzleID)
    {
        return collectPuzzles.Contains(puzzleID);
    }
}