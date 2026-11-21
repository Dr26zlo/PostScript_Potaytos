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
 static HashSet<string> asked=new HashSet<string>();static string current="";static int route;static bool printed,replaced;
 static double start,last,ringAt,endingCaptureAt;static HashSet<string> captures=new HashSet<string>();static string report="";static int screenshots;
 static DemoQa(){EditorApplication.update+=Tick;}
 public static void Capture(){SessionState.SetBool("Postcript.CaptureOnly",true);Run();}
 public static void Run(){PlayerSettings.runInBackground=true;Application.runInBackground=true;Environment.SetEnvironmentVariable("POSTCRIPT_QA","1");SessionState.SetBool("Postcript.Qa",true);route=0;start=EditorApplication.timeSinceStartup;EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
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
   if(start==0)start=last;if(last-start>600)throw new Exception("Route timed out "+route+" mode="+d.Mode+" step="+d.Step);
   var runner=UnityEngine.Object.FindObjectOfType<DialogueRunner>();
   if(d.Mode=="welcome"){d.Begin(false);return;}
   if(d.Mode=="journal"){string key="journal:"+route+":"+d.Step;if(captures.Add(key)){ScreenCapture.CaptureScreenshot("/tmp/postcript-journal-"+route+"-"+d.Step+".png");return;}d.CloseJournal();return;}
   if(d.Mode=="ending")
   {
    if(endingCaptureAt==0){endingCaptureAt=last;ScreenCapture.CaptureScreenshot("/tmp/postcript-ending-"+route+".png");return;}if(last-endingCaptureAt<1)return;
    Assert(d.Results.Count==4,"Four orders not completed");
    if(route==3){foreach(var o in d.Content.orders){Assert(d.Results[o.id]=="typo","Typo result wrong");Assert(d.Flags.Contains("error_"+o.id),"Missing factual typo document");}}
    foreach(var order in d.Content.orders){var result=d.Results[order.id];bool expect=order.options.Any(o=>o.id==result&&o.reward);var reward=UnityEngine.Object.FindObjectsOfType<DemoReward>(true).FirstOrDefault(r=>r.Key==order.reward);Assert(reward!=null && reward.gameObject.activeInHierarchy==expect,"Actual reward state wrong: "+order.reward);}
    Log("Route "+route+" reached ending: "+string.Join(",",d.Results.Select(x=>x.Key+"="+x.Value)));
    route++;endingCaptureAt=0;
    if(route>=Routes.Length || SessionState.GetBool("Postcript.CaptureOnly",false)){SessionState.SetBool("Postcript.Qa",false);SessionState.SetBool("Postcript.QaQuit",true);SessionState.SetInt("Postcript.QaExit",0);EditorApplication.ExitPlaymode();return;}
    current="";asked.Clear();printed=false;replaced=false;start=last;
    UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");return;
   }
   if(d.Mode=="memory"){string key="memory:"+route+":"+d.Step;if(captures.Add(key)){ScreenCapture.CaptureScreenshot("/tmp/postcript-memory-"+route+"-"+d.Step+".png");return;}SolvePuzzle(UnityEngine.Object.FindObjectOfType<DemoMemoryView>());return;}
   var door=UnityEngine.Object.FindObjectOfType<GuestDoor>();
   if(door!=null && door.IsWaiting){Assert(!runner.IsDialogueActive,"Dialogue before door click");door.OpenForWaitingGuest();return;}
   if(d.Mode=="phone")
   {
    var phone=UnityEngine.Object.FindObjectOfType<RoomPhoneCall>();
    if(phone==null)return;
    if(phone.IsRinging){if(ringAt==0){ringAt=last;return;}if(last-ringAt<.6)return;Assert(!runner.IsDialogueActive,"Call started before click");phone.Answer();ringAt=0;Log("Telephone waits for click");return;}
   }
   if(runner.IsDialogueActive)
   {
    var node=(DialogueObject)Get(runner,"currentNode");
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
    asked.Add(node.speakerName=="Имп"?"imp:"+choice.choiceText:current+":"+choice.choiceText);
    if(screenshots<2){ScreenCapture.CaptureScreenshot("/tmp/postcript-dialogue-"+screenshots+".png");screenshots++;}
    if(choice.action!=null)choice.action();else Call(runner,"OnChoiceClicked",choice);return;
   }
   var guest=UnityEngine.Object.FindObjectOfType<Guest>();if(guest==null || (bool)Get(guest,"arriving"))return;
   var orderNow=(DemoOrder)Get(d,"order");if(orderNow==null)return;
   if(current!=orderNow.id){current=orderNow.id;printed=false;replaced=false;}
   var selected=(DemoOption)Get(d,"selected");
   if(selected==null){guest.Interact();return;}
   var machine=UnityEngine.Object.FindObjectOfType<TypewriterStation>();var zoom=machine.GetComponentInChildren<TypewriterZoomArea>();
   if(machine.HasDeliveryWarning)
   {
    if(!replaced){machine.ResolveDeliveryWarning(false);Assert(machine.FinishedSheetCount==0,"Discarded sheet remains queued");replaced=true;printed=false;Log("Discard removes corrupted sheet from queue");}
    else machine.ResolveDeliveryWarning(true);return;
   }
   if(!printed)
   {
    zoom.EnterTypewriterMode();if(!zoom.CanType)return;
    Assert(machine.TryInsertSheet(),"Cannot insert paper");
    string text=route==3?"Ошибка":selected.text;int col=0;
    foreach(var word in text.Split((char[])null,StringSplitOptions.RemoveEmptyEntries))
    {
     if(col>0 && col+word.Length+1>28){Call(machine,"FinishReturn");col=0;}
     if(col>0){Assert((bool)Call(machine,"TryPrint",' '),"Cannot print space");col++;}
     foreach(char ch in word){Assert((bool)Call(machine,"TryPrint",char.ToUpperInvariant(ch)),"Cannot print letter "+ch);col++;}
    }
    Assert(machine.TryRemoveSheet(),"Cannot remove sheet");printed=true;zoom.ExitTypewriterMode();Log("Printed and removed actual order sheet "+current);return;
   }
   guest.Interact();
  }
  catch(Exception ex){File.AppendAllText("/tmp/postcript-qa.txt","FAIL "+ex+"\n");SessionState.SetBool("Postcript.Qa",false);SessionState.SetBool("Postcript.QaQuit",true);SessionState.SetInt("Postcript.QaExit",1);EditorApplication.ExitPlaymode();}
 }
 static void SolvePuzzle(DemoMemoryView view)
 {
  var puzzle=(DemoPuzzle)Get(view,"puzzle");var state=Get(view,"state");var pieces=(IList)Get(state,"pieces");
  for(int row=0;row<puzzle.rows.Length;row++)
  {
   object first=null;for(int i=0;i<pieces.Count;i++)if((int)Get(pieces[i],"row")==row){if(first==null){first=pieces[i];continue;}var piece=pieces[i];var rect=(Rect)Get(piece,"rect");var origin=(Rect)Get(first,"rect");rect.position=origin.position+Vector2.right*((int)Get(piece,"col")*224f/240*190);piece.GetType().GetField("rect").SetValue(piece,rect);Call(view,"Snap",piece);}
  }
  Assert((bool)Get(state,"complete"),"Puzzle did not complete through real snapping");Assert(DemoDirector.Instance.Flags.Contains(puzzle.id+"_complete"),"Missing puzzle discovery");
  Call(view,"Open",puzzle,(Action)(()=>{}));Assert((bool)Get(Get(view,"state"),"complete"),"Puzzle state lost on return");
  // Restore the actual close path to resume the client's menu.
  typeof(DemoMemoryView).GetField("puzzle",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,null);
  var d=DemoDirector.Instance;typeof(DemoDirector).GetProperty("Mode").GetSetMethod(true).Invoke(d,new object[]{"room"});
  d.GetComponent<CameraMovement>().enabled=true;Call(d,"Menu");Log("Puzzle "+puzzle.id+" joins and retains state");
 }
}
