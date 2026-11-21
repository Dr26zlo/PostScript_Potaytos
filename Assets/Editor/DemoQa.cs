using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class DemoQa
{
 static readonly string[] Routes={"precise,bridge,preserve,restored","reveal,market,erase,false","plain,bridge,preserve,check","typo,typo,typo,typo"};
 static HashSet<string> asked=new HashSet<string>();static string current="";static int route;static bool printed,replaced,printing;static int printRow;
 static double start,last,ringAt,endingCaptureAt;static HashSet<string> captures=new HashSet<string>();static string report="";static int screenshots;static double holdUntil;static HashSet<string> verified=new HashSet<string>();
 static DemoQa(){EditorApplication.update+=Tick;}
 public static void IntroCapture(){Run();SessionState.SetBool("Postcript.IntroOnly",true);}
 public static void Capture(){Run();SessionState.SetBool("Postcript.CaptureOnly",true);}
 public static void Run(){SessionState.SetBool("Postcript.IntroOnly",false);SetView(1280,720);SessionState.SetBool("Postcript.CaptureOnly",false);File.WriteAllText("/tmp/postcript-qa.txt", "Revision 21-11: runtime QA (method-driven, physical gestures checked separately)\n");SessionState.SetBool("Postcript.QaQuit",false);PlayerSettings.runInBackground=true;Application.runInBackground=true;Environment.SetEnvironmentVariable("POSTCRIPT_QA","1");SessionState.SetBool("Postcript.Qa",true);route=0;start=EditorApplication.timeSinceStartup;EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
 static object Get(object o,string name)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(o);
 static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(o,args);
 static void Assert(bool test,string why){if(!test)throw new Exception(why);}
 static void Log(string s){report+="PASS "+s+"\n";File.WriteAllText("/tmp/postcript-qa.txt",report);}
 static void Tick()
 {
  if(SessionState.GetBool("Postcript.QaQuit",false) && !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool("Postcript.QaQuit",false);EditorApplication.Exit(SessionState.GetInt("Postcript.QaExit",0));return;}
  if(!SessionState.GetBool("Postcript.Qa",false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
  EditorApplication.QueuePlayerLoopUpdate();
  if(EditorApplication.timeSinceStartup-last<.15)return;last=EditorApplication.timeSinceStartup;
  try
  {
   var d=DemoDirector.Instance;File.WriteAllText("/tmp/postcript-qa-state.txt","playing="+EditorApplication.isPlaying+" director="+(d!=null)+" mode="+(d!=null?d.Mode:"null")+" step="+(d!=null?d.Step:0)+" frame="+Time.frameCount+" time="+Time.time+" door="+(UnityEngine.Object.FindObjectOfType<GuestDoor>()!=null && UnityEngine.Object.FindObjectOfType<GuestDoor>().IsWaiting)+" guests="+UnityEngine.Object.FindObjectsOfType<Guest>().Length+" arriving="+(UnityEngine.Object.FindObjectOfType<Guest>()!=null?Get(UnityEngine.Object.FindObjectOfType<Guest>(),"arriving"):null)+" dialogue="+(UnityEngine.Object.FindObjectOfType<DialogueRunner>()!=null&&UnityEngine.Object.FindObjectOfType<DialogueRunner>().IsDialogueActive)+" selected="+(d!=null?Get(d,"selected"):null)+" machineFocus="+(UnityEngine.Object.FindObjectOfType<TypewriterStation>()!=null&&UnityEngine.Object.FindObjectOfType<TypewriterStation>().IsFocused));if(d==null || Get(d,"movement")==null)return;
   if(last<holdUntil)return;
   if(start==0)start=last;if(last-start>600)throw new Exception("Route timed out "+route+" mode="+d.Mode+" step="+d.Step);
   var runner=UnityEngine.Object.FindObjectOfType<DialogueRunner>();
   if(d.Mode=="welcome"){d.Begin(false);return;}
   var guidance=UnityEngine.Object.FindObjectOfType<DemoGuidance>();
   if(d.Mode=="room"&&guidance!=null&&(guidance.Stage=="board"||guidance.Stage=="archive"))
   {
    Assert(UnityEngine.Object.FindObjectsOfType<Guest>().Length==0,"Guest before tutorial completion");
    string stage=guidance.Stage;
    if(verified.Add("settle-hint:"+route+stage)){holdUntil=last+1.5;return;}
    if(verified.Add("hint:"+route+stage)){ScreenCapture.CaptureScreenshot("/tmp/postcript-hint-"+stage+"-"+route+".png");holdUntil=last+.6;return;}
    if(stage=="board"&&SessionState.GetBool("Postcript.IntroOnly",false)){Log("Intro framing captured after awakening; camera="+Camera.main.transform.position+" size="+Camera.main.orthographicSize+" target="+d.GetComponent<CameraMovement>().TutorialOverviewTarget);SessionState.SetBool("Postcript.Qa",false);SessionState.SetBool("Postcript.QaQuit",true);SessionState.SetInt("Postcript.QaExit",0);EditorApplication.ExitPlaymode();return;}
    if(stage=="board"){d.OpenArchive();Assert(d.Mode=="room","Archive bypasses board tutorial");d.OpenJournal();Assert(d.Mode=="journal","Board not opened");}
    else{d.OpenJournal();Assert(d.Mode=="room","Board bypasses archive tutorial");d.OpenArchive();Assert(d.Mode=="archive","Archive not opened");}
    Log("Tutorial "+stage+" waits for world interaction, no guest before close");return;
   }
   if(d.Mode=="journal"||d.Mode=="archive"){string key=d.Mode+":"+route+":"+d.Step;if(captures.Add(key)){ScreenCapture.CaptureScreenshot("/tmp/postcript-"+d.Mode+"-"+route+"-"+d.Step+".png");holdUntil=last+.6;return;}if(d.Mode=="archive")Assert(d.Content.documents.First(x=>x.id=="archive_rules").storage=="archive","Rules missing archive storage");d.CloseJournal();return;}
   if(d.Mode=="ending")
   {
    if(endingCaptureAt==0){endingCaptureAt=last;ScreenCapture.CaptureScreenshot("/tmp/postcript-ending-"+route+".png");return;}if(last-endingCaptureAt<1)return;
    Assert(d.Results.Count==4,"Four orders not completed");
    if(route==3){foreach(var o in d.Content.orders){Assert(d.Results[o.id]=="typo","Typo result wrong");Assert(d.Flags.Contains("error_"+o.id),"Missing factual typo document");}}
    foreach(var order in d.Content.orders){var result=d.Results[order.id];bool expect=order.options.Any(o=>o.id==result&&o.reward);var reward=UnityEngine.Object.FindObjectsOfType<DemoReward>(true).FirstOrDefault(r=>r.Key==order.reward);Assert(reward!=null && reward.gameObject.activeInHierarchy==expect,"Actual reward state wrong: "+order.reward);
     if(expect){typeof(DemoDirector).GetProperty("Mode").GetSetMethod(true).Invoke(d,new object[]{"room"});d.InspectReward(reward);Assert(d.Mode=="reward","Reward inspector did not open");d.CloseReward();typeof(DemoDirector).GetProperty("Mode").GetSetMethod(true).Invoke(d,new object[]{"ending"});}}
    Log("Route "+route+" reached ending: "+string.Join(",",d.Results.Select(x=>x.Key+"="+x.Value)));
    route++;endingCaptureAt=0;
    if(route>=Routes.Length || SessionState.GetBool("Postcript.CaptureOnly",false)){SessionState.SetBool("Postcript.Qa",false);SessionState.SetBool("Postcript.QaQuit",true);SessionState.SetInt("Postcript.QaExit",0);EditorApplication.ExitPlaymode();return;}
    if(route==2)SetView(1920,1080);
    current="";asked.Clear();printed=false;replaced=false;printing=false;printRow=0;start=last;
    UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");return;
   }
   if(d.Mode=="memory"){string key="memory:"+route+":"+d.Step;if(captures.Add(key)){ScreenCapture.CaptureScreenshot("/tmp/postcript-memory-"+route+"-"+d.Step+".png");holdUntil=last+.7;return;}SolvePuzzle(UnityEngine.Object.FindObjectOfType<DemoMemoryView>());return;}
   var door=UnityEngine.Object.FindObjectOfType<GuestDoor>();
   if(door!=null && door.IsWaiting){Assert(d.Flags.Contains("tutorial_board_done")&&d.Flags.Contains("tutorial_archive_done"),"Guest bypassed tutorial");Assert(!runner.IsDialogueActive,"Dialogue before door click");door.OpenForWaitingGuest();return;}
   if(d.Mode=="phone")
   {
    var phone=UnityEngine.Object.FindObjectOfType<RoomPhoneCall>();
    if(phone==null)return;
    if(phone.IsRinging){if(ringAt==0){ringAt=last;return;}if(last-ringAt<.6)return;Assert(!runner.IsDialogueActive,"Call started before click");phone.Answer();ringAt=0;Log("Telephone waits for click");return;}
   }
   if(runner.IsDialogueActive)
   {
    UnityEngine.Canvas.ForceUpdateCanvases();
    var backing=runner.choicesContainer.GetComponent<UnityEngine.UI.Image>();Assert(backing==null||!backing.enabled,"Common answer backing enabled");
    foreach(Transform child in runner.choicesContainer)if(child.gameObject.activeSelf){var paper=child.GetComponent<UnityEngine.UI.Image>();Assert(paper!=null&&paper.enabled&&paper.sprite!=null,"Answer missing individual paper");var label=child.GetComponentInChildren<UnityEngine.UI.Text>();Assert(label.color.r<.4f,"Answer ink is not dark");Assert(label.rectTransform.rect.width>0&&label.rectTransform.rect.height>0,"Answer padding consumes label");}

    var node=(DialogueObject)Get(runner,"currentNode");
    var focus=d.GetComponent<CameraMovement>();
    if(focus.CurrentFocus!=null&&!focus.HasReachedFocus(focus.CurrentFocus))return;
    if(node.speakerName!="Имп"&&node.speakerName!="Вы"&&focus.CurrentFocus!=null&&verified.Add("face:"+route+node.speakerName)){ScreenCapture.CaptureScreenshot("/tmp/postcript-face-"+route+"-"+node.speakerName+".png");holdUntil=last+.5;return;}
    if(node.choices.Count==0){runner.OnScreenClicked();return;}
    DialogueChoice choice=null;
    if(node.speakerName=="Имп")choice=node.choices.FirstOrDefault(c=>!asked.Contains("imp:"+c.choiceText))??node.choices.Last();
    else if(node.dialogueText=="Какой текст напечатать?")
    {
     var order=(DemoOrder)Get(d,"order");int index=Array.FindIndex(d.Content.orders,x=>x.id==order.id);string id=Routes[route].Split(',')[index];
     var opt=order.options.FirstOrDefault(o=>o.id==id)??order.options.First(o=>d.Available(o.requires));choice=node.choices.First(c=>c.choiceText==opt.label);
    }
    else if(node.dialogueText=="Что вы хотите уточнить?")
    {
     choice=node.choices.FirstOrDefault(c=>!asked.Contains(current+":"+c.choiceText)&&c.choiceText!="Перейти к тексту заказа"&&c.choiceText!="Вернуться к комнате")??node.choices.First(c=>c.choiceText=="Перейти к тексту заказа");
    }
    else choice=node.choices[0];
    if(screenshots<2){ScreenCapture.CaptureScreenshot("/tmp/postcript-dialogue-"+screenshots+".png");screenshots++;holdUntil=last+.6;return;}
    asked.Add(node.speakerName=="Имп"?"imp:"+choice.choiceText:current+":"+choice.choiceText);
    if(choice.action!=null)choice.action();else Call(runner,"OnChoiceClicked",choice);return;
   }
   var guest=UnityEngine.Object.FindObjectOfType<Guest>();if(guest==null || (bool)Get(guest,"arriving"))return;
   var orderNow=(DemoOrder)Get(d,"order");if(orderNow==null)return;
   if(current!=orderNow.id){current=orderNow.id;printed=false;replaced=false;printing=false;printRow=0;
    var move=d.GetComponent<CameraMovement>();Transform face=PortraitFocus.For(guest.transform);Vector3 anchor=face.position;var renderer=guest.GetComponent<SpriteRenderer>();var original=renderer.sprite;var before=SpriteHit.OpaqueBounds(original);float height=before.size.y*guest.transform.lossyScale.y;float foot=guest.transform.TransformPoint(before.min).y;
    foreach(string emotion in new[]{"question","happy","angry","neutral"}){Call(d,"Portrait",emotion);var bounds=SpriteHit.OpaqueBounds(renderer.sprite);Assert(Mathf.Abs(bounds.size.y*guest.transform.lossyScale.y-height)<.001f,"Pose apparent height changed");Assert(Mathf.Abs(guest.transform.TransformPoint(bounds.min).y-foot)<.001f,"Pose feet moved");Assert(Vector3.Distance(face.position,anchor)<.001f,"Face focus moved across poses");}
    Log("Stable pose footprint and shared face focus: "+current);
   }
   var selected=(DemoOption)Get(d,"selected");
   if(selected==null){guest.Interact();return;}
   var machine=UnityEngine.Object.FindObjectOfType<TypewriterStation>();var zoom=machine.GetComponentInChildren<TypewriterZoomArea>();
   if(machine.HasDeliveryWarning)
   {
    if(!replaced){machine.ResolveDeliveryWarning(false);Assert(machine.FinishedSheetCount==0,"Discarded sheet remains queued");replaced=true;printed=false;printing=false;printRow=0;Log("Discard removes corrupted sheet from queue");}
    else machine.ResolveDeliveryWarning(true);return;
   }
   if(!printed)
   {
    zoom.EnterTypewriterMode();if(!zoom.CanType)return;
    if(!printing){Assert(machine.TryInsertSheet(),"Cannot insert paper");printing=true;printRow=0;}
    string text=route==3?"Ошибка":selected.text;
    if(selected.requiresLineBreak){Assert(!guest.MatchesOrder(selected.text.Replace('\n',' ')),"Flattened multiline accepted");Assert(guest.MatchesOrder("  "+selected.text.ToUpperInvariant().Replace('ё','е').Replace(" ","  ")+"  "),"Normalized multiline rejected");}
    var rows=text.Split('\n');
    if(printRow<rows.Length)
    {
     int row=printRow;
     if(row>0){Call(machine,"FinishReturn");Assert(!machine.HasLineBreakHint,"Line reminder remained after real return");}
     int col=0;foreach(var word in rows[row].Split((char[])null,StringSplitOptions.RemoveEmptyEntries))
     {
      if(col>0&&col+word.Length+1>28){Call(machine,"FinishReturn");col=0;}
      if(col>0){Assert((bool)Call(machine,"TryPrint",' '),"Cannot print space");col++;}
      foreach(char ch in word){Assert((bool)Call(machine,"TryPrint",char.ToUpperInvariant(ch)),"Cannot print letter "+ch);col++;}
     }
     if(row==0&&rows.Length>1){Assert(machine.HasLineBreakHint,"Missing required manual-return reminder");ScreenCapture.CaptureScreenshot("/tmp/postcript-linebreak-"+route+"-"+current+".png");}
     printRow++;holdUntil=last+.65;return;
    }
    Assert(machine.TryRemoveSheet(),"Cannot remove sheet");printed=true;printing=false;zoom.ExitTypewriterMode();Log("Printed and removed actual order sheet "+current);return;
   }
   guest.Interact();
  }
  catch(Exception ex){File.AppendAllText("/tmp/postcript-qa.txt","FAIL "+ex+"\n");SessionState.SetBool("Postcript.Qa",false);SessionState.SetBool("Postcript.QaQuit",true);SessionState.SetInt("Postcript.QaExit",1);EditorApplication.ExitPlaymode();}
 }
 static void SetView(int width,int height)
 {
  var assembly=typeof(Editor).Assembly;var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
  var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);var sizes=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);
  var groupType=assembly.GetType("UnityEditor.GameViewSizeGroupType");var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{Enum.Parse(groupType,"Standalone")});
  var sizeType=assembly.GetType("UnityEditor.GameViewSize");var kindType=assembly.GetType("UnityEditor.GameViewSizeType");var constructor=sizeType.GetConstructor(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new[]{kindType,typeof(int),typeof(int),typeof(string)},null);
  var size=constructor.Invoke(new[]{Enum.Parse(kindType,"FixedResolution"),(object)width,height,"QA "+width+"x"+height});
  group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});int index=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null)-1;
  var gameViewType=assembly.GetType("UnityEditor.GameView");var window=EditorWindow.GetWindow(gameViewType);gameViewType.GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window,index);window.Repaint();
 }
 static void SolvePuzzle(DemoMemoryView view)
 {
  var frameCapture=ScreenCapture.CaptureScreenshotAsTexture();
  if(frameCapture!=null){var pixels=frameCapture.GetPixels32();int cyan=pixels.Count(c=>c.r<20&&c.g>200&&c.b>200);Assert(cyan<pixels.Length/200,"Rendered memory is obscured by cyan");UnityEngine.Object.Destroy(frameCapture);}
  var puzzle=(DemoPuzzle)Get(view,"puzzle");var state=Get(view,"state");var pieces=(IList)Get(state,"pieces");
  Assert(view.GetComponentsInChildren<UnityEngine.UI.Mask>().Length==0,"UI unexpectedly parented to camera");
  if(puzzle.mode=="memory"&&route==0){var t=Resources.Load<Texture2D>("Demo/Memory/graphite-rim");var copy=new Texture2D(t.width,t.height,TextureFormat.RGBA32,false);copy.SetPixels32(t.GetPixels32());copy.Apply();File.WriteAllBytes("/tmp/postcript-loaded-rim.png",copy.EncodeToPNG());UnityEngine.Object.Destroy(copy);Log("Rim loaded centre="+t.GetPixel(t.width/2,t.height/2)+" corner="+t.GetPixel(0,0));}
  var ui=(GameObject)Get(view,"ui");Assert(ui.GetComponentInChildren<UnityEngine.UI.Mask>()!=null,"No native visual mask");
  var handles=ui.GetComponentsInChildren<MemoryDragHandle>();Assert(handles.Length>=pieces.Count,"Missing native drag handlers");
  foreach(var handle in handles){var image=handle.GetComponent<UnityEngine.UI.Image>();Assert(image.alphaHitTestMinimumThreshold>.1f,"Rectangular memory hit target");Assert(image.sprite.texture.isReadable,"Alpha hits require readable texture");}
  for(int row=0;row<puzzle.rows.Length;row++)
  {
   var rowPieces=pieces.Cast<object>().Where(p=>(int)Get(p,"row")==row).OrderBy(p=>(int)Get(p,"col")).ToArray();
   float width=190+(rowPieces.Length-1)*224f/240f*190f;var origin=new Vector2(640-width/2,280+row*100);
   foreach(var piece in rowPieces){var rect=(Rect)Get(piece,"rect");rect.position=origin+Vector2.right*((int)Get(piece,"col")*224f/240*190);piece.GetType().GetField("rect").SetValue(piece,rect);if((int)Get(piece,"col")>0)Call(view,"Snap",piece);}
  }
  Assert((bool)Get(state,"complete"),"Puzzle did not complete through real snapping");Assert(DemoDirector.Instance.Flags.Contains(puzzle.id+"_complete"),"Missing puzzle discovery");
  foreach(Vector2 proposed in new[]{new Vector2(9000,0),new Vector2(-9000,0),new Vector2(0,9000),new Vector2(0,-9000)})
  {var delta=(Vector2)Call(view,"ConstrainDelta",0,false,proposed);Assert((bool)Call(view,"Allowed",0,false,delta),"Whole joined group escapes mask");Assert(delta.magnitude<proposed.magnitude,"No group clamp");}
  if(puzzle.mode=="memory")
  {
   var frame=handles.Select(h=>h.GetComponent<UnityEngine.UI.Image>()).FirstOrDefault(i=>i.sprite.name=="frame");
   if(frame!=null){var rect=frame.rectTransform;Vector2 point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));Assert(!frame.IsRaycastLocationValid(point,null),"Transparent frame hole intercepts click");}
  }
  var container=(RectTransform)Get(view,"container");var firstRect=(Rect)Get(pieces[0],"rect");
  var startPoint=RectTransformUtility.WorldToScreenPoint(null,container.TransformPoint(new Vector3(firstRect.center.x-640,360-firstRect.center.y)));
  var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=startPoint};
  view.BeginDrag(0,false,data);data.position+=new Vector2(12000,0);view.Drag(data);view.EndDrag();Assert((bool)Call(view,"Allowed",0,false,Vector2.zero),"Native drag event moved group outside ellipse");
  view.Close();var d=DemoDirector.Instance;Call(d,"OpenPuzzle",puzzle.id);Assert((bool)Get(Get(view,"state"),"complete"),"Puzzle state lost on return");view.Close();
  Assert(d.Mode=="room","Puzzle back callback failed");Log("Puzzle "+puzzle.id+": native mask, alpha targets, clamped joined groups, real close/reopen retains state");
 }
}
