using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class DialogueRunner : MonoBehaviour
{
    [Header("UI References")]
    public Text nameText;
    public Text dialogueText;
    public Transform choicesContainer;
    public GameObject choiceButtonPrefab;

    [Header("Animation Settings")]
    public float slideSpeed = 10f;
    private RectTransform panelRect;
    private Vector2 hiddenPosition;
    private Vector2 shownPosition;
    private Coroutine animationCoroutine;

    private bool isDialogueActive = false;
    private DialogueObject currentNode;
    private Dictionary<string, int> choiceHistory = new Dictionary<string, int>();

    public UnityEvent<string> onChoiceSelected;
    public UnityEvent onDialogueEnd;

    private void Awake()
    {
        panelRect = GetComponent<RectTransform>();

        RoomReadableUi.Configure(this);
        shownPosition = panelRect.anchoredPosition;
        hiddenPosition = new Vector2(shownPosition.x, -panelRect.rect.height - 32f);

        panelRect.anchoredPosition = hiddenPosition;
    }

    public void StartDialogue(DialogueObject dialogue)
    {
        if (dialogue == null) return;

        choiceHistory.Clear();
        isDialogueActive = true;

        MovePanel(shownPosition);
        ShowNode(dialogue);
    }

    private void ShowNode(DialogueObject node)
    {
        choicesContainer.gameObject.SetActive(true);
        currentNode = node;

        if (nameText != null) nameText.text = node.speakerName;
        if (dialogueText != null) dialogueText.text = node.dialogueText;

        foreach (Transform child in choicesContainer) Destroy(child.gameObject);

        if (node.choices.Count > 0)
        {
            foreach (var choice in node.choices)
            {
                GameObject btnObj = Instantiate(choiceButtonPrefab, choicesContainer);
                var btnText = btnObj.GetComponentInChildren<Text>();
                if (btnText != null) btnText.text = choice.choiceText;
                RoomReadableUi.ConfigureChoice(btnObj);

                var button = btnObj.GetComponent<Button>();
                var capturedChoice = choice;
                button.onClick.AddListener(() => OnChoiceClicked(capturedChoice));
            }
        } else
        {
            choicesContainer.gameObject.SetActive(false);
        }
        RoomReadableUi.FitContent(this);
        hiddenPosition = new Vector2(shownPosition.x, -panelRect.rect.height - 32f);
    }

    public void OnScreenClicked()
    {
        if (!isDialogueActive) return;

        if (currentNode != null && currentNode.choices.Count > 0) return;

        if (currentNode != null && currentNode.nextNode != null)
        {
            ShowNode(currentNode.nextNode);
        }
        else
            EndDialogue();
    }

    private void OnChoiceClicked(DialogueChoice choice)
    {
        string id = string.IsNullOrEmpty(choice.choiceID) ? choice.choiceText : choice.choiceID;

        if (choiceHistory.ContainsKey(id)) choiceHistory[id]++;
        else choiceHistory[id] = 1;

        onChoiceSelected?.Invoke(id);

        if (choice.nextNode != null)
            ShowNode(choice.nextNode);
        else
            EndDialogue();
    }

    private void EndDialogue()
    {
        isDialogueActive = false;
        onDialogueEnd?.Invoke();

        MovePanel(hiddenPosition);
    }

    private void MovePanel(Vector2 targetPos)
    {
        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        animationCoroutine = StartCoroutine(AnimatePanel(targetPos));
    }

    private IEnumerator AnimatePanel(Vector2 targetPos)
    {
        while (Vector2.Distance(panelRect.anchoredPosition, targetPos) > 0.1f)
        {
            panelRect.anchoredPosition = Vector2.Lerp(panelRect.anchoredPosition, targetPos, Time.deltaTime * slideSpeed);
            yield return null;
        }
        panelRect.anchoredPosition = targetPos;
    }

    public bool HasChosen(string choiceID)
    {
        return choiceHistory.ContainsKey(choiceID) && choiceHistory[choiceID] > 0;
    }
}