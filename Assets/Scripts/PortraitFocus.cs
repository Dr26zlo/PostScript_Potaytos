using UnityEngine;

// A face anchor shared by every dialogue path, with opaque-footprint pose alignment.
public sealed class PortraitFocus : MonoBehaviour
{
 private Transform anchor;private SpriteRenderer portrait;
 private static PortraitFocus Component(Transform actor)
 {
  var component=actor.GetComponent<PortraitFocus>();
  if(component==null)component=actor.gameObject.AddComponent<PortraitFocus>();
  component.Ensure();return component;
 }
 public static Transform For(Transform actor)=>Component(actor).anchor;
 public static void SetSprite(Transform actor,Sprite sprite)=>Component(actor).Apply(sprite);
 private Bounds WorldBounds()
 {
  var bounds=SpriteHit.OpaqueBounds(portrait.sprite);
  Vector3 min=transform.TransformPoint(bounds.min),max=transform.TransformPoint(bounds.max);
  return new Bounds((min+max)*.5f,new Vector3(Mathf.Abs(max.x-min.x),Mathf.Abs(max.y-min.y),0));
 }
 private void Ensure()
 {
  if(anchor!=null)return;
  portrait=GetComponent<SpriteRenderer>();anchor=new GameObject("Фокус лица").transform;anchor.SetParent(transform,false);
  if(portrait!=null)
  {
   var sprite=portrait.sprite;string actor=sprite.name.Split('-')[0];
   // Coordinates identify the face, rather than a staff, hat or other tall prop.
   Vector2 uv=actor=="lantern"?new Vector2(.49f,.70f):actor=="conductor"?new Vector2(.43f,.75f):actor=="keeper"?new Vector2(.50f,.84f):actor=="restorer"?new Vector2(.48f,.74f):actor=="imp"?new Vector2(.64f,.76f):new Vector2(.5f,.78f);
   anchor.localPosition=(Vector2.Scale(uv,sprite.rect.size)-sprite.pivot)/sprite.pixelsPerUnit;
  }
 }
 private void Apply(Sprite sprite)
 {
  if(portrait==null||sprite==null||portrait.sprite==sprite)return;
  Vector3 focus=anchor.position;var before=WorldBounds();portrait.sprite=sprite;var after=WorldBounds();
  if(after.size.y>0)transform.localScale*=before.size.y/after.size.y;
  after=WorldBounds();transform.position+=new Vector3(before.center.x-after.center.x,before.min.y-after.min.y,0);
  anchor.position=focus;
 }
}
