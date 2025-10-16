using UnityEngine;

public class RoadPiece : MonoBehaviour
{
    [Header("Anchors on this prefab")]
    public Transform startAnchor;
    public Transform endAnchor;

    [Tooltip("אם > 0 משתמשים כאורך ידני; אחרת מחושב מעוגנים/Bounds")]
    public float lengthOverride = 0f;

    public float Length
    {
        get
        {
            if (lengthOverride > 0f) return lengthOverride;
            if (startAnchor && endAnchor) return Vector3.Distance(startAnchor.position, endAnchor.position);
            var r = GetComponentInChildren<Renderer>();
            return r ? r.bounds.size.z : 10f;
        }
    }

    public void SnapStartTo(Transform target)
    {
        if (!target) return;

        if (startAnchor)
        {
            var localPos = startAnchor.localPosition;
            var localRot = startAnchor.localRotation;

            var worldRot = target.rotation * Quaternion.Inverse(localRot);
            var worldPos = target.position - (worldRot * localPos);

            transform.SetPositionAndRotation(worldPos, worldRot);
        }
        else
        {
            transform.SetPositionAndRotation(target.position, target.rotation);
        }
    }

    public void SnapAfter(RoadPiece previous)
    {
        if (previous && previous.endAnchor)
            SnapStartTo(previous.endAnchor);
    }
}
