using System.Collections.Generic;
using UnityEngine;

// Reads only the actual alpha, preserving holes and transparent padding.
public static class SpriteHit
{
 private static readonly Dictionary<Texture2D,Texture2D> readable=new Dictionary<Texture2D,Texture2D>();
 public static bool Contains(SpriteRenderer renderer,Vector3 world,float threshold=.15f)
 {
  if(renderer==null||!renderer.enabled||!renderer.gameObject.activeInHierarchy||renderer.sprite==null)return false;
  var sprite=renderer.sprite;Vector3 local=renderer.transform.InverseTransformPoint(world);
  if(renderer.flipX)local.x=-local.x;if(renderer.flipY)local.y=-local.y;
  Vector2 pixel=(Vector2)local*sprite.pixelsPerUnit+sprite.pivot;
  if(pixel.x<0||pixel.y<0||pixel.x>=sprite.rect.width||pixel.y>=sprite.rect.height)return false;
  var source=Readable(sprite.texture);
  return source.GetPixel(Mathf.FloorToInt(sprite.rect.x+pixel.x),Mathf.FloorToInt(sprite.rect.y+pixel.y)).a>=threshold;
 }
 private static readonly Dictionary<Sprite,Bounds> opaqueBounds=new Dictionary<Sprite,Bounds>();
 public static Bounds OpaqueBounds(Sprite sprite)
 {
  if(opaqueBounds.TryGetValue(sprite,out var bounds))return bounds;
  var texture=Readable(sprite.texture);var pixels=texture.GetPixels32();var rect=sprite.rect;
  int minX=(int)rect.width,minY=(int)rect.height,maxX=0,maxY=0;
  for(int y=0;y<(int)rect.height;y++)for(int x=0;x<(int)rect.width;x++)
   if(pixels[((int)rect.y+y)*texture.width+(int)rect.x+x].a>=38){minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x+1);maxY=Mathf.Max(maxY,y+1);}
  if(minX>=maxX||minY>=maxY)bounds=sprite.bounds;
  else {var min=(new Vector2(minX,minY)-sprite.pivot)/sprite.pixelsPerUnit;var max=(new Vector2(maxX,maxY)-sprite.pivot)/sprite.pixelsPerUnit;bounds=new Bounds((min+max)*.5f,max-min);}
  opaqueBounds[sprite]=bounds;return bounds;
 }
 private static Texture2D Readable(Texture2D texture)
 {
  var source=texture;
  if(!source.isReadable)
  {
   if(!readable.TryGetValue(source,out var copy))
   {
    var target=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
    Graphics.Blit(source,target);RenderTexture.active=target;copy=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);copy.ReadPixels(new Rect(0,0,source.width,source.height),0,0);copy.Apply();RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);readable[source]=copy;
   }
   source=copy;
  }
  return source;
 }
}
