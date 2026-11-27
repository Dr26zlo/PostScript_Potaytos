using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CampaignController : MonoBehaviour
{
 public static CampaignController Instance { get; private set; }
 public CampaignContent Content { get; private set; }
 public HashSet<string> Flags { get; private set; } = new HashSet<string>();
 public readonly Dictionary<string,string> Results = new Dictionary<string,string>();
 public int Step { get; private set; }
 public string Mode { get; private set; } = "welcome";
 private DialogueRunner dialogue; private CameraMovement movement; private TypewriterStation machine;
 private Guest guest; private OrderDefinition order; private OrderOption selected; private GuestDoor door;
 private TaskCycle cycle;
 private readonly Dictionary<string, Transform> actors = new Dictionary<string, Transform>();
 private Action journalReturn; private bool started; private string inspected;
 private static bool autoStart;
 private InspectableReward inspectedReward;private Vector2 archiveScroll;
 private TutorialGuidance guidance;private string storage="board";private bool resumed;
 private MemoryPuzzleView memory;
 private string SavePath => Environment.GetEnvironmentVariable("POSTCRIPT_QA")=="1"?Path.Combine(Path.GetTempPath(),"postcript-qa-save.json"):Path.Combine(Application.persistentDataPath,"postcript-demo-v3.json");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
 private static void StartFullscreen()
 {
  Display display = Display.main;
  Screen.SetResolution(display.systemWidth, display.systemHeight, FullScreenMode.FullScreenWindow);
 }
#endif
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Register(){ SceneManager.sceneLoaded-=Install;SceneManager.sceneLoaded+=Install; }
 static void Install(Scene s,LoadSceneMode mode){ if(Camera.main!=null && Camera.main.GetComponent<CameraMovement>()!=null)Camera.main.gameObject.AddComponent<CampaignController>(); }
 private void Awake()
 {
  Instance=this; Content=JsonUtility.FromJson<CampaignContent>(Resources.Load<TextAsset>("Demo/Content").text);
  cycle = FindObjectOfType<TaskCycle>();
  if (cycle == null) cycle = gameObject.AddComponent<TaskCycle>();
  cycle.UseCampaignSchedule();

  foreach (var task in Content.tasks.Where(task => task.kind == "conversation"))
  {
   if (actors.ContainsKey(task.target)) continue;
   var actor = FindObjectsOfType<Transform>(true).FirstOrDefault(item => item.name == task.target);
   actors.Add(task.target, actor);
   if (actor == null) continue;

   var renderer = actor.GetComponent<SpriteRenderer>();
   var portrait = Resources.Load<Sprite>("Demo/Art/" + task.portrait);
   if (renderer != null && portrait != null)
   {
    float bottom = renderer.bounds.min.y;
    float height = renderer.bounds.size.y;
    renderer.sprite = portrait;
    actor.localScale *= height / renderer.bounds.size.y;
    actor.position += Vector3.up * (bottom - renderer.bounds.min.y);
   }
   actor.gameObject.SetActive(false);
  }
 }

 private void Start()
 {
  dialogue = FindObjectOfType<DialogueRunner>();
  movement = GetComponent<CameraMovement>();
  machine = FindObjectOfType<TypewriterStation>();
  door = FindObjectOfType<GuestDoor>();
  memory = gameObject.AddComponent<MemoryPuzzleView>();
  guidance = gameObject.AddComponent<TutorialGuidance>();
  movement.enabled = false;
  if (autoStart)
  {
   autoStart = false;
   Begin(false);
  }
 }

 private static DialogueChoice Choice(string label, Action action) =>
  new DialogueChoice { choiceText = label, action = action };

 public bool Available(string flag)=>string.IsNullOrEmpty(flag)||Flags.Contains(flag);
 public void AddDocument(string id){if(string.IsNullOrEmpty(id)||!Flags.Add(id))return;var document=Content.documents.FirstOrDefault(d=>d.id==id);if(started&&document!=null&&guidance!=null)guidance.Notify(document.storage);}
 public void Begin(bool resume)
 {
  resumed=resume;Flags.Clear();Results.Clear();Step=0;
  if(resume && File.Exists(SavePath))try{var save=JsonUtility.FromJson<CampaignSave>(File.ReadAllText(SavePath));Step=save.step;foreach(var f in save.flags)Flags.Add(f);if(save.errors!=null)Content.documents=Content.documents.Concat(save.errors).ToArray();foreach(var r in save.results){var bits=r.Split('=');Results[bits[0]]=bits[1];}}catch(Exception){Step=0;Flags.Clear();Results.Clear();}
  foreach(var prior in Content.orders)if(Results.ContainsKey(prior.id)&&prior.options.Any(o=>o.id==Results[prior.id]&&o.reward)){var reward=FindReward(prior.reward);if(reward!=null)reward.SetActive(true);}
  foreach(string id in new[]{"archive_rules","start_memory","start_mixed","start_departure","contract_signature"})AddDocument(id);
  Mode="room";movement.enabled=true;started=true;StartCoroutine(Flow());
 }
 private void Save(){try{File.WriteAllText(SavePath,JsonUtility.ToJson(new CampaignSave{step=Step,flags=Flags.ToArray(),results=Results.Select(p=>p.Key+"="+p.Value).ToArray(),errors=Content.documents.Where(d=>d.id.StartsWith("error_")).ToArray()}));}catch(Exception ex){Debug.LogWarning("Не удалось сохранить демо: "+ex.Message);}}
 private IEnumerator Flow()
 {
  var handlers = new Dictionary<string, Func<TaskCycle.CampaignEntry, IEnumerator>>
  {
   { "tutorial", RunTutorial },
   { "order", OrderVisit },
   { "phone", RunPhone },
   { "conversation", RunConversation },
   { "ending", RunEnding }
  };
  return cycle.RunCampaign(Content.tasks, Step, index =>
  {
   Step = index;
   Save();
  }, handlers);
 }

 private IEnumerator RunTutorial(TaskCycle.CampaignEntry task)
 {
  if (task.awakening && !resumed)
  {
   guidance.SetStage(task.target);
   Mode = "awakening";
   yield return guidance.Awaken();
   Mode = "room";
  }
  if (!Flags.Contains(task.requirement))
  {
   guidance.SetStage(task.target);
   yield return new WaitUntil(() => Flags.Contains(task.requirement));
  }
  guidance.SetStage(null);
 }

 private bool AtEnding => Step < Content.tasks.Length && Content.tasks[Step].kind == "ending";

 private IEnumerator RunEnding(TaskCycle.CampaignEntry task)
 {
  AddDocument(task.target);
  Mode = "room";
  OpenJournal();
  inspected = task.target;
  yield return new WaitUntil(() => Mode == "ending");
  movement.enabled = false;
  Save();
 }

 private IEnumerator OrderVisit(TaskCycle.CampaignEntry task)
 {
  order = Content.orders.First(item => item.id == task.target);
  var prefab=Resources.Load<GameObject>("Demo/Client");var obj=Instantiate(prefab);guest=obj.GetComponent<Guest>();guest.name=order.speaker;selected=null;
  var texture=Resources.Load<Sprite>("Demo/Art/"+order.npc);var renderer=guest.GetComponent<SpriteRenderer>();
  if(texture!=null){float height=renderer.bounds.size.y;renderer.sprite=texture;float newHeight=texture.bounds.size.y;guest.transform.localScale=Vector3.one*(height/newHeight);}
  var reward=FindReward(order.reward);guest.PrepareArrival(reward,door);guest.OrderInteraction=InteractGuest;guest.OrderSubmission=Submit;
  yield return new WaitUntil(()=>guest==null);Mode="room";order=null;
 }
 private GameObject FindReward(string key)
 {
  string name=key=="glass_fish"?"Стеклянная рыба":key=="clock"?"Часы":null;
  foreach(var t in FindObjectsOfType<Transform>(true))if(t.name==name){InspectableReward.Attach(t.gameObject,key);return t.gameObject;}
  var sprite=Resources.Load<Sprite>("Demo/Art/"+key);if(sprite==null)return null;
  var obj=new GameObject(key);var r=obj.AddComponent<SpriteRenderer>();r.sprite=sprite;r.sortingLayerName="FG";r.sortingOrder=30;obj.transform.position=new Vector3(key=="paper_tram"?4:5,-3,0);obj.transform.localScale=Vector3.one*(.9f/sprite.bounds.size.y);obj.SetActive(false);InspectableReward.Attach(obj,key);return obj;
 }
 public void InteractGuest()
 {
  if(Mode!="room" || dialogue.IsDialogueActive || machine.IsFocused || (RoomStationsController.Instance!=null && RoomStationsController.Instance.IsOpen))return;
  if(selected!=null && machine.FinishedSheetCount>0){machine.TryGiveNextSheet(guest);return;}
  Portrait("neutral");movement.FocusOn(PortraitFocus.For(guest.transform));
  if(!Flags.Contains(order.id+"_met")){Flags.Add(order.id+"_met");dialogue.StartLines(order.speaker,order.intro,Menu);}
  else Menu();
 }
 private void Menu()
 {
  Mode="room";var choices = new List<DialogueChoice>();
  foreach(var q in order.questions)if(Available(q.requires))
  {
   var question=q;choices.Add(Choice(q.label,()=>
   {
    Portrait("question");
    if(!string.IsNullOrEmpty(question.action))
    {
     if(question.action=="letter_daughter" && !Flags.Contains("letter_flash")){Flags.Add("letter_flash");dialogue.StartLines("Мысль",new[]{"Она отступила. Боялась меня?.."},()=>OpenPuzzle(question.action));}
     else OpenPuzzle(question.action);
    }
    else
    {
     AddDocument(question.inspect);AddDocument(question.document);
     Action done=()=>{if(!string.IsNullOrEmpty(question.inspect))InspectDocument(question.inspect);else Menu();};
     if(!string.IsNullOrEmpty(question.answer))dialogue.StartLines(order.speaker,new[]{question.answer},done);else done();
    }
   }));
  }
  choices.Add(Choice("Перейти к тексту заказа",TextMenu));
  choices.Add(Choice("Вернуться к комнате",()=>{dialogue.EndDialogue();movement.ResetZoom();}));dialogue.StartMenu(order.speaker, "Что вы хотите уточнить?", choices);
 }
 private void TextMenu()
 {
  var choices = new List<DialogueChoice>();
  foreach(var opt in order.options)if(Available(opt.requires)){var option=opt;choices.Add(Choice(opt.label,()=>SelectOrder(option)));}
  choices.Add(Choice("Ещё вопросы",Menu));dialogue.StartMenu(order.speaker, "Какой текст напечатать?", choices);
 }
 private void OpenPuzzle(string id)
 {
  dialogue.EndDialogue();movement.ResetZoom();Mode="memory";movement.enabled=false;
  memory.Open(Content.puzzles.First(p=>p.id==id),()=>{Mode="room";movement.enabled=true;movement.FocusOn(PortraitFocus.For(guest.transform));Menu();});
 }
 private void SelectOrder(OrderOption option){selected=option;guest.SetExpectedText(option.text,option.requiresLineBreak);machine.TakeOrder(guest);dialogue.EndDialogue();movement.ResetZoom();}
 private void Portrait(string emotion){if(guest==null||order==null)return;var pose=Resources.Load<Sprite>("Demo/Art/"+order.npc+(emotion=="neutral"?"":"-"+emotion));if(pose!=null)PortraitFocus.SetSprite(guest.transform,pose);}
 private bool Submit(string text)
 {
  if(selected==null || guest==null)return false;
  bool exact=guest.MatchesOrder(text);bool success=exact && selected.reward;
  Results[order.id]=exact?selected.id:"typo";
  if(exact)AddDocument(selected.document);else { var error=new CampaignDocument{id="error_"+order.id,title="Лист с опечаткой · "+order.speaker,storage="archive",text=text+"\n\nКлиент получил лист с опечаткой. Награда не выдана."};var all=Content.documents.ToList();all.Add(error);Content.documents=all.ToArray();AddDocument(error.id);}machine.ReleaseOrder(guest);
  var answer=dialogue.CreateNode(order.speaker,exact?selected.response:"В тексте ошибка. Я заберу лист, но воспользоваться им не смогу.");
  Portrait(success?"happy":"angry");guest.CompleteOrder(answer,success);return true;
 }
 private IEnumerator RunPhone(TaskCycle.CampaignEntry task)
 {
  var lines = task.lines;
  OrderCall outcome = null;
  if (!string.IsNullOrEmpty(task.target))
  {
   var source = Content.orders.First(item => item.id == task.target);
   var result = Results.TryGetValue(task.target, out var value) ? value : "typo";
   outcome = source.calls.FirstOrDefault(call => call.result == result)
    ?? source.calls.First(call => call.result == "typo");
   lines = outcome.lines;
  }

  var phone = GetComponent<RoomPhoneCall>();
  if (phone == null) phone = gameObject.AddComponent<RoomPhoneCall>();
  phone.SetConversation(lines);
  Mode = "phone";
  yield return phone.RingAndWait();

  if (outcome != null) AddDocument(outcome.document);
  if (task.documents != null)
   foreach (string document in task.documents) AddDocument(document);
  Mode = "room";
 }

 private IEnumerator RunConversation(TaskCycle.CampaignEntry task)
 {
  var actor = actors[task.target];
  if (actor != null) actor.gameObject.SetActive(true);
  var asked = new HashSet<TaskCycle.ConversationChoice>();
  bool complete = false;

  void ShowMenu()
  {
   var choices = new List<DialogueChoice>();
   foreach (var choice in task.choices)
   {
    if (choice.once && asked.Contains(choice)) continue;
    choices.Add(Choice(choice.label, () =>
    {
     asked.Add(choice);
     dialogue.StartLines(task.speaker, new[] { choice.answer }, () =>
     {
      AddDocument(choice.document);
      if (choice.finish) complete = true;
      else ShowMenu();
     });
    }));
   }
   dialogue.StartMenu(task.speaker, task.prompt, choices);
  }

  if (actor != null) movement.FocusOn(PortraitFocus.For(actor));
  dialogue.StartLines(task.speaker, task.lines, ShowMenu);
  yield return new WaitUntil(() => complete);
  if (actor != null) actor.gameObject.SetActive(false);
  movement.ResetZoom();
 }
 public void CompletePuzzle(MemoryPuzzleDefinition puzzle){Flags.Add(puzzle.id+"_complete");AddDocument(puzzle.onCompleteDocument);}
 private void InspectDocument(string id){dialogue.EndDialogue();movement.ResetZoom();Mode="room";OpenStorage(Content.documents.First(d=>d.id==id).storage);inspected=id;journalReturn=()=>{movement.FocusOn(PortraitFocus.For(guest.transform));Menu();};}
 public bool IsReadingArchive => Mode=="archive"&&inspected!=null;
 public void ReadArchive(string id){if(Mode=="archive")inspected=id;}
 public void CloseJournal()
 {
  if(storage=="archive"&&RoomStationsController.Instance!=null)RoomStationsController.Instance.Hide();
  if(storage=="board"&&Flags.Contains("tutorial_board_opened"))Flags.Add("tutorial_board_done");
  if(storage=="archive"&&Flags.Contains("tutorial_archive_opened"))Flags.Add("tutorial_archive_done");
  if(journalReturn!=null){var callback=journalReturn;journalReturn=null;Mode="room";movement.enabled=true;callback();return;}
  Mode=AtEnding?"ending":"room";movement.enabled=Mode=="room";
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
 public void InspectReward(InspectableReward reward){if(Mode!="room"||dialogue.IsDialogueActive)return;inspectedReward=reward;Mode="reward";movement.enabled=false;dialogue.StartLines("Вы",new[]{reward.Description},null);}
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
   if(doc.id=="archive_rules")
   {
    var sheet=new Rect(Screen.width*.22f,35*s,Screen.width*.56f,Screen.height-75*s);
    GUI.DrawTexture(sheet,Resources.Load<Texture2D>("Demo/UI/rules-paper"),ScaleMode.StretchToFill);
    var title=RoomReadableUi.PaperLabel(Mathf.RoundToInt(25*s),FontStyle.Bold);title.alignment=TextAnchor.MiddleCenter;
    GUI.Label(new Rect(sheet.x+30*s,sheet.y+30*s,sheet.width-60*s,55*s),doc.title,title);
    var rules=RoomReadableUi.PaperLabel(Mathf.RoundToInt(20*s));rules.wordWrap=true;
    GUI.Label(new Rect(sheet.x+90*s,sheet.y+95*s,sheet.width-180*s,sheet.height-130*s),doc.text,rules);
   }
   else if(image!=null)GUI.DrawTexture(new Rect(Screen.width*.22f,50*s,Screen.width*.56f,Screen.height-130*s),image,ScaleMode.ScaleToFit);
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
   if(RoomReadableUi.PaperButton(new Rect(20,20,200*s,46*s),AtEnding?"Продолжение следует":"Назад в комнату"))CloseJournal();
  }
 }
 public static Rect FitImage(Rect area,float aspect)
 {
  Vector2 size=area.width/area.height>aspect?new Vector2(area.height*aspect,area.height):new Vector2(area.width,area.width/aspect);
  return new Rect(area.center-size*.5f,size);
 }
 public static Vector2 UnrotatePointer(Vector2 point,Vector2 centre,float degrees)=>centre+(Vector2)(Quaternion.Euler(0,0,-degrees)*(point-centre));
 private void DrawBoard(CampaignDocument[] docs,float s)
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
 private void DrawArchive(CampaignDocument[] docs,float s)
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
 private void OnDestroy(){if(Instance==this)Instance=null;}
}
