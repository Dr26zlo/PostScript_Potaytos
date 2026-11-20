using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
public class CameraMovement : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform backgroundSprite;

    [Header("Follow Settings")]
    public float smoothTime = 0.3f;
    public float maxSpeed = Mathf.Infinity;
    [Range(0f, 1f)]
    public float mouseFollowStrength = 1f;

    [Header("Zoom Settings")]
    public float defaultZoom = 5f;
    public float zoomedInSize = 2.5f;
    public float zoomSpeed = 3f;

    [Header("Blur Settings")]
    public bool enableBlur = true;
    public RawImage blurOverlay;
    public float maxBlurSize = 3f;
    public float blurSpeedThreshold = 5f;
    public float blurDecaySpeed = 5f;

    private Camera cam;
    private Vector3 velocity = Vector3.zero;
    private SpriteRenderer backgroundRenderer;

    private Material blurMaterial;
    private float currentBlur = 0f;
    private Vector3 lastCameraPos;

    private Transform zoomTarget = null;
    private float targetOrthoSize;
    private bool isZooming = false;
    public Transform CurrentFocus => zoomTarget;
    public bool HasFocus => isZooming && zoomTarget != null;

    void Start()
    {
        cam = GetComponent<Camera>();
        backgroundRenderer = backgroundSprite != null ? backgroundSprite.GetComponent<SpriteRenderer>() : null;
        if (backgroundRenderer == null)
        {
            Debug.LogError("Назначьте фон со SpriteRenderer для камеры.", this);
            enabled = false;
            return;
        }

        targetOrthoSize = defaultZoom;
        Bounds bounds = backgroundRenderer.bounds;
        cam.orthographicSize = Mathf.Min(defaultZoom, MaxOrthoSize(bounds, cam.aspect));
        transform.position = ClampToBackground(transform.position, bounds, cam.orthographicSize, cam.aspect);
        lastCameraPos = transform.position;

        if (enableBlur && blurOverlay != null && blurOverlay.material != null)
        {
            blurMaterial = new Material(blurOverlay.material);
            blurOverlay.material = blurMaterial;
            blurMaterial.SetFloat("_BlurSize", 0f);
        }
    }

    private static float MaxOrthoSize(Bounds bounds, float aspect) =>
        Mathf.Min(bounds.extents.y, bounds.extents.x / aspect);

    private static Vector3 ClampToBackground(Vector3 position, Bounds bounds, float size, float aspect)
    {
        float rangeX = Mathf.Max(0f, bounds.extents.x - size * aspect);
        float rangeY = Mathf.Max(0f, bounds.extents.y - size);
        position.x = Mathf.Clamp(position.x, bounds.center.x - rangeX, bounds.center.x + rangeX);
        position.y = Mathf.Clamp(position.y, bounds.center.y - rangeY, bounds.center.y + rangeY);
        return position;
    }

    void LateUpdate()
    {
        if (backgroundRenderer == null) return;

        Bounds bounds = backgroundRenderer.bounds;
        float aspect = cam.aspect;
        float maxSize = MaxOrthoSize(bounds, aspect);
        float requestedSize = isZooming && zoomTarget != null ? targetOrthoSize : defaultZoom;
        float targetSize = Mathf.Min(requestedSize, maxSize);
        cam.orthographicSize = Mathf.Min(maxSize,
            Mathf.Lerp(cam.orthographicSize, targetSize, Time.deltaTime * zoomSpeed));

        Vector3 desired = isZooming && zoomTarget != null
            ? zoomTarget.position
            : Vector3.Lerp(bounds.center, cam.ViewportToWorldPoint(new Vector3(Input.mousePosition.x / Screen.width, Input.mousePosition.y / Screen.height, -transform.position.z)), mouseFollowStrength);
        desired.z = transform.position.z;
        Vector3 target = ClampToBackground(desired, bounds, cam.orthographicSize, aspect);
        Vector3 next = Vector3.SmoothDamp(transform.position, target, ref velocity, smoothTime, maxSpeed);
        Vector3 clamped = ClampToBackground(next, bounds, cam.orthographicSize, aspect);
        if (next.x != clamped.x) velocity.x = 0f;
        if (next.y != clamped.y) velocity.y = 0f;
        transform.position = clamped;

        UpdateBlur();
    }


    public void FocusOn(Transform target)
    {
        FocusOn(target, 0f);
    }

    public void FocusOn(Transform target, float minimumZoomSize)
    {
        if (target == null) return;
        zoomTarget = target;
        isZooming = true;
        targetOrthoSize = Mathf.Max(zoomedInSize, minimumZoomSize);
    }

    public bool HasReachedFocus(Transform target)
    {
        if (!isActiveAndEnabled || cam == null || backgroundRenderer == null || !isZooming || target == null || zoomTarget != target)
            return false;

        const float tolerance = 0.02f;
        Bounds bounds = backgroundRenderer.bounds;
        float targetSize = Mathf.Min(targetOrthoSize, MaxOrthoSize(bounds, cam.aspect));
        Vector3 clampedTarget = ClampToBackground(target.position, bounds, targetSize, cam.aspect);
        Vector2 offset = (Vector2)transform.position - (Vector2)clampedTarget;
        return offset.sqrMagnitude <= tolerance * tolerance &&
            Mathf.Abs(cam.orthographicSize - targetSize) <= tolerance;
    }

    public void ResetZoom()
    {
        isZooming = false;
        zoomTarget = null;
        targetOrthoSize = defaultZoom;
    }

    void OnDestroy()
    {
        if (blurMaterial != null) Destroy(blurMaterial);
    }

    void UpdateBlur()
    {
        if (!enableBlur || blurMaterial == null || Time.deltaTime <= 0f) return;

        float cameraSpeed = (transform.position - lastCameraPos).magnitude / Time.deltaTime;
        lastCameraPos = transform.position;

        float targetBlur = 0f;
        if (cameraSpeed > 0.01f)
        {
            targetBlur = Mathf.Clamp01(cameraSpeed / blurSpeedThreshold) * maxBlurSize;
        }

        float lerpSpeed = targetBlur > currentBlur ? 15f : blurDecaySpeed;
        currentBlur = Mathf.Lerp(currentBlur, targetBlur, Time.deltaTime * lerpSpeed);

        blurMaterial.SetFloat("_BlurSize", currentBlur);
    }

    public void SetBlurEnabled(bool state)
    {
        enableBlur = state;
        if (!state && blurMaterial != null)
        {
            currentBlur = 0f;
            blurMaterial.SetFloat("_BlurSize", 0f);
        }
    }
}