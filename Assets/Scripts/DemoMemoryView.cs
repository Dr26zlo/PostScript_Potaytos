using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DemoMemoryView : MonoBehaviour
{
 private sealed class Piece { public Rect rect; public int row,col; public string text; public Texture2D paper; public int group; }
 private sealed class State { public List<Piece> pieces=new List<Piece>();public List<Rect> junk=new List<Rect>();public bool complete; }
 private readonly Dictionary<string,State> states=new Dictionary<string,State>();
 private DemoPuzzle puzzle;private State state;private Action closed;private int drag=-1;private Vector2 offset;private bool junkDrag;private AudioSource audioSource;
 private void Awake(){audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;}
 public bool IsOpen => puzzle != null;
 public void Open(DemoPuzzle value,Action onClose)
 {
  puzzle=value;closed=onClose;
  if(!states.TryGetValue(value.id,out state))
  {
   state=new State();states[value.id]=state;
   var rng=new System.Random(value.id.GetHashCode());
   for(int row=0;row<value.rows.Length;row++)for(int col=0;col<value.rows[row].parts.Length;col++)
   {
    state.pieces.Add(new Piece{row=row,col=col,text=value.rows[row].parts[col],group=state.pieces.Count,rect=new Rect(180+rng.Next(650),180+rng.Next(290),190,82),paper=MakePaper(row,col)});
   }
   if(value.mode=="memory")for(int i=0;i<8;i++){var p=state.pieces[i%state.pieces.Count];state.junk.Add(new Rect(p.rect.x-12+i*4,p.rect.y-10,210,130));}
  }
 }
 private Texture2D MakePaper(int row,int col)
 {
  const int w=240,h=100;var tex=new Texture2D(w,h);tex.filterMode=FilterMode.Bilinear;
  for(int y=0;y<h;y++)for(int x=0;x<w;x++)
  {
   int left=col==0?4:Profile(y,row,col);int right=Profile(y,row,col+1);
   bool solid=x>=left && x<w-16+right && y>3+(x%17==0?2:0) && y<h-4;
   float grain=((x*17+y*31)%23)/600f;tex.SetPixel(x,y,solid?new Color(.85f+grain,.79f+grain,.65f+grain):Color.clear);
  }
  tex.Apply();return tex;
 }
 private static int Profile(int y,int row,int boundary) => (int)(8+5*Mathf.Sin(y*(.12f+boundary*.025f)+row*2+boundary)+2*Mathf.Sin(y*.47f+boundary));
 private void OnGUI()
 {
  if(puzzle==null)return;
  GUI.color=new Color(0,0,0,.8f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;
  Matrix4x4 previous=GUI.matrix;float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));

  var oval=Resources.Load<Texture2D>("Demo/Memory/white-oval");
  if(puzzle.mode=="memory" && oval!=null)GUI.DrawTexture(new Rect(80,70,1120,580),oval,ScaleMode.StretchToFill);
  else {GUI.color=new Color(.32f,.24f,.16f);GUI.DrawTexture(new Rect(80,70,1120,580),Texture2D.whiteTexture);GUI.color=Color.white;}
  if(puzzle.mode=="memory"){var rim=Resources.Load<Texture2D>("Demo/Memory/graphite-rim");if(rim!=null)GUI.DrawTexture(new Rect(80,70,1120,580),rim,ScaleMode.StretchToFill);}
  var style=RoomReadableUi.PaperLabel(22);style.alignment=TextAnchor.MiddleCenter;style.wordWrap=true;
  foreach(var p in state.pieces){GUI.DrawTexture(p.rect,p.paper);GUI.Label(new Rect(p.rect.x+15,p.rect.y+10,p.rect.width-30,p.rect.height-20),p.text,style);}
  string[] names={"ticket","button","key","ribbon","postage-stamp","frame","lit-window","button"};
  for(int i=0;i<state.junk.Count;i++){var tex=Resources.Load<Texture2D>("Demo/Memory/"+names[i%names.Length]);if(tex!=null)GUI.DrawTexture(state.junk[i],tex,ScaleMode.ScaleToFit);}
  var white=new GUIStyle(GUI.skin.label){font=RoomReadableUi.Font,fontSize=22,wordWrap=true,alignment=TextAnchor.MiddleCenter};
  GUI.Label(new Rect(240,15,800,45),puzzle.title,white);
  GUI.Label(new Rect(180,650,920,55),state.complete?(puzzle.id=="letter_daughter"?"Но здесь она ждёт… Строка восстановлена. Вернитесь к клиенту.":"Строки восстановлены. Теперь выберите текст заказа у клиента."):puzzle.hint,white);
  var e=Event.current;Vector2 mouse=e.mousePosition;
  if(e.type==EventType.MouseDown && e.button==0)
  {
   for(int i=state.junk.Count-1;i>=0;i--)if(state.junk[i].Contains(mouse)){drag=i;junkDrag=true;offset=mouse-state.junk[i].position;e.Use();break;}
   if(drag<0)for(int i=state.pieces.Count-1;i>=0;i--)if(state.pieces[i].rect.Contains(mouse)){drag=i;junkDrag=false;offset=mouse-state.pieces[i].rect.position;e.Use();break;}
  }
  if(e.type==EventType.MouseDrag && drag>=0)
  {
   Vector2 position=mouse-offset;position.x=Mathf.Clamp(position.x,65,1050);position.y=Mathf.Clamp(position.y,80,560);
   if(junkDrag){var rect=state.junk[drag];rect.position=position;state.junk[drag]=rect;}else {var piece=state.pieces[drag];Vector2 delta=position-piece.rect.position;
    Rect bounds=piece.rect;foreach(var member in state.pieces)if(member.group==piece.group)bounds=Rect.MinMaxRect(Mathf.Min(bounds.xMin,member.rect.xMin),Mathf.Min(bounds.yMin,member.rect.yMin),Mathf.Max(bounds.xMax,member.rect.xMax),Mathf.Max(bounds.yMax,member.rect.yMax));
    delta.x=Mathf.Clamp(delta.x,65-bounds.xMin,1215-bounds.xMax);delta.y=Mathf.Clamp(delta.y,80-bounds.yMin,640-bounds.yMax);
    foreach(var member in state.pieces)if(member.group==piece.group)member.rect.position+=delta;}e.Use();
  }
  if(e.type==EventType.MouseUp && drag>=0)
  {
   if(!junkDrag)Snap(state.pieces[drag]);drag=-1;e.Use();
  }
  if(RoomReadableUi.PaperButton(new Rect(20,15,170,44),"Назад к клиенту",1f)){puzzle=null;drag=-1;var callback=closed;closed=null;callback?.Invoke();}
  GUI.matrix=previous;
 }
 private void Snap(Piece selectedPiece)
 {
  int group=selectedPiece.group;
  foreach(var edge in state.pieces)if(edge.group==group && TrySnap(edge))return;
 }
 private bool TrySnap(Piece piece)
 {
  // Each strip has a specific neighbour and matching torn profile. Wrong rows never join.
  foreach(var other in state.pieces)
  {
   if(other==piece || other.row!=piece.row || Mathf.Abs(other.col-piece.col)!=1)continue;
   Vector2 expected=other.rect.position+Vector2.right*((piece.col-other.col)*(224f/240f*190f));
   if(Vector2.Distance(piece.rect.position,expected)>48)continue;
   if(piece.group==other.group)continue;
   int old=piece.group,target=other.group;Vector2 delta=expected-piece.rect.position;
   foreach(var member in state.pieces)if(member.group==old){member.rect.position+=delta;member.group=target;}
   var sound=Resources.Load<AudioClip>("RoomStations/Paper");if(sound!=null)audioSource.PlayOneShot(sound,.5f);
   bool all=true;
   foreach(var first in state.pieces)foreach(var part in state.pieces)
       if(first.row==part.row && first.group!=part.group)all=false;
   if(all){state.complete=true;DemoDirector.Instance.CompletePuzzle(puzzle);}
   return true;
  }
  return false;
 }
 private void OnDestroy(){foreach(var s in states.Values)foreach(var p in s.pieces)Destroy(p.paper);}
}
