using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QRemind_UI : MonoBehaviour
{
    public Animator animator;
    public View view;
    private bool started = false;
    private bool completed = false;
    public void ShowHint()
    {
        if (started) return;
        started = true;
        animator.SetBool("isbegin", true);
    }
    void LateUpdate()
    {
        if (!started || completed) return;
        if (view.currentState == View.State.Version2DX)
        {
            completed = true;
            animator.SetBool("isend", true);
            animator.SetBool("isbegin", false);
        }
    }
}