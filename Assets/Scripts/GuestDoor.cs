using UnityEngine;

[DisallowMultipleComponent]
public sealed class GuestDoor : MonoBehaviour
{
    [SerializeField] private Transform threshold;
    [SerializeField] private GameObject openDoor;
    [SerializeField] private GameObject closedDoor;
    [SerializeField, Range(0.1f, 1f)] private float guestScale = 0.48f;

    internal float GuestScale => guestScale;
    internal Vector3 Threshold => threshold != null ? threshold.position : transform.position;

    internal void SetOpen(bool open)
    {
        if (closedDoor != null) closedDoor.SetActive(!open);
        if (openDoor != null) openDoor.SetActive(open);
    }
}
