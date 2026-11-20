using UnityEngine;

[RequireComponent(typeof(InteractiveObject))]
public class TypewriterZoomArea : MonoBehaviour
{
    public CameraMovement cameraScript;
    public Transform focusPoint;
    public Transform backgroundTransform;

    internal TypewriterStation station;
    internal float minimumZoomSize;
    private bool isFocused = false;

    internal bool IsFocused => isFocused;

    public bool CanType => isActiveAndEnabled && isFocused &&
        cameraScript != null && cameraScript.HasReachedFocus(focusPoint);

    void Start()
    {
        if (cameraScript == null && Camera.main != null)
            cameraScript = Camera.main.GetComponent<CameraMovement>();

        if (focusPoint == null)
            focusPoint = transform;

        if (station == null)
            GetComponent<InteractiveObject>().onClick?.AddListener(EnterTypewriterMode);
    }

    void Update()
    {
        if (isFocused && cameraScript.CurrentFocus != focusPoint) isFocused = false;
        if (isFocused && (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)))
        {
            ExitTypewriterMode();
        }
    }

    public void EnterTypewriterMode()
    {
        if (isFocused || cameraScript == null || !cameraScript.isActiveAndEnabled) return;
        if (cameraScript.HasFocus && cameraScript.CurrentFocus != focusPoint) return;
        isFocused = true;

        cameraScript.FocusOn(focusPoint, minimumZoomSize);

    }

    public void ExitTypewriterMode()
    {
        if (!isFocused) return;
        isFocused = false;

        cameraScript.ResetZoom();

    }
}