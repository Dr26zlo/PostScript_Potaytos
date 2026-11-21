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
 private Transform imp; private DemoMemoryView memory; private AudioSource voice;
 private string SavePath => Environment.GetEnvironmentVariable("POSTCRIPT_QA")=="1"?"/tmp/postcript-qa-save.json":Path.Combine(Application.persistentDataPath,"postcript-demo.json");
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Register(){ SceneManager.sceneLoaded-=Install;SceneManager.sceneLoaded+=Install; }
 static void Install(Scene s,LoadSceneMode mode){ if(Camera.main!=null && Camera.main.GetComponent<CameraMovement>()!=null)Camera.main.gameObject.AddComponent<DemoDirector>(); }
 private void Awake()
 {
  Instance=this; Content=JsonUtility.FromJson<DemoContent>(Resources.Load<TextAsset>("Demo/Content").text);
  var cycle=FindObjectOfType<TaskCycle>();if(cycle!=null)cycle.enabled=false;
  foreach(var t in FindObjectsOfType<Transform>(true))if(t.name=="Сидящий имп"){imp=t;var r=t.GetComponent<SpriteRenderer>();var art=Resources.Load<Sprite>("Demo/Art/imp");if(r!=null && art!=null){float bottom=r.bounds.min.y;float height=r.bounds.size.y;r.sprite=art;t.localScale*=height/r.bounds.size.y;t.position+=Vector3.up*(bottom-r.bounds.min.y);}imp.gameObject.SetActive(false);}
 }
 private void Start(){dialogue=FindObjectOfType<DialogueRunner>();movement=GetComponent<CameraMovement>();machine=FindObjectOfType<TypewriterStation>();door=FindObjectOfType<GuestDoor>();memory=gameObject.AddComponent<DemoMemoryView>();voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;dialogue.onDialogueEnd.AddListener(OnDialogueEnd);movement.enabled=false;if(autoStart){autoStart=false;Begin(false);}}
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
 public void AddDocument(string id){if(!string.IsNullOrEmpty(id))Flags.Add(id);}
 public void Begin(bool resume)
 {
  Flags.Clear();Results.Clear();Step=0;
  if(resume && File.Exists(SavePath))try{var save=JsonUtility.FromJson<DemoSave>(File.ReadAllText(SavePath));Step=save.step;foreach(var f in save.flags)Flags.Add(f);if(save.errors!=null)Content.documents=Content.documents.Concat(save.errors).ToArray();foreach(var r in save.results){var bits=r.Split('=');Results[bits[0]]=bits[1];}}catch(Exception){Step=0;Flags.Clear();Results.Clear();}
  foreach(var prior in Content.orders)if(Results.ContainsKey(prior.id)&&prior.options.Any(o=>o.id==Results[prior.id]&&o.reward)){var reward=FindReward(prior.reward);if(reward!=null)reward.SetActive(true);}
  foreach(string id in new[]{"start_memory","start_mixed","start_departure","contract_signature"})AddDocument(id);
  Mode="room";movement.enabled=true;started=true;StartCoroutine(Flow());
 }
 private void Save(){try{File.WriteAllText(SavePath,JsonUtility.ToJson(new DemoSave{step=Step,flags=Flags.ToArray(),results=Results.Select(p=>p.Key+"="+p.Value).ToArray(),errors=Content.documents.Where(d=>d.id.StartsWith("error_")).ToArray()}));}catch(Exception ex){Debug.LogWarning("Не удалось сохранить демо: "+ex.Message);}}
 private IEnumerator Flow()
 {
  for(;Step<Content.flow.Length;Step++)
  {
   Save(); string beat=Content.flow[Step];sequenceDone=false;
   if(beat=="initial_board"){OpenJournal();yield return new WaitUntil(()=>Mode=="room");}
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
  Portrait("question");movement.FocusOn(guest.transform);
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
  memory.Open(Content.puzzles.First(p=>p.id==id),()=>{Mode="room";movement.enabled=true;movement.FocusOn(guest.transform);Menu();});
 }
 private void SelectOrder(DemoOption option){selected=option;guest.SetExpectedText(option.text);machine.TakeOrder(guest);dialogue.EndDialogue();movement.ResetZoom();}
 private void Portrait(string emotion){if(guest==null||order==null)return;var pose=Resources.Load<Sprite>("Demo/Art/"+order.npc+"-"+emotion);if(pose!=null)guest.GetComponent<SpriteRenderer>().sprite=pose;}
 private bool Submit(string text)
 {
  if(selected==null || guest==null)return false;
  bool exact=guest.MatchesOrder(text);bool success=exact && selected.reward;
  Results[order.id]=exact?selected.id:"typo";
  if(exact)AddDocument(selected.document);else { var error=new DemoDocument{id="error_"+order.id,title="Лист с опечаткой · "+order.speaker,text=text+"\n\nКлиент получил лист с опечаткой. Награда не выдана."};var all=Content.documents.ToList();all.Add(error);Content.documents=all.ToArray();AddDocument(error.id);}machine.ReleaseOrder(guest);
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
  if(imp!=null)imp.gameObject.SetActive(true);var intro=Node("Имп",Content.imp.intro+"\nЯ каждый вечер занимаю ваше место дома. Они видят того, кого ждут.");
  foreach(var c in Content.imp.choices){var choice=c;intro.choices.Add(Choice(c.label,()=>Say("Имп",new[]{choice.answer},()=>dialogue.StartDialogue(intro))));}
  intro.choices.Add(Choice("Покажи соглашение",()=>Say("Имп",new[]{Content.imp.outro},()=>{AddDocument(Content.imp.document);sequenceDone=true;})));
  if(imp!=null)movement.FocusOn(imp);dialogue.StartDialogue(intro);yield return new WaitUntil(()=>sequenceDone);
  if(imp!=null)imp.gameObject.SetActive(false);movement.ResetZoom();
 }
 public void CompletePuzzle(DemoPuzzle puzzle){Flags.Add(puzzle.id+"_complete");AddDocument(puzzle.onCompleteDocument);}
 private void InspectDocument(string id){dialogue.EndDialogue();movement.ResetZoom();Mode="room";OpenJournal();inspected=id;journalReturn=()=>{movement.FocusOn(guest.transform);Menu();};}
 public void CloseJournal(){if(journalReturn!=null){var callback=journalReturn;journalReturn=null;Mode="room";movement.enabled=true;callback();return;}Mode=Step<Content.flow.Length && Content.flow[Step]=="ending"?"ending":"room";movement.enabled=Mode=="room";}
 public void OpenJournal(){if(Mode!="room")return;Mode="journal";inspected=null;movement.enabled=false;}
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
  if(Mode!="journal")return;
  GUI.color=Color.black;GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;
  var docs=Content.documents.Where(d=>Flags.Contains(d.id)).ToArray();
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
   if(RoomReadableUi.PaperButton(new Rect(20,20,150*s,42*s),journalReturn!=null?"К разговору":"К доске")){if(journalReturn!=null)CloseJournal();else inspected=null;}
  }
  else
  {
   var board=Resources.Load<Texture2D>("Demo/Art/board");if(board!=null)GUI.DrawTexture(new Rect(10,10,Screen.width-20,Screen.height-20),board,ScaleMode.StretchToFill);
   int cols=docs.Length<=4?2:4;int rows=Mathf.Max(1,Mathf.CeilToInt(docs.Length/(float)cols));float w=(Screen.width-100*s)/cols,h=(Screen.height-150*s)/rows;
   for(int i=0;i<docs.Length;i++){var doc=docs[i];var area=new Rect(50*s+(i%cols)*w,70*s+(i/cols)*h,w-12*s,h-10*s);var image=Resources.Load<Texture2D>("Demo/Documents/"+doc.id);if(image!=null)GUI.DrawTexture(area,image,ScaleMode.ScaleToFit);if((image!=null?GUI.Button(area,"",GUI.skin.label):RoomReadableUi.PaperButton(area,doc.title)))inspected=doc.id;}
   if(RoomReadableUi.PaperButton(new Rect(20,20,170*s,42*s),Step<Content.flow.Length && Content.flow[Step]=="ending"?"Продолжение следует":"Назад в комнату")){Mode=Step<Content.flow.Length && Content.flow[Step]=="ending"?"ending":"room";movement.enabled=Mode=="room";}
  }
 }
 private void OnDestroy(){if(Instance==this)Instance=null;foreach(var n in nodes)if(n!=null)Destroy(n);}
}
