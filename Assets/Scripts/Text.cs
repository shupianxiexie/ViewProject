using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Text : MonoBehaviour
{
    Animator animator;
    bool isWrong = false;

    void Start()
    {
        animator = GetComponent<Animator>();
    }
    public void PlayWrong()
    {
        StopAllCoroutines();
        StartCoroutine(PlayWrongOnce());
    }
    IEnumerator PlayWrongOnce()
    {
        isWrong = true;
        animator.SetBool("IsWrong", isWrong);
        yield return new WaitForSeconds(2.5f);
        isWrong = false;
        animator.SetBool("IsWrong", false);
    }
}