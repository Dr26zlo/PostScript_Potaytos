using UnityEngine;

[RequireComponent(typeof(InteractiveObject))]
public class TypewriterZoomArea : MonoBehaviour
{
    [Header("Zoom")]
    public CameraMovement cameraScript;
    public Transform focusPoint;
    public Transform backgroundTransform;

    internal TypewriterStation station;
    internal float minimumZoomSize;
    private static TypewriterZoomArea focusedArea;

    internal bool IsFocused => focusedArea == this;

    internal static bool AllowsClick(Transform target)
    {
        if (focusedArea == null) return true;
        Transform machine = focusedArea.station != null ? focusedArea.station.transform : focusedArea.transform;
        return target.IsChildOf(machine) ||
            (focusedArea.station == null && target.GetComponent<TypewriterKey>() != null);
    }

    public bool CanType => isActiveAndEnabled && IsFocused &&
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
        if (IsFocused && cameraScript.CurrentFocus != focusPoint) focusedArea = null;
        if (IsFocused && (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)))
        {
            ExitTypewriterMode();
        }
    }

    public void EnterTypewriterMode()
    {
        if (!isActiveAndEnabled || focusedArea != null || cameraScript == null || !cameraScript.isActiveAndEnabled) return;
        if (cameraScript.HasFocus && cameraScript.CurrentFocus != focusPoint) return;
        focusedArea = this;

        cameraScript.FocusOn(focusPoint, minimumZoomSize);

    }

    public void ExitTypewriterMode()
    {
        if (!IsFocused) return;
        focusedArea = null;
        if (cameraScript != null && cameraScript.isActiveAndEnabled)
            cameraScript.ResetZoom();
    }

    private void OnDisable() => ExitTypewriterMode();
}