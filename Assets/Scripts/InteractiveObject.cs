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

    private static int pointerFrame=-1,clickFrame=-1;private static InteractiveObject pointerTarget;
    private static InteractiveObject PointerTarget()
    {
        if(pointerFrame==Time.frameCount)return pointerTarget;pointerFrame=Time.frameCount;pointerTarget=null;
        var camera=Camera.main;if(camera==null)return null;
        var ray=camera.ViewportPointToRay(new Vector3(Input.mousePosition.x/Screen.width,Input.mousePosition.y/Screen.height));
        if(!new Plane(Vector3.forward,Vector3.zero).Raycast(ray,out float distance))return null;var point=ray.GetPoint(distance);
        int layer=int.MinValue,order=int.MinValue;
        foreach(var collider in Physics2D.OverlapPointAll(point))
        {
            var item=collider.GetComponent<InteractiveObject>();if(item==null||!item.isActiveAndEnabled)continue;
            var sprite=item.GetComponent<SpriteRenderer>();if(!SpriteHit.Contains(sprite,point))continue;
            int value=SortingLayer.GetLayerValueFromID(sprite.sortingLayerID);
            if(value<layer||(value==layer&&sprite.sortingOrder<order))continue;
            layer=value;order=sprite.sortingOrder;pointerTarget=item;
        }
        return pointerTarget;
    }
    void Update()
    {
        if(WorldBlocked){if(isHovered)ResetHighlight();return;}
        bool over=PointerTarget()==this;if(over&&!isHovered)OnMouseEnter();else if(!over&&isHovered)OnMouseExit();
    }

    void OnMouseEnter()
    {
        if (WorldBlocked || isHovered || PointerTarget()!=this) return;
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
        if(WorldBlocked||clickFrame==Time.frameCount)return;
        var target=PointerTarget();if(target!=null&&target.UseAutomaticMouseClicks){clickFrame=Time.frameCount;target.Click();}
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