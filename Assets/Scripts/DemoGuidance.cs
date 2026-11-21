using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class DemoGuidance : MonoBehaviour
{
 public string Stage { get; private set; }
 private readonly Queue<string> notifications=new Queue<string>();private string toast;private float toastRemaining;private GuestDoor door;private DialogueRunner dialogue;private TypewriterStation machine;private SpriteRenderer doorSprite;
 private float opening=-1;private Light2D highlight;private Transform room;
 public IEnumerator Awaken()
 {
  opening=0;var camera=GetComponent<CameraMovement>();camera.BeginWakeBlur();
  while(opening<1){opening+=Time.unscaledDeltaTime/1.3f;yield return null;}
  opening=-1;
 }
 public void SetStage(string stage){Stage=stage;GetComponent<CameraMovement>().TutorialOverview=!string.IsNullOrEmpty(stage);}
 public void Notify(string storage){string text=storage=="archive"?"Новый документ в архиве":"Новая запись на доске";if(toast!=text&&!notifications.Contains(text))notifications.Enqueue(text);}
 private void Start()
 {
  door=FindObjectOfType<GuestDoor>();dialogue=FindObjectOfType<DialogueRunner>();machine=FindObjectOfType<TypewriterStation>();foreach(var r in FindObjectsOfType<SpriteRenderer>())if(r.name=="Закрытая дверь"){doorSprite=r;break;}
  foreach(var root in gameObject.scene.GetRootGameObjects())if(root.transform.Find("Освещение")!=null){room=root.transform;break;}
  var obj=new GameObject("Мягкая подсветка обучения");highlight=obj.AddComponent<Light2D>();highlight.lightType=Light2D.LightType.Point;highlight.color=new Color(.88f,.9f,1);highlight.pointLightOuterRadius=2;highlight.pointLightInnerRadius=.2f;
 }
 private void Update()
 {
  var director=DemoDirector.Instance;if(director==null||highlight==null)return;
  string target=Stage;
  if(string.IsNullOrEmpty(target)&&door!=null&&door.IsWaiting)target="door";
  Vector3 at=room!=null?room.TransformPoint(target=="board"?new Vector3(15.6f,.4f,0):new Vector3(11,-6,0)):Vector3.zero;
   if(target=="door"&&doorSprite!=null)at=doorSprite.bounds.center;
   if(!string.IsNullOrEmpty(Stage))GetComponent<CameraMovement>().TutorialOverviewTarget=at;
  highlight.enabled=director.Mode=="room"&&!string.IsNullOrEmpty(target);
  if(highlight.enabled)
  {
   highlight.transform.position=at;highlight.intensity=.18f+.18f*(.5f+.5f*Mathf.Sin(Time.unscaledTime*2.6f));
  }
  if(director.Mode=="room"&&string.IsNullOrEmpty(target)&&(dialogue==null||!dialogue.IsDialogueActive)&&(machine==null||!machine.IsFocused))
  {
   if(toast!=null){toastRemaining-=Time.unscaledDeltaTime;if(toastRemaining<=0)toast=null;}
   if(toast==null&&notifications.Count>0){toast=notifications.Dequeue();toastRemaining=3.5f;}
  }
 }
 private void OnGUI()
 {
  var director=DemoDirector.Instance;if(director==null)return;
  if(opening>=0)
  {
   float height=Screen.height*.5f*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01(opening)));
   GUI.color=Color.black;GUI.DrawTexture(new Rect(0,0,Screen.width,height),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(0,Screen.height-height,Screen.width,height),Texture2D.whiteTexture);GUI.color=Color.white;return;
  }
  if(director.Mode!="room"||(dialogue!=null&&dialogue.IsDialogueActive)||(machine!=null&&machine.IsFocused))return;
  string target=Stage;
  if(string.IsNullOrEmpty(target)&&door!=null&&door.IsWaiting)target="door";
  string text=target=="board"?director.Content.tutorial.board:target=="archive"?director.Content.tutorial.archive:target=="door"?director.Content.tutorial.door:toast;
  if(string.IsNullOrEmpty(text))return;
  if(machine!=null&&machine.HasLineBreakHint)return;
  float s=RoomReadableUi.Scale;var style=new GUIStyle(GUI.skin.label){font=RoomReadableUi.Font,fontSize=Mathf.RoundToInt(26*s),alignment=TextAnchor.MiddleCenter,wordWrap=true};
  var rect=new Rect(Screen.width*.15f,Screen.height*.39f,Screen.width*.7f,90*s);
  style.normal.textColor=new Color(0,0,0,.75f);GUI.Label(new Rect(rect.x+2,rect.y+2,rect.width,rect.height),text,style);
  style.normal.textColor=new Color(1,1,1,.8f+.2f*Mathf.Sin(Time.unscaledTime*2));GUI.Label(rect,text,style);
 }
 private void OnDestroy(){if(highlight!=null)Destroy(highlight.gameObject);}
}
