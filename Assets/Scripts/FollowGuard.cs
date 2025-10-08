using System.Collections;
using UnityEngine;

public class FollowGuard : MonoBehaviour
{
    public Animator guardAnimator;
    public Animator dogAnimator;
    public Transform guardTransform;
    public Transform dogTransform;
    public float curDistance;
    public void Jump()
    {
        StartCoroutine(PlayAnim("Guard_jump", "Dog_jump"));
    }

    public void LeftDodge()
    {
        StartCoroutine(PlayAnim("Guard_dodgeLeft", "Dog_Run"));
    }
    public void RightDodge()
    {
        StartCoroutine(PlayAnim("Guard_dodgeRight", "Dog_Run"));
    }
    public void Stumble()
    {
        StopAllCoroutines();
        StartCoroutine(PlayAnim("Guard_grap after", "Dog_Fast Run"));
    }
    public void CaughtPlayer()
    {
        guardAnimator.Play("catch_1");
        dogAnimator.Play("catch");
    }

    private IEnumerator PlayAnim(string anim, string anim2)
    {
        yield return new WaitForSeconds(curDistance / 5f);
        guardAnimator.Play(anim);
        dogAnimator.Play(anim2);
    }
    // Update is called once per frame
    public void Folllow(Vector3 pos, float speed)
    {
        Vector3 position = pos - Vector3.forward * curDistance;
        guardTransform.position = Vector3.Lerp(guardTransform.position, position, Time.deltaTime * speed / curDistance);
        dogTransform.position = guardTransform.position;
    }
}
