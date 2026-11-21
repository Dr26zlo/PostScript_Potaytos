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
        dialogueRunner = FindObjectOfType<DialogueRunner>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = normalColor;
        }
    }

    private DialogueRunner dialogueRunner;
    private bool WorldBlocked => DemoDirector.Instance != null &&
        (DemoDirector.Instance.Mode != "room" || (dialogueRunner != null && dialogueRunner.IsDialogueActive) || (RoomStationsController.Instance != null && RoomStationsController.Instance.IsOpen));

    void Update() { if (isHovered && WorldBlocked) ResetHighlight(); }

    void OnMouseEnter()
    {
        if (WorldBlocked) return;
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
        if (!WorldBlocked && isActiveAndEnabled && TypewriterZoomArea.AllowsClick(transform))
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