using System.Collections;
using System.Collections.Generic;
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
    private Vector3 targetPosition;
    private float minX, maxX, minY, maxY;

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
        targetOrthoSize = defaultZoom;
        cam.orthographicSize = defaultZoom;

        if (backgroundSprite == null)
        {
            Debug.LogError("Background Sprite не назначен!");
            enabled = false;
            return;
        }

        CalculateBounds();
        lastCameraPos = transform.position;

        if (enableBlur && blurOverlay != null && blurOverlay.material != null)
        {
            blurMaterial = new Material(blurOverlay.material);
            blurOverlay.material = blurMaterial;
            blurMaterial.SetFloat("_BlurSize", 0f);
        }
    }

    void CalculateBounds()
    {
        SpriteRenderer sr = backgroundSprite.GetComponent<SpriteRenderer>();
        if (sr == null) { enabled = false; return; }

        Bounds bounds = sr.bounds;
        float camHalfHeight = cam.orthographicSize;
        float camHalfWidth = cam.orthographicSize * cam.aspect;

        minX = bounds.min.x + camHalfWidth;
        maxX = bounds.max.x - camHalfWidth;
        minY = bounds.min.y + camHalfHeight;
        maxY = bounds.max.y - camHalfHeight;

        if (minX > maxX) { minX = maxX = bounds.center.x; }
        if (minY > maxY) { minY = maxY = bounds.center.y; }
    }

    void LateUpdate()
    {

        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetOrthoSize, Time.deltaTime * zoomSpeed);

        CalculateBounds();
        Vector3 finalTargetPos;

        if (isZooming && zoomTarget != null)
        {
            finalTargetPos = new Vector3(zoomTarget.position.x, zoomTarget.position.y, transform.position.z);
        }
        else
        {
            Vector3 mousePos = cam.ViewportToWorldPoint(new Vector3(Input.mousePosition.x / Screen.width, Input.mousePosition.y / Screen.height, -transform.position.z));
            mousePos.z = 0f;
            Vector3 center = backgroundSprite.position;
            Vector3 desiredPosition = Vector3.Lerp(center, mousePos, mouseFollowStrength);

            finalTargetPos.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
            finalTargetPos.y = Mathf.Clamp(desiredPosition.y, minY, maxY);
            finalTargetPos.z = transform.position.z;
        }

        transform.position = Vector3.SmoothDamp(transform.position, finalTargetPos, ref velocity, smoothTime, maxSpeed);
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
        if (!isActiveAndEnabled || cam == null || !isZooming || target == null || zoomTarget != target)
            return false;

        const float tolerance = 0.02f;
        Vector2 offset = (Vector2)transform.position - (Vector2)target.position;
        return offset.sqrMagnitude <= tolerance * tolerance &&
            Mathf.Abs(cam.orthographicSize - targetOrthoSize) <= tolerance;
    }

    public void ResetZoom()
    {
        isZooming = false;
        zoomTarget = null;
        targetOrthoSize = defaultZoom;
        CalculateBounds();
    }

    void OnDestroy()
    {
        if (blurMaterial != null) Destroy(blurMaterial);
    }

    void UpdateBlur()
    {
        if (!enableBlur || blurMaterial == null) return;

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