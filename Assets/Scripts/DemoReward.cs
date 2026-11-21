using UnityEngine;
public sealed class DemoReward : MonoBehaviour
{
 public string Key { get; private set; }
 public string Title { get; private set; } public string Description { get; private set; }
 private bool hovered;private Sprite paper;
 public static void Attach(GameObject item,string key)
 {
  var reward=item.GetComponent<DemoReward>();if(reward==null)reward=item.AddComponent<DemoReward>();
  reward.Key=key;
  switch(key)
  {
   case "glass_fish":reward.Title="Стеклянная рыба";reward.Description="Он носил её как пресс-папье. Пузырёк внутри всё время смотрит в мою сторону.";break;
   case "paper_tram":reward.Title="Бумажный трамвай";reward.Description="Кондукторша сложила его из старого билета. Если подуть, тихо звенит.";break;
   case "clock":reward.Title="Часы смотрительницы";reward.Description="У смотрительницы они шли, только когда кто-то стучал. Здесь пока тикают.";break;
   default:reward.Title="Рамка для письма";reward.Description="За стеклом сухо, даже когда в комнате сырость. Для письма самое место.";break;
  }
  item.name=reward.Title;
 }
 private void Start(){var runner=FindObjectOfType<DialogueRunner>();if(runner!=null)paper=runner.choiceButtonPrefab.GetComponent<UnityEngine.UI.Image>().sprite;}
 private void Update()
 {
  hovered=false;if(DemoDirector.Instance==null||DemoDirector.Instance.Mode!="room")return;
  var cam=Camera.main;if(cam==null)return;var move=cam.GetComponent<CameraMovement>();if(move.HasFocus)return;
  var ray=cam.ViewportPointToRay(new Vector3(Input.mousePosition.x/Screen.width,Input.mousePosition.y/Screen.height));
  if(!new Plane(Vector3.forward,transform.position).Raycast(ray,out float distance))return;var p=ray.GetPoint(distance);
  foreach(var r in GetComponentsInChildren<SpriteRenderer>()){var b=r.bounds;if(SpriteHit.Contains(r,p))hovered=true;}
  if(hovered&&Input.GetMouseButtonDown(0))DemoDirector.Instance.InspectReward(this);
 }
 private void OnGUI(){if(!hovered)return;float s=RoomReadableUi.Scale;var box=new Rect((Screen.width-500*s)/2,Screen.height-120*s,500*s,100*s);RoomReadableUi.DrawPaper(paper,box);var style=RoomReadableUi.PaperLabel(Mathf.RoundToInt(16*s));style.wordWrap=true;GUI.Label(new Rect(box.x+30*s,box.y+18*s,box.width-60*s,box.height-30*s),Title+"\nНажмите, чтобы рассмотреть",style);}
}
