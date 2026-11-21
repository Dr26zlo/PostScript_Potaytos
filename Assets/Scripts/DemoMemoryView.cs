using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class DemoMemoryView : MonoBehaviour
{
 private sealed class Piece { public Rect rect;public int row,col,group;public string text;public Texture2D paper;public Sprite sprite; }
 private sealed class Junk { public Rect rect;public Sprite sprite; }
 private sealed class State {public List<Piece> pieces=new List<Piece>();public List<Junk> junk=new List<Junk>();public bool complete;}
 private sealed class Visual {public RectTransform rect;public Image image,shadow;public bool junk;public int index;public float lift;}
 private readonly Dictionary<string,State> states=new Dictionary<string,State>();
 private readonly Dictionary<Texture2D,List<Vector2>> contours=new Dictionary<Texture2D,List<Vector2>>();
 private readonly List<Visual> visuals=new List<Visual>();
 private DemoPuzzle puzzle;private State state;private Action closed;private AudioSource audioSource;
 private GameObject ui;private RectTransform container,maskRoot;private Text status;
 private int drag=-1;private bool junkDrag;private Vector2 offset;private Sprite ovalMask;
 private static readonly Rect Surface=new Rect(80,70,1120,580);
 public bool IsOpen=>puzzle!=null;
 private void Awake(){audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;}
 public void Open(DemoPuzzle value,Action onClose)
 {
  puzzle=value;closed=onClose;
  if(!states.TryGetValue(value.id,out state))
  {
   state=new State();states[value.id]=state;var rng=new System.Random(StableHash(value.id));
   for(int row=0;row<value.rows.Length;row++)for(int col=0;col<value.rows[row].parts.Length;col++)
   {
    var paper=MakePaper(row,col);var p=new Piece{row=row,col=col,group=state.pieces.Count,text=value.rows[row].parts[col],paper=paper,sprite=Sprite.Create(paper,new Rect(0,0,paper.width,paper.height),new Vector2(.5f,.5f))};
    p.rect=new Rect(440+rng.Next(190),260+rng.Next(80),190,82);state.pieces.Add(p);
   }
   string[] names={"postage-stamp","button","key","ribbon","frame","lit-window","button","ribbon"};
   if(value.mode=="memory")for(int i=0;i<names.Length;i++)
   {
    var sprite=Resources.Load<Sprite>("Demo/Memory/"+names[i]);if(sprite==null)continue;
    var p=state.pieces[i%state.pieces.Count];float aspect=sprite.rect.width/sprite.rect.height;
    var size=aspect>1?new Vector2(190,190/aspect):new Vector2(160*aspect,160);
    state.junk.Add(new Junk{sprite=sprite,rect=new Rect(p.rect.position-new Vector2(12,12),size)});
   }
  }
  BuildView();
 }
 private static int StableHash(string value){unchecked{int hash=17;foreach(char c in value)hash=hash*31+c;return hash;}}
 private Texture2D MakePaper(int row,int col)
 {
  const int w=240,h=100;var tex=new Texture2D(w,h);tex.filterMode=FilterMode.Bilinear;
  for(int y=0;y<h;y++)for(int x=0;x<w;x++)
  {
   int left=col==0?4:Profile(y,row,col),right=Profile(y,row,col+1);
   bool solid=x>=left&&x<w-16+right&&y>3+(x%17==0?2:0)&&y<h-4;
   float grain=((x*17+y*31)%23)/600f;tex.SetPixel(x,y,solid?new Color(.85f+grain,.79f+grain,.65f+grain):Color.clear);
  }
  tex.Apply();return tex;
 }
 private static int Profile(int y,int row,int boundary)=>(int)(8+5*Mathf.Sin(y*(.12f+boundary*.025f)+row*2+boundary)+2*Mathf.Sin(y*.47f+boundary));
 private static RectTransform RectObject(string name,Transform parent,Rect area)
 {
  var obj=new GameObject(name,typeof(RectTransform));var rect=(RectTransform)obj.transform;rect.SetParent(parent,false);
  rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(area.x,-area.y);rect.sizeDelta=area.size;return rect;
 }
 private Image Picture(string name,Transform parent,Rect area,Sprite sprite,Color color,bool raycast=false)
 {
  var rect=RectObject(name,parent,area);var image=rect.gameObject.AddComponent<Image>();image.sprite=sprite;image.color=color;image.raycastTarget=raycast;return image;
 }
 private Text Label(string name,Transform parent,Rect area,string text,int size,Color color)
 {
  var rect=RectObject(name,parent,area);var label=rect.gameObject.AddComponent<Text>();label.font=RoomReadableUi.Font;label.fontSize=size;label.color=color;label.text=text;label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;return label;
 }
 private void BuildView()
 {
  if(ui!=null)Destroy(ui);visuals.Clear();
  ui=new GameObject("Память — маска и предметы",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
  var canvas=ui.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=2000;
  var scaler=ui.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
  var bg=Picture("Затемнение",ui.transform,new Rect(0,0,1280,720),null,new Color(0,0,0,.82f),true);
  bg.rectTransform.anchorMin=Vector2.zero;bg.rectTransform.anchorMax=Vector2.one;bg.rectTransform.offsetMin=bg.rectTransform.offsetMax=Vector2.zero;
  container=RectObject("Область",ui.transform,new Rect(0,0,1280,720));container.anchorMin=container.anchorMax=new Vector2(.5f,.5f);container.pivot=new Vector2(.5f,.5f);container.anchoredPosition=Vector2.zero;
  var oval=Resources.Load<Sprite>("Demo/Memory/white-oval");
  Picture("Первый слой — белый овал",container,Surface,puzzle.mode=="memory"?oval:null,puzzle.mode=="memory"?Color.white:new Color(.32f,.24f,.16f));
  maskRoot=RectObject("Маска белой области",container,Surface);var maskImage=maskRoot.gameObject.AddComponent<Image>();
  if(puzzle.mode=="memory")
  {
   maskImage.sprite=oval;
  }
  maskRoot.gameObject.AddComponent<Mask>().showMaskGraphic=false;
  for(int i=0;i<state.pieces.Count;i++)AddVisual(i,false,state.pieces[i].sprite);
  for(int i=0;i<state.junk.Count;i++)AddVisual(i,true,state.junk[i].sprite);
  // The contour is deliberately above every thing and fragment, never a hit target.
  if(puzzle.mode=="memory")Picture("Последний слой — графитовый контур",container,Surface,Resources.Load<Sprite>("Demo/Memory/graphite-rim"),Color.white);
  Label("Название",container,new Rect(240,12,800,45),puzzle.title,24,Color.white);
  status=Label("Подсказка",container,new Rect(160,655,960,55),puzzle.hint,22,Color.white);
  var runner=FindObjectOfType<DialogueRunner>();var button=Instantiate(runner.choiceButtonPrefab,container);button.name="Назад к клиенту";
  var r=(RectTransform)button.transform;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(20,-15);r.sizeDelta=new Vector2(260,66);
  button.GetComponentInChildren<Text>().text="Назад к клиенту";RoomReadableUi.ConfigureChoice(button);button.GetComponent<Button>().onClick.AddListener(Close);
  SyncVisuals(true);
 }
 private void AddVisual(int index,bool junk,Sprite sprite)
 {
  var shadow=Picture("Тень предмета",maskRoot,new Rect(),sprite,new Color(0,0,0,.18f));
  var image=Picture(junk?"Вещь":"Обрывок",maskRoot,new Rect(),sprite,Color.white,true);image.alphaHitTestMinimumThreshold=.12f;
  var handle=image.gameObject.AddComponent<MemoryDragHandle>();handle.owner=this;handle.index=index;handle.junk=junk;
  if(!junk)Label("Слова",image.transform,new Rect(12,9,166,64),state.pieces[index].text,22,RoomReadableUi.Ink);
  visuals.Add(new Visual{rect=image.rectTransform,image=image,shadow=shadow,junk=junk,index=index});
 }
 private Rect ItemRect(Visual v)=>v.junk?state.junk[v.index].rect:state.pieces[v.index].rect;
 private void Update(){if(IsOpen)SyncVisuals(false);}
 private void SyncVisuals(bool immediate)
 {
  foreach(var v in visuals)
  {
   bool held=drag>=0 && (junkDrag?v.junk&&v.index==drag:!v.junk&&state.pieces[v.index].group==state.pieces[drag].group);
   v.lift=immediate?0:Mathf.MoveTowards(v.lift,held?1:0,Time.unscaledDeltaTime*12);
   var area=ItemRect(v);var local=area.position-Surface.position;Vector2 lifted=local-new Vector2(0,4*v.lift);
   v.rect.anchoredPosition=new Vector2(lifted.x,-lifted.y);v.rect.sizeDelta=area.size;v.rect.localScale=Vector3.one*(1+.018f*v.lift);
   v.shadow.rectTransform.anchoredPosition=new Vector2(local.x+3+3*v.lift,-local.y-4-3*v.lift);v.shadow.rectTransform.sizeDelta=area.size;v.shadow.color=new Color(0,0,0,.12f+.15f*v.lift);
  }
  if(status!=null)status.color=new Color(1,1,1,RoomReadableUi.HintAlpha);
  if(status!=null)status.text=state.complete?(puzzle.id=="letter_daughter"?"Но здесь она ждёт… Строка восстановлена.":"Строки восстановлены. Вернитесь к клиенту."):puzzle.hint;
 }
 private Vector2 Pointer(PointerEventData e){RectTransformUtility.ScreenPointToLocalPointInRectangle(container,e.position,null,out Vector2 local);return new Vector2(local.x+640,360-local.y);}
 public void BeginDrag(int index,bool junk,PointerEventData e)
 {
  drag=index;junkDrag=junk;var area=junk?state.junk[index].rect:state.pieces[index].rect;offset=Pointer(e)-area.position;
  foreach(var visual in visuals)if(junk?visual.junk&&visual.index==index:!visual.junk&&state.pieces[visual.index].group==state.pieces[index].group){visual.shadow.transform.SetAsLastSibling();visual.rect.SetAsLastSibling();}
 }
 public void Drag(PointerEventData e)
 {
  if(drag<0)return;Rect area=junkDrag?state.junk[drag].rect:state.pieces[drag].rect;Vector2 delta=Pointer(e)-offset-area.position;
  delta=ConstrainDelta(drag,junkDrag,delta);
  if(junkDrag){var item=state.junk[drag];item.rect.position+=delta;}else{int group=state.pieces[drag].group;foreach(var p in state.pieces)if(p.group==group)p.rect.position+=delta;}
 }
 public void EndDrag(){if(drag<0)return;if(!junkDrag)Snap(state.pieces[drag]);drag=-1;}
 private List<Vector2> Contour(Texture2D texture)
 {
  if(contours.TryGetValue(texture,out var points))return points;
  points=new List<Vector2>();var pixels=texture.GetPixels32();int w=texture.width,h=texture.height;
  for(int y=0;y<h;y++)
  {
   int left=0,right=w-1;while(left<w&&pixels[y*w+left].a<30)left++;while(right>=left&&pixels[y*w+right].a<30)right--;
   if(left>right)continue;
   points.Add(new Vector2(left/(float)w,1-y/(float)h));points.Add(new Vector2((right+1f)/w,1-y/(float)h));
   points.Add(new Vector2(left/(float)w,1-(y+1f)/h));points.Add(new Vector2((right+1f)/w,1-(y+1f)/h));
  }
  contours[texture]=points;return points;
 }
 private bool Inside(Rect area,Texture2D texture,Vector2 delta)
 {
  foreach(var uv in Contour(texture))
  {
   Vector2 p=area.position+Vector2.Scale(uv,area.size)+delta;
   if(puzzle.mode=="memory") {float x=(p.x-640)/450f,y=(p.y-360)/210f;if(x*x+y*y>1)return false;}
   else if(p.x<95||p.x>1185||p.y<85||p.y>630)return false;
  }
  return true;
 }
 private bool Allowed(int index,bool junk,Vector2 delta)
 {
  if(junk)return Inside(state.junk[index].rect,state.junk[index].sprite.texture,delta);
  int group=state.pieces[index].group;foreach(var p in state.pieces)if(p.group==group&&!Inside(p.rect,p.paper,delta))return false;return true;
 }
 private Vector2 ConstrainDelta(int index,bool junk,Vector2 delta)
 {
  if(Allowed(index,junk,delta))return delta;float min=0,max=1;
  for(int i=0;i<14;i++){float mid=(min+max)/2;if(Allowed(index,junk,delta*mid))min=mid;else max=mid;}
  return delta*min;
 }
 private void Snap(Piece selectedPiece){int group=selectedPiece.group;foreach(var edge in state.pieces)if(edge.group==group&&TrySnap(edge))return;}
 private bool TrySnap(Piece piece)
 {
  foreach(var other in state.pieces)
  {
   if(other==piece||other.group==piece.group||other.row!=piece.row||Mathf.Abs(other.col-piece.col)!=1)continue;
   Vector2 expected=other.rect.position+Vector2.right*((piece.col-other.col)*(224f/240f*190f));
   Vector2 delta=expected-piece.rect.position;if(delta.magnitude>48)continue;
   if(!Allowed(state.pieces.IndexOf(piece),false,delta))continue;
   int old=piece.group,target=other.group;foreach(var member in state.pieces)if(member.group==old){member.rect.position+=delta;member.group=target;}
   var sound=Resources.Load<AudioClip>("RoomStations/Paper");if(sound!=null)audioSource.PlayOneShot(sound,.5f);
   bool complete=true;foreach(var first in state.pieces)foreach(var part in state.pieces)if(first.row==part.row&&first.group!=part.group)complete=false;
   if(complete){state.complete=true;DemoDirector.Instance.CompletePuzzle(puzzle);}return true;
  }
  return false;
 }
 public void Close(){if(ui!=null)Destroy(ui);puzzle=null;drag=-1;var callback=closed;closed=null;callback?.Invoke();}
 private void OnDestroy(){if(ui!=null)Destroy(ui);foreach(var s in states.Values)foreach(var p in s.pieces){Destroy(p.sprite);Destroy(p.paper);}if(ovalMask!=null){Destroy(ovalMask.texture);Destroy(ovalMask);}}
}
public sealed class MemoryDragHandle : MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IBeginDragHandler,IDragHandler,IEndDragHandler
{
 public DemoMemoryView owner;public int index;public bool junk;
 public void OnPointerDown(PointerEventData e)=>owner.BeginDrag(index,junk,e);
 public void OnBeginDrag(PointerEventData e)=>owner.BeginDrag(index,junk,e);
 public void OnDrag(PointerEventData e)=>owner.Drag(e);
 public void OnEndDrag(PointerEventData e)=>owner.EndDrag();
 public void OnPointerUp(PointerEventData e)=>owner.EndDrag();
}
