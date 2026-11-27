using System;
[Serializable] public class CampaignContent
{
 public TutorialPrompts tutorial; public CampaignDocument[] documents; public OrderDefinition[] orders; public MemoryPuzzleDefinition[] puzzles; public TaskCycle.CampaignEntry[] tasks; public CampaignEnding ending;
}
[Serializable] public class CampaignDocument { public string id,title,text,png,storage; }
[Serializable] public class OrderDefinition { public string id,speaker,npc,reward; public string[] intro; public OrderQuestion[] questions; public OrderOption[] options; public OrderCall[] calls; }
[Serializable] public class OrderQuestion { public string id,label,answer,inspect,document,action,requires; }
[Serializable] public class OrderOption { public string id,label,text,requires,response,document; public bool reward,requiresLineBreak; }
[Serializable] public class OrderCall { public string result, document; public string[] lines; }
[Serializable] public class MemoryPuzzleDefinition { public string id,mode,title,onCompleteDocument,hint; public MemoryPuzzleRow[] rows; public string[] phrases,junk; }
[Serializable] public class MemoryPuzzleRow { public string[] parts; }
[Serializable] public class CampaignEnding { public string always,preserve,erase,restored,check,@false,goal,title; }
[Serializable] public class CampaignSave { public int step; public string[] flags,results; public CampaignDocument[] errors; }

[Serializable] public class TutorialPrompts { public string board,archive,door,lineBreak; }

// я это даже комментировать не хочу...