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

    [Header("Blur Settings")]
    [Tooltip("Включить/выключить блюр при движении")]
    public bool enableBlur = true;

    [Tooltip("Перетащите сюда RawImage с BlurMaterial")]
    public RawImage blurOverlay;

    [Tooltip("Максимальный размер блюра (в пикселях)")]
    public float maxBlurSize = 3f;

    [Tooltip("Скорость камеры, при которой блюр достигает максимума")]
    public float blurSpeedThreshold = 5f;

    [Tooltip("Скорость затухания блюра (чем больше, тем быстрее исчезает)")]
    public float blurDecaySpeed = 5f;

    private Camera cam;
    private Vector3 velocity = Vector3.zero;
    private Vector3 targetPosition;
    private float minX, maxX, minY, maxY;

    private Material blurMaterial;
    private float currentBlur = 0f;
    private Vector3 lastCameraPos;

    void Start()
    {
        cam = GetComponent<Camera>();

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
            blurMaterial = blurOverlay.material;
            blurMaterial.SetFloat("_BlurSize", 0f);
        }
        else if (enableBlur && blurOverlay == null)
        {
            Debug.LogWarning("Blur включен, но RawImage не назначен. Блюр работать не будет.");
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
        Vector3 mousePos = cam.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;

        Vector3 center = backgroundSprite.position;
        Vector3 desiredPosition = Vector3.Lerp(center, mousePos, mouseFollowStrength);

        targetPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
        targetPosition.y = Mathf.Clamp(desiredPosition.y, minY, maxY);
        targetPosition.z = transform.position.z;

        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime, maxSpeed);

        UpdateBlur();
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

    void OnDrawGizmosSelected()
    {
        if (backgroundSprite == null) return;
        SpriteRenderer sr = backgroundSprite.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(sr.bounds.center, sr.bounds.size);
            if (Application.isPlaying)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(new Vector3((minX + maxX) / 2, (minY + maxY) / 2, 0),
                                    new Vector3(maxX - minX, maxY - minY, 0));
            }
        }
    }
}
