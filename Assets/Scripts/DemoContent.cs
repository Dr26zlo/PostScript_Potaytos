using System;
[Serializable] public class DemoContent
{
 public DemoDocument[] documents; public DemoOrder[] orders; public DemoPuzzle[] puzzles; public string[] flow; public string[] wife; public DemoImp imp; public DemoEnding ending;
}
[Serializable] public class DemoDocument { public string id,title,text,png; }
[Serializable] public class DemoOrder { public string id,speaker,npc,reward; public string[] intro; public DemoQuestion[] questions; public DemoOption[] options; public DemoCalls calls; }
[Serializable] public class DemoQuestion { public string id,label,answer,inspect,document,action,requires; }
[Serializable] public class DemoOption { public string id,label,text,requires,response,document; public bool reward; }
[Serializable] public class DemoCalls { public string[] precise,plain,reveal,bridge,market; public string[] Get(string key) { switch(key){case "precise":return precise;case "plain":return plain;case "reveal":return reveal;case "bridge":return bridge;case "market":return market;default:return null;} } }
[Serializable] public class DemoPuzzle { public string id,mode,title,onCompleteDocument,hint; public DemoPuzzleRow[] rows; public string[] phrases,junk; }
[Serializable] public class DemoPuzzleRow { public string[] parts; }
[Serializable] public class DemoImp { public string intro,outro,document; public DemoImpChoice[] choices; }
[Serializable] public class DemoImpChoice { public string label,answer; }
[Serializable] public class DemoEnding { public string always,preserve,erase,restored,check,@false,goal,title; }
[Serializable] public class DemoSave { public int step; public string[] flags,results; public DemoDocument[] errors; }
