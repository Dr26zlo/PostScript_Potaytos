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
    public bool IsDialogueActive => isDialogueActive;
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

    private readonly List<DialogueObject> runtimeNodes = new List<DialogueObject>();

    public DialogueObject CreateNode(string speaker, string text)
    {
        var node = ScriptableObject.CreateInstance<DialogueObject>();
        node.speakerName = speaker;
        node.dialogueText = text;
        runtimeNodes.Add(node);
        return node;
    }

    public void StartMenu(string speaker, string prompt, IEnumerable<DialogueChoice> choices)
    {
        var node = CreateNode(speaker, prompt);
        node.choices.AddRange(choices);
        StartDialogue(node);
    }

    public void StartLines(string speaker, IEnumerable<string> lines, System.Action finished)
    {
        DialogueObject first = null, last = null;
        foreach (string line in lines)
        {
            var node = CreateNode(speaker, line);
            if (last == null) first = node;
            else last.nextNode = node;
            last = node;
        }

        if (first == null)
        {
            finished?.Invoke();
            return;
        }
        if (finished != null)
        {
            void OnFinished()
            {
                onDialogueEnd.RemoveListener(OnFinished);
                finished();
            }
            onDialogueEnd.AddListener(OnFinished);
        }
        StartDialogue(first);
    }

    private void OnDestroy()
    {
        foreach (var node in runtimeNodes)
            Destroy(node);
    }

    public event System.Action<DialogueObject> NodeShown;

    private void ShowNode(DialogueObject node)
    {
        choicesContainer.gameObject.SetActive(true);
        currentNode = node;

        if (nameText != null) nameText.text = node.speakerName;
        if (dialogueText != null) dialogueText.text = node.dialogueText;

        foreach (Transform child in choicesContainer)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

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
        NodeShown?.Invoke(node);
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
        if (choice.action != null) { choice.action(); return; }
        string id = string.IsNullOrEmpty(choice.choiceID) ? choice.choiceText : choice.choiceID;

        if (choiceHistory.ContainsKey(id)) choiceHistory[id]++;
        else choiceHistory[id] = 1;

        onChoiceSelected?.Invoke(id);

        if (choice.nextNode != null)
            ShowNode(choice.nextNode);
        else
            EndDialogue();
    }

    public void EndDialogue()
    {
        isDialogueActive = false;
        choicesContainer.gameObject.SetActive(false);
        MovePanel(hiddenPosition);
        onDialogueEnd?.Invoke();
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