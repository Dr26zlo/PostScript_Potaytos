using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(InteractiveObject))]
public sealed class Guest : MonoBehaviour, ITypewriterSheetRecipient
{
    [SerializeField] private DialogueObject dialogue;
    [SerializeField, TextArea(3, 8)] private string requestedText;
    [SerializeField] private DialogueObject correctSheetDialogue;
    [SerializeField] private DialogueObject incorrectSheetDialogue;
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private TypewriterStation typewriter;

    [Header("После выполнения заказа")]
    [SerializeField] private GameObject rewardItem;
    [SerializeField] private Vector2 departureOffset = new Vector2(4f, 0f);
    [SerializeField, Min(0.1f)] private float departureDuration = 1.4f;

    private InteractiveObject interaction;
    private CameraMovement focusedCamera;
    private Action conversationEnded;
    private bool completed;

    public string RequestedText => requestedText;

    private void Awake()
    {
        interaction = GetComponent<InteractiveObject>();
        if (interaction.onClick == null) interaction.onClick = new UnityEvent();
        if (rewardItem != null && transform.IsChildOf(rewardItem.transform))
        {
            Debug.LogWarning("Выберите отдельный объект награды из группы «Предметы».", this);
            rewardItem = null;
        }
        if (rewardItem != null) rewardItem.SetActive(false);
    }

    private void OnEnable() => interaction.onClick.AddListener(Interact);

    private void OnDisable()
    {
        interaction.onClick.RemoveListener(Interact);
        if (conversationEnded != null) DetachFromDialogue();
        if (typewriter != null) typewriter.ReleaseOrder(this);
    }

    public void Interact()
    {
        if (completed || conversationEnded != null || dialogue == null || string.IsNullOrWhiteSpace(requestedText)) return;
        if (dialogueRunner == null) dialogueRunner = FindObjectOfType<DialogueRunner>();
        if (typewriter == null) typewriter = FindObjectOfType<TypewriterStation>();
        if (dialogueRunner == null || typewriter == null || dialogueRunner.IsDialogueActive || typewriter.IsFocused) return;

        if (typewriter.ActiveGuest == this)
        {
            if (!typewriter.TryGiveNextSheet(this))
                typewriter.ShowOrderMessage("Снимите готовый лист с машинки и принесите его гостю.");
            return;
        }

        if (typewriter.ActiveGuest != null)
        {
            typewriter.ShowOrderMessage("Сначала отдайте текущий заказ.");
            return;
        }

        StartConversation(dialogue, () => typewriter.TakeOrder(this));
    }

    private void StartConversation(DialogueObject node, Action onEnded)
    {
        conversationEnded = onEnded;
        focusedCamera = Camera.main != null ? Camera.main.GetComponent<CameraMovement>() : null;
        if (focusedCamera != null) focusedCamera.FocusOn(transform);
        if (dialogueRunner.onDialogueEnd == null) dialogueRunner.onDialogueEnd = new UnityEvent();
        dialogueRunner.onDialogueEnd.AddListener(FinishConversation);
        dialogueRunner.StartDialogue(node);
    }

    private void FinishConversation()
    {
        Action onEnded = conversationEnded;
        DetachFromDialogue();
        onEnded?.Invoke();
    }

    private void DetachFromDialogue()
    {
        if (dialogueRunner != null) dialogueRunner.onDialogueEnd.RemoveListener(FinishConversation);
        conversationEnded = null;
        if (focusedCamera != null) focusedCamera.ResetZoom();
        focusedCamera = null;
    }

    public bool TryAcceptSheet(string text)
    {
        if (completed || typewriter == null || typewriter.ActiveGuest != this ||
            dialogueRunner == null || dialogueRunner.IsDialogueActive || string.IsNullOrWhiteSpace(text)) return false;

        bool correct = string.Equals(Normalize(text), Normalize(requestedText), StringComparison.OrdinalIgnoreCase);
        completed = true;
        typewriter.ReleaseOrder(this);

        DialogueObject response = correct ? correctSheetDialogue : incorrectSheetDialogue;
        if (response != null)
            StartConversation(response, () => BeginDeparture(correct));
        else
            BeginDeparture(correct);
        return true;
    }

    private void BeginDeparture(bool unlockReward)
    {
        interaction.enabled = false;
        StartCoroutine(Depart(unlockReward));
    }

    private IEnumerator Depart(bool unlockReward)
    {
        Vector3 origin = transform.position;
        var portrait = GetComponent<SpriteRenderer>();
        Color portraitColor = portrait.color;
        bool revealReward = unlockReward && rewardItem != null && !rewardItem.activeSelf;
        Transform reward = revealReward ? rewardItem.transform : null;
        Vector3 rewardPosition = revealReward ? reward.localPosition : Vector3.zero;
        Vector3 rewardScale = revealReward ? reward.localScale : Vector3.one;
        float duration = Mathf.Max(0.1f, departureDuration);

        if (revealReward)
        {
            reward.localPosition = rewardPosition + Vector3.down * 0.25f;
            reward.localScale = rewardScale * 0.01f;
            rewardItem.SetActive(true);
            typewriter.ShowOrderMessage($"Открыт предмет «{rewardItem.name}».");
        }

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float progress = elapsed / duration;
            float bob = Mathf.Sin(progress * Mathf.PI * 9f) * 0.13f * (1f - progress);
            transform.position = origin + (Vector3)(departureOffset * progress) + Vector3.up * bob;
            Color color = portraitColor;
            color.a *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 1f, progress));
            portrait.color = color;

            if (reward != null)
            {
                float appear = Mathf.Clamp01(progress * 1.6f);
                float ease = 1f - Mathf.Pow(1f - appear, 3f);
                reward.localPosition = Vector3.Lerp(rewardPosition + Vector3.down * 0.25f, rewardPosition, ease);
                reward.localScale = rewardScale * (ease + Mathf.Sin(appear * Mathf.PI) * 0.16f);
            }
            yield return null;
        }

        if (reward != null)
        {
            reward.localPosition = rewardPosition;
            reward.localScale = rewardScale;
        }
        Destroy(gameObject);
    }

    private static string Normalize(string text) => Regex.Replace(text.Trim(), @"\s+", " ");
}
