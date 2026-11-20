using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class InteractiveObject : MonoBehaviour
{
    [Header("Highlight Settings")]
    public Color normalColor = Color.white;
    public Color hoverColor = new Color(0.8f, 0.8f, 0.8f);

    [Header("Events")]
    public UnityEvent onClick;
    public UnityEvent onMouseEnter;
    public UnityEvent onMouseExit;

    private SpriteRenderer spriteRenderer;
    private bool isHovered = false;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = normalColor;
        }
    }

    void OnMouseEnter()
    {
        isHovered = true;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = hoverColor;
        }
        onMouseEnter?.Invoke();
    }

    void OnMouseExit()
    {
        isHovered = false;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = normalColor;
        }
        onMouseExit?.Invoke();
    }

    internal bool UseAutomaticMouseClicks { get; set; } = true;

    void OnMouseDown()
    {
        if (UseAutomaticMouseClicks) Click();
    }


    public void Click()
    {
        if (isActiveAndEnabled && TypewriterZoomArea.AllowsClick(transform))
            onClick?.Invoke();
    }
    public void ResetHighlight()
    {
        isHovered = false;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = normalColor;
        }
    }
}