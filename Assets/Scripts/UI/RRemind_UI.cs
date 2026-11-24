using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RRemind_UI : MonoBehaviour
{
    public Animator animator;
    private bool started = false;
    private bool completed = false;
    public void ShowHint()
    {
        if (started) return;
        started = true;
        animator.SetBool("isbegin", true);
    }
    public void HideHint()
    {
        if (!started || completed) return;
        completed = true;
        animator.SetBool("isend", true);
        animator.SetBool("isbegin", false);
    }
}
