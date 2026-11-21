using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DemoDirector : MonoBehaviour
{
 public static DemoDirector Instance { get; private set; }
 public DemoContent Content { get; private set; }
 public HashSet<string> Flags { get; private set; } = new HashSet<string>();
 public readonly Dictionary<string,string> Results = new Dictionary<string,string>();
 public int Step { get; private set; }
 public string Mode { get; private set; } = "welcome";
 private DialogueRunner dialogue; private CameraMovement movement; private TypewriterStation machine;
 private Guest guest; private DemoOrder order; private DemoOption selected; private GuestDoor door;
 private readonly List<DialogueObject> nodes = new List<DialogueObject>();
 private Action afterDialogue;private Action journalReturn; private bool sequenceDone; private bool started; private string inspected;
 private static bool autoStart;
 private DemoReward inspectedReward;private Vector2 archiveScroll;
 private DemoGuidance guidance;private string storage="board";private bool resumed;
 private Transform imp; private DemoMemoryView memory; private AudioSource voice;
 private string SavePath => Environment.GetEnvironmentVariable("POSTCRIPT_QA")=="1"?"/tmp/postcript-qa-save.json":Path.Combine(Application.persistentDataPath,"postcript-demo-v3.json");
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Register(){ SceneManager.sceneLoaded-=Install;SceneManager.sceneLoaded+=Install; }
 static void Install(Scene s,LoadSceneMode mode){ if(Camera.main!=null && Camera.main.GetComponent<CameraMovement>()!=null)Camera.main.gameObject.AddComponent<DemoDirector>(); }
 private void Awake()
 {
  Instance=this; Content=JsonUtility.FromJson<DemoContent>(Resources.Load<TextAsset>("Demo/Content").text);
  var cycle=FindObjectOfType<TaskCycle>();if(cycle!=null)cycle.enabled=false;
  foreach(var t in FindObjectsOfType<Transform>(true))if(t.name=="Сидящий имп"){imp=t;var r=t.GetComponent<SpriteRenderer>();var art=Resources.Load<Sprite>("Demo/Art/imp");if(r!=null && art!=null){float bottom=r.bounds.min.y;float height=r.bounds.size.y;r.sprite=art;t.localScale*=height/r.bounds.size.y;t.position+=Vector3.up*(bottom-r.bounds.min.y);}imp.gameObject.SetActive(false);}
 }
 private void Start(){dialogue=FindObjectOfType<DialogueRunner>();movement=GetComponent<CameraMovement>();machine=FindObjectOfType<TypewriterStation>();door=FindObjectOfType<GuestDoor>();memory=gameObject.AddComponent<DemoMemoryView>();voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;dialogue.onDialogueEnd.AddListener(OnDialogueEnd);guidance=gameObject.AddComponent<DemoGuidance>();movement.enabled=false;if(autoStart){autoStart=false;Begin(false);}}
 private void OnNode(DialogueObject node){if(Mode=="phone" && node.speakerName!="Вы"){voice.Stop();voice.PlayOneShot(Resources.Load<AudioClip>("PhoneAudio/VoiceQuestion"),.5f);}}
 private void OnDialogueEnd(){var done=afterDialogue;afterDialogue=null;done?.Invoke();}
 public DialogueObject Node(string who,string text){var n=ScriptableObject.CreateInstance<DialogueObject>();n.speakerName=who;n.dialogueText=text;nodes.Add(n);return n;}
 private DialogueChoice Choice(string label,Action action)=>new DialogueChoice{choiceText=label,action=action};
 private void Say(string who,IEnumerable<string> lines,Action done)
 {
  DialogueObject first=null,last=null;foreach(string text in lines){var n=Node(who,text);if(first==null)first=n;if(last!=null)last.nextNode=n;last=n;}
  afterDialogue=done;dialogue.StartDialogue(first);
 }
 public bool Available(string flag)=>string.IsNullOrEmpty(flag)||Flags.Contains(flag);
 public void AddDocument(string id){if(string.IsNullOrEmpty(id)||!Flags.Add(id))return;var document=Content.documents.FirstOrDefault(d=>d.id==id);if(started&&document!=null&&guidance!=null)guidance.Notify(document.storage);}
 public void Begin(bool resume)
 {
  resumed=resume;Flags.Clear();Results.Clear();Step=0;
  if(resume && File.Exists(SavePath))try{var save=JsonUtility.FromJson<DemoSave>(File.ReadAllText(SavePath));Step=save.step;foreach(var f in save.flags)Flags.Add(f);if(save.errors!=null)Content.documents=Content.documents.Concat(save.errors).ToArray();foreach(var r in save.results){var bits=r.Split('=');Results[bits[0]]=bits[1];}}catch(Exception){Step=0;Flags.Clear();Results.Clear();}
  foreach(var prior in Content.orders)if(Results.ContainsKey(prior.id)&&prior.options.Any(o=>o.id==Results[prior.id]&&o.reward)){var reward=FindReward(prior.reward);if(reward!=null)reward.SetActive(true);}
  foreach(string id in new[]{"archive_rules","start_memory","start_mixed","start_departure","contract_signature"})AddDocument(id);
  Mode="room";movement.enabled=true;started=true;StartCoroutine(Flow());
 }
 private void Save(){try{File.WriteAllText(SavePath,JsonUtility.ToJson(new DemoSave{step=Step,flags=Flags.ToArray(),results=Results.Select(p=>p.Key+"="+p.Value).ToArray(),errors=Content.documents.Where(d=>d.id.StartsWith("error_")).ToArray()}));}catch(Exception ex){Debug.LogWarning("Не удалось сохранить демо: "+ex.Message);}}
 private IEnumerator Flow()
 {
  for(;Step<Content.flow.Length;Step++)
  {
   Save(); string beat=Content.flow[Step];sequenceDone=false;
   if(beat=="initial_board")
   {
    if(!resumed){guidance.SetStage("board");Mode="awakening";yield return guidance.Awaken();Mode="room";}
    if(!Flags.Contains("tutorial_board_done")){guidance.SetStage("board");yield return new WaitUntil(()=>Flags.Contains("tutorial_board_done"));}
    guidance.SetStage(null);
   }
   else if(beat=="initial_archive")
   {
    if(!Flags.Contains("tutorial_archive_done")){guidance.SetStage("archive");yield return new WaitUntil(()=>Flags.Contains("tutorial_archive_done"));}
    guidance.SetStage(null);
   }
   else if(beat.StartsWith("call_")){yield return CallOrder(beat.Substring(5));}
   else if(beat=="wife_call"){yield return Phone(Content.wife,()=>{AddDocument("wife_call");AddDocument("work_log");});}
   else if(beat=="imp"){yield return ImpScene();}
   else if(beat=="ending"){AddDocument("contract_end");Mode="room";OpenJournal();inspected="contract_end";yield return new WaitUntil(()=>Mode=="ending");movement.enabled=false;Save();yield break;}
   else {order=Content.orders.FirstOrDefault(o=>o.id==beat);if(order!=null)yield return OrderVisit();}
  }
 }
 private IEnumerator OrderVisit()
 {
  var prefab=Resources.Load<GameObject>("Demo/Client");var obj=Instantiate(prefab);guest=obj.GetComponent<Guest>();guest.name=order.speaker;selected=null;
  var texture=Resources.Load<Sprite>("Demo/Art/"+order.npc);var renderer=guest.GetComponent<SpriteRenderer>();
  if(texture!=null){float height=renderer.bounds.size.y;renderer.sprite=texture;float newHeight=texture.bounds.size.y;guest.transform.localScale=Vector3.one*(height/newHeight);}
  var reward=FindReward(order.reward);guest.PrepareArrival(reward,door);guest.DemoInteraction=InteractGuest;guest.DemoSubmission=Submit;
  yield return new WaitUntil(()=>guest==null);Mode="room";order=null;
 }
 private GameObject FindReward(string key)
 {
  string name=key=="glass_fish"?"Стеклянная рыба":key=="clock"?"Часы":null;
  foreach(var t in FindObjectsOfType<Transform>(true))if(t.name==name){DemoReward.Attach(t.gameObject,key);return t.gameObject;}
  var sprite=Resources.Load<Sprite>("Demo/Art/"+key);if(sprite==null)return null;
  var obj=new GameObject(key);var r=obj.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sortingLayerName="FG";r.sortingOrder=30;obj.transform.position=new Vector3(key=="paper_tram"?4:5,-3,0);obj.transform.localScale=Vector3.one*(.9f/sprite.bounds.size.y);obj.SetActive(false);DemoReward.Attach(obj,key);return obj;
 }
 public void InteractGuest()
 {
  if(Mode!="room" || dialogue.IsDialogueActive || machine.IsFocused || (RoomStationsController.Instance!=null && RoomStationsController.Instance.IsOpen))return;
  if(selected!=null && machine.FinishedSheetCount>0){machine.TryGiveNextSheet(guest);return;}
  Portrait("neutral");movement.FocusOn(PortraitFocus.For(guest.transform));
  if(!Flags.Contains(order.id+"_met")){Flags.Add(order.id+"_met");Say(order.speaker,order.intro,Menu);}
  else Menu();
 }
 private void Menu()
 {
  Mode="room";var node=Node(order.speaker,"Что вы хотите уточнить?");
  foreach(var q in order.questions)if(Available(q.requires))
  {
   var question=q;node.choices.Add(Choice(q.label,()=>
   {
    Portrait("question");
    if(!string.IsNullOrEmpty(question.action))
    {
     if(question.action=="letter_daughter" && !Flags.Contains("letter_flash")){Flags.Add("letter_flash");Say("Мысль",new[]{"Она отступила. Боялась меня?.."},()=>OpenPuzzle(question.action));}
     else OpenPuzzle(question.action);
    }
    else
    {
     AddDocument(question.inspect);AddDocument(question.document);
     Action done=()=>{if(!string.IsNullOrEmpty(question.inspect))InspectDocument(question.inspect);else Menu();};
     if(!string.IsNullOrEmpty(question.answer))Say(order.speaker,new[]{question.answer},done);else done();
    }
   }));
  }
  node.choices.Add(Choice("Перейти к тексту заказа",TextMenu));
  node.choices.Add(Choice("Вернуться к комнате",()=>{dialogue.EndDialogue();movement.ResetZoom();}));dialogue.StartDialogue(node);
 }
 private void TextMenu()
 {
  var node=Node(order.speaker,"Какой текст напечатать?");
  foreach(var opt in order.options)if(Available(opt.requires)){var option=opt;node.choices.Add(Choice(opt.label,()=>SelectOrder(option)));}
  node.choices.Add(Choice("Ещё вопросы",Menu));dialogue.StartDialogue(node);
 }
 private void OpenPuzzle(string id)
 {
  dialogue.EndDialogue();movement.ResetZoom();Mode="memory";movement.enabled=false;
  memory.Open(Content.puzzles.First(p=>p.id==id),()=>{Mode="room";movement.enabled=true;movement.FocusOn(PortraitFocus.For(guest.transform));Menu();});
 }
 private void SelectOrder(DemoOption option){selected=option;guest.SetExpectedText(option.text,option.requiresLineBreak);machine.TakeOrder(guest);dialogue.EndDialogue();movement.ResetZoom();}
 private void Portrait(string emotion){if(guest==null||order==null)return;var pose=Resources.Load<Sprite>("Demo/Art/"+order.npc+(emotion=="neutral"?"":"-"+emotion));if(pose!=null)PortraitFocus.SetSprite(guest.transform,pose);}
 private bool Submit(string text)
 {
  if(selected==null || guest==null)return false;
  bool exact=guest.MatchesOrder(text);bool success=exact && selected.reward;
  Results[order.id]=exact?selected.id:"typo";
  if(exact)AddDocument(selected.document);else { var error=new DemoDocument{id="error_"+order.id,title="Лист с опечаткой · "+order.speaker,storage="archive",text=text+"\n\nКлиент получил лист с опечаткой. Награда не выдана."};var all=Content.documents.ToList();all.Add(error);Content.documents=all.ToArray();AddDocument(error.id);}machine.ReleaseOrder(guest);
  var answer=Node(order.speaker,exact?selected.response:"В тексте ошибка. Я заберу лист, но воспользоваться им не смогу.");
  Portrait(success?"happy":"angry");guest.CompleteDemo(answer,success);return true;
 }
 private IEnumerator CallOrder(string id)
 {
  var source=Content.orders.First(o=>o.id==id);string result=Results.ContainsKey(id)?Results[id]:"typo";
  var lines=source.calls.Get(result)??new[]{"Я не смог воспользоваться этим листом. В нём ошибка."};
  string doc=id=="lantern"?"o1_call_"+result:"o2_call_"+result;
  yield return Phone(lines,()=>AddDocument(doc));
 }
 private IEnumerator Phone(string[] lines,Action done)
 {
  var call=GetComponent<RoomPhoneCall>();if(call==null)call=gameObject.AddComponent<RoomPhoneCall>();
  call.SetConversation(lines);Mode="phone";yield return call.RingAndWait();done();Mode="room";
 }
 private IEnumerator ImpScene()
 {
  if(imp!=null)imp.gameObject.SetActive(true);
  var asked=new HashSet<string>();
  Action menu=null;
  menu=()=>
  {
   var node=Node("Имп","Что ещё хотите узнать?");
   foreach(var entry in Content.imp.choices)if(!asked.Contains(entry.label))
   {
    var question=entry;node.choices.Add(Choice(entry.label,()=>{asked.Add(question.label);Say("Имп",new[]{question.answer},menu);}));
   }
   node.choices.Add(Choice("Покажи соглашение",()=>Say("Имп",new[]{Content.imp.outro},()=>{AddDocument(Content.imp.document);sequenceDone=true;})));
   dialogue.StartDialogue(node);
  };
  if(imp!=null)movement.FocusOn(PortraitFocus.For(imp));Say("Имп",new[]{Content.imp.intro},menu);
  yield return new WaitUntil(()=>sequenceDone);
  if(imp!=null)imp.gameObject.SetActive(false);movement.ResetZoom();
 }
 public void CompletePuzzle(DemoPuzzle puzzle){Flags.Add(puzzle.id+"_complete");AddDocument(puzzle.onCompleteDocument);}
 private void InspectDocument(string id){dialogue.EndDialogue();movement.ResetZoom();Mode="room";OpenStorage(Content.documents.First(d=>d.id==id).storage);inspected=id;journalReturn=()=>{movement.FocusOn(PortraitFocus.For(guest.transform));Menu();};}
 public bool IsReadingArchive => Mode=="archive"&&inspected!=null;
 public void ReadArchive(string id){if(Mode=="archive")inspected=id;}
 public void CloseJournal()
 {
  if(storage=="archive"&&RoomStationsController.Instance!=null)RoomStationsController.Instance.Hide();
  if(storage=="board"&&Flags.Contains("tutorial_board_opened"))Flags.Add("tutorial_board_done");
  if(storage=="archive"&&Flags.Contains("tutorial_archive_opened"))Flags.Add("tutorial_archive_done");
  if(journalReturn!=null){var callback=journalReturn;journalReturn=null;Mode="room";movement.enabled=true;callback();return;}
  Mode=Step<Content.flow.Length&&Content.flow[Step]=="ending"?"ending":"room";movement.enabled=Mode=="room";
 }
 public void OpenJournal()=>OpenStorage("board");
 public void OpenArchive()=>OpenStorage("archive");
 private void OpenStorage(string place)
 {
  if(Mode!="room")return;
  if(guidance!=null&&guidance.Stage=="board"&&place!="board")return;
  if(guidance!=null&&guidance.Stage=="archive"&&place!="archive")return;
  storage=place=="archive"?"archive":"board";Mode=storage=="archive"?"archive":"journal";inspected=null;movement.enabled=false;
  if(storage=="archive"&&RoomStationsController.Instance!=null)RoomStationsController.Instance.ShowArchive(Content.documents.Where(d=>Flags.Contains(d.id)&&d.storage=="archive").OrderBy(d=>d.id=="archive_rules"?0:1).ToArray());
  Flags.Add(storage=="archive"?"tutorial_archive_opened":"tutorial_board_opened");
 }
 public void CloseReward(){dialogue.EndDialogue();Mode="room";movement.enabled=true;inspectedReward=null;}
 public void InspectReward(DemoReward reward){if(Mode!="room"||dialogue.IsDialogueActive)return;inspectedReward=reward;Mode="reward";movement.enabled=false;Say("Вы",new[]{reward.Description},null);}
 private void OnGUI()
 {
  float s=RoomReadableUi.Scale;var label=RoomReadableUi.PaperLabel(Mathf.RoundToInt(19*s));label.wordWrap=true;
  if(Mode=="welcome" || Mode=="ending")
  {
   GUI.color=Color.black;GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;
   var style=new GUIStyle(label);style.normal.textColor=new Color(.9f,.85f,.72f);style.alignment=TextAnchor.MiddleCenter;
   string text=Mode=="welcome"?"POSTCRIPT\nЛавка писем и воспоминаний\n\nСначала осмотрите доску. На стук откройте дверь.\nЗадавайте вопросы, исследуйте память, печатайте буквы мышью.\nПеренос строки — рычаг машинки вправо.":Content.ending.always+"\n\n"+(Results.ContainsKey("doors")&&Results["doors"]=="preserve"?Content.ending.preserve:Content.ending.erase)+"\n"+(Results.ContainsKey("letter")&&Results["letter"]=="restored"?Content.ending.restored:Results.ContainsKey("letter")&&Results["letter"]=="false"?Content.ending.@false:Results.ContainsKey("letter")&&Results["letter"]=="typo"?"В моей копии опечатка. Нужно вернуться к подлиннику письма.":Content.ending.check)+"\n\n"+Content.ending.goal+"\n"+Content.ending.title;
   GUI.Label(new Rect(Screen.width*.2f,Screen.height*.15f,Screen.width*.6f,Screen.height*.5f),text,style);
   if(Mode=="ending" && RoomReadableUi.PaperButton(new Rect(Screen.width/2-130*s,Screen.height*.64f,260*s,42*s),"Посмотреть доску")){Mode="room";OpenJournal();}
   if(RoomReadableUi.PaperButton(new Rect(Screen.width/2-130*s,Screen.height*.72f,260*s,45*s),Mode=="welcome"?"Начать":"Заново")){if(started){autoStart=true;SceneManager.LoadScene(SceneManager.GetActiveScene().name);}else Begin(false);}
   if(Mode=="welcome" && File.Exists(SavePath)&&RoomReadableUi.PaperButton(new Rect(Screen.width/2-130*s,Screen.height*.8f,260*s,45*s),"Продолжить"))Begin(true);
   if(RoomReadableUi.PaperButton(new Rect(Screen.width/2-130*s,Screen.height*.88f,260*s,40*s),"Выход"))Application.Quit();return;
  }
  if(Mode=="reward")
  {
   GUI.color=new Color(0,0,0,.9f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;
   var renderers=inspectedReward.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.enabled).OrderBy(r=>r.sortingOrder).ToArray();
   if(renderers.Length>0)
   {
    Bounds total=renderers[0].bounds;foreach(var r in renderers)total.Encapsulate(r.bounds);
    var area=new Rect(Screen.width*.2f,Screen.height*.1f,Screen.width*.6f,Screen.height*.6f);float fit=Mathf.Min(area.width/total.size.x,area.height/total.size.y);
    foreach(var r in renderers){Vector3 centre=r.transform.TransformPoint(r.sprite.bounds.center);Vector2 at=area.center+new Vector2(centre.x-total.center.x,total.center.y-centre.y)*fit;var size=Vector2.Scale(r.sprite.bounds.size,r.transform.lossyScale)*fit;Matrix4x4 matrix=GUI.matrix;GUIUtility.RotateAroundPivot(-r.transform.eulerAngles.z,at);RoomReadableUi.DrawPaper(r.sprite,new Rect(at-size*.5f,size));GUI.matrix=matrix;}
   }

   if(RoomReadableUi.PaperButton(new Rect(20,20,170*s,42*s),"Назад в комнату"))CloseReward();return;
  }
  if(Mode!="journal"&&Mode!="archive")return;
  if(Mode=="archive"&&inspected==null&&RoomStationsController.Instance!=null&&RoomStationsController.Instance.IsOpen)return;
  GUI.color=Color.black;GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;
  var docs=Content.documents.Where(d=>Flags.Contains(d.id)&&(d.storage=="archive"?"archive":"board")==storage).OrderBy(d=>d.id=="archive_rules"?0:1).ToArray();
  if(inspected!=null)
  {
   var doc=docs.First(d=>d.id==inspected);var image=Resources.Load<Texture2D>("Demo/Documents/"+doc.id);
   if(image!=null)GUI.DrawTexture(new Rect(Screen.width*.22f,50*s,Screen.width*.56f,Screen.height-130*s),image,ScaleMode.ScaleToFit);
   else
   {
    var sheet=new Rect(Screen.width*.2f,70*s,Screen.width*.6f,Screen.height-150*s);
    GUI.color=new Color(.91f,.86f,.73f);GUI.DrawTexture(sheet,Texture2D.whiteTexture);GUI.color=Color.white;
    GUI.Label(new Rect(sheet.x+35*s,sheet.y+35*s,sheet.width-70*s,sheet.height-130*s),doc.title+"\n\n"+doc.text,label);
    var stamp=Resources.Load<Texture2D>("Demo/Documents/error_overlay");if(stamp!=null)GUI.DrawTexture(new Rect(sheet.x+25*s,sheet.yMax-100*s,sheet.width-50*s,75*s),stamp,ScaleMode.ScaleToFit);
   }
   if(RoomReadableUi.PaperButton(new Rect(20,20,150*s,42*s),journalReturn!=null?"К разговору":storage=="archive"?"К архиву":"К доске")){if(journalReturn!=null)CloseJournal();else inspected=null;}
  }
  else
  {
   if(storage=="archive")DrawArchive(docs,s);else DrawBoard(docs,s);
   if(RoomReadableUi.PaperButton(new Rect(20,20,200*s,46*s),Step<Content.flow.Length&&Content.flow[Step]=="ending"?"Продолжение следует":"Назад в комнату"))CloseJournal();
  }
 }
 public static Rect FitImage(Rect area,float aspect)
 {
  Vector2 size=area.width/area.height>aspect?new Vector2(area.height*aspect,area.height):new Vector2(area.width,area.width/aspect);
  return new Rect(area.center-size*.5f,size);
 }
 public static Vector2 UnrotatePointer(Vector2 point,Vector2 centre,float degrees)=>centre+(Vector2)(Quaternion.Euler(0,0,-degrees)*(point-centre));
 private void DrawBoard(DemoDocument[] docs,float s)
 {
  var texture=Resources.Load<Texture2D>("Demo/Art/board");Rect board=FitImage(new Rect(30*s,65*s,Screen.width-60*s,Screen.height-100*s),texture!=null?texture.width/(float)texture.height:1.7f);
  if(texture!=null)GUI.DrawTexture(board,texture);
  Rect inner=new Rect(board.x+board.width*.12f,board.y+board.height*.13f,board.width*.76f,board.height*.72f);
  int cols=docs.Length<=4?2:4,rows=Mathf.Max(1,Mathf.CeilToInt(docs.Length/(float)cols));float w=inner.width/cols,h=inner.height/rows;
  var pin=Resources.Load<Texture2D>("Demo/UI/pin");
  for(int i=0;i<docs.Length;i++)
  {
   var doc=docs[i];Rect cell=new Rect(inner.x+(i%cols)*w+w*.1f,inner.y+(i/cols)*h+h*.1f,w*.8f,h*.8f);
   Rect paper=FitImage(cell,1600f/1040);float angle=(i%2==0?-1:1)*(1.8f+i%3*.7f);var matrix=GUI.matrix;
   GUIUtility.RotateAroundPivot(angle,paper.center);var image=Resources.Load<Texture2D>("Demo/Documents/"+doc.id);
   if(image!=null)GUI.DrawTexture(paper,image);
   else {GUI.color=new Color(.9f,.85f,.74f);GUI.DrawTexture(paper,Texture2D.whiteTexture);GUI.color=Color.white;GUI.Label(paper,doc.title,RoomReadableUi.PaperLabel(Mathf.RoundToInt(16*s)));}
   float pinSize=Mathf.Min(34*s,paper.height*.12f);if(pin!=null)GUI.DrawTexture(new Rect(paper.center.x-pinSize/2,paper.y+2,pinSize,pinSize),pin,ScaleMode.ScaleToFit);
   GUI.matrix=matrix;
   if(Event.current.type==EventType.MouseDown&&Event.current.button==0&&paper.Contains(UnrotatePointer(Event.current.mousePosition,paper.center,angle))){inspected=doc.id;Event.current.Use();}
  }
 }
 private void DrawArchive(DemoDocument[] docs,float s)
 {
  var heading=new GUIStyle(GUI.skin.label){font=RoomReadableUi.Font,fontSize=Mathf.RoundToInt(28*s),alignment=TextAnchor.MiddleCenter};GUI.Label(new Rect(0,25*s,Screen.width,45*s),"Архив писем и рабочих записей",heading);
  Rect view=new Rect(Screen.width*.18f,90*s,Screen.width*.64f,Screen.height-130*s);float rowHeight=125*s;
  archiveScroll=GUI.BeginScrollView(view,archiveScroll,new Rect(0,0,view.width-25*s,docs.Length*rowHeight));
  for(int i=0;i<docs.Length;i++)
  {
   var row=new Rect(0,i*rowHeight,view.width-30*s,rowHeight-12*s);var image=Resources.Load<Texture2D>("Demo/Documents/"+docs[i].id);
   if(image!=null)GUI.DrawTexture(new Rect(row.x+5*s,row.y,155*s,row.height),image,ScaleMode.ScaleToFit);
   if(RoomReadableUi.PaperButton(new Rect(row.x+175*s,row.y+15*s,row.width-180*s,row.height-30*s),docs[i].title))inspected=docs[i].id;
  }
  GUI.EndScrollView();
 }
 private void OnDestroy(){if(Instance==this)Instance=null;foreach(var n in nodes)if(n!=null)Destroy(n);}
}
