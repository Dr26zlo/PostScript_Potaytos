using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public sealed class GuestDoor : MonoBehaviour
{
    [SerializeField] private Transform threshold;
    [SerializeField] private GameObject openDoor;
    [SerializeField] private GameObject closedDoor;
    [SerializeField, Range(0.1f, 1f)] private float guestScale = 0.48f;

    public bool IsWaiting { get; private set; }
    private AudioSource knock;
    public IEnumerator WaitForPlayer()
    {
        SetOpen(false);
        IsWaiting = true;
        if (knock == null) { knock = gameObject.AddComponent<AudioSource>(); knock.playOnAwake = false; }
        knock.clip = Resources.Load<AudioClip>("RoomAudio/DoorKnock");
        if (knock.clip != null) knock.Play();
        while (IsWaiting) yield return null;
    }
    public void OpenForWaitingGuest() { if (IsWaiting) IsWaiting = false; }
    private void Update()
    {
        if (DemoDirector.Instance != null && DemoDirector.Instance.Mode != "room") return;
        if (!IsWaiting || !Input.GetMouseButtonDown(0)) return;
        var camera = Camera.main;
        if (camera == null) return;
        var move = camera.GetComponent<CameraMovement>();
        if (move != null && move.HasFocus) return;
        var ray = camera.ViewportPointToRay(new Vector3(Input.mousePosition.x / Screen.width, Input.mousePosition.y / Screen.height));
        var plane = new Plane(Vector3.forward, transform.position);
        if (!plane.Raycast(ray, out float distance)) return;
        var point = ray.GetPoint(distance);
        foreach (var sprite in closedDoor.GetComponentsInChildren<SpriteRenderer>())
        {
            var b = sprite.bounds;
            if (SpriteHit.Contains(sprite,point))
                OpenForWaitingGuest();
        }
    }

    internal float GuestScale => guestScale;
    internal Vector3 Threshold => threshold != null ? threshold.position : transform.position;

    internal void SetOpen(bool open)
    {
        if (closedDoor != null) closedDoor.SetActive(!open);
        if (openDoor != null) openDoor.SetActive(open);
    }
}
