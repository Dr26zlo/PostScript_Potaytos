using UnityEngine;
using UnityEngine.EventSystems;

public class DialogueClickHandler : MonoBehaviour, IPointerClickHandler
{
    public DialogueRunner runner;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (runner != null)
        {
            runner.OnScreenClicked();
        }
    }
}
