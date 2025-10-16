using UnityEngine;

public class PoliceChaser : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;
    public Animator animator;

    [Header("Chase Settings")]
    public float followDistance = 3f;
    public float lateralOffset = 0f;
    public float heightOffset = 0f;
    public float moveSpeed = 10f;
    public float rotateSpeed = 10f;

    [Header("Start Placement")]
    public bool snapToPlayerOnStart = true;        
    public bool alignToGround = true;               
    public LayerMask groundMask = ~0;             
    public float groundRaycastUp = 2f;               
    public float groundRaycastDown = 10f;           

    [Header("Animation")]
    public string runBoolParam = "IsChasing";

    private bool _isChasing = false;
    private Vector3 _vel;

    void Awake()
    {
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
        if (!animator) animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (!_isChasing || !player) return;

        var back   = -player.forward * followDistance;
        var side   =  player.right   * lateralOffset;
        var upVec  =  Vector3.up     * heightOffset;
        var target = player.position + back + side + upVec;

        transform.position = Vector3.SmoothDamp(transform.position, target, ref _vel, 0.05f, moveSpeed);

        var look = player.position - transform.position; look.y = 0f;
        if (look.sqrMagnitude > 0.0001f)
        {
            var rot = Quaternion.LookRotation(look, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, rotateSpeed * Time.deltaTime);
        }
    }

    public void StartChase()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        if (snapToPlayerOnStart && player)
        {
            var back   = -player.forward * followDistance;
            var side   =  player.right   * lateralOffset;
            var target = player.position + back + side;

            if (alignToGround)
            {
                var rayOrigin = target + Vector3.up * groundRaycastUp;
                if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, groundRaycastDown + groundRaycastUp, groundMask))
                {
                    target.y = hit.point.y + heightOffset;
                }
                else
                {
                    target.y = player.position.y + heightOffset;
                }
            }
            else
            {
                target.y = player.position.y + heightOffset;
            }

            transform.position = target;
            transform.rotation = Quaternion.LookRotation(player.forward, Vector3.up);
        }

        _isChasing = true;
        if (animator && !string.IsNullOrEmpty(runBoolParam))
            animator.SetBool(runBoolParam, true);
    }

    public void StopChase()
    {
        _isChasing = false;
        if (animator && !string.IsNullOrEmpty(runBoolParam))
            animator.SetBool(runBoolParam, false);
    }
}
