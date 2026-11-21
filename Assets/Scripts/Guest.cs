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
    private bool arriving;
    private GuestDoor guestDoor;
    private bool doorOpen;

    public System.Action DemoInteraction;
    public System.Func<string, bool> DemoSubmission;
    public void SetExpectedText(string value) { requestedText = value; }
    public void CompleteDemo(DialogueObject response, bool success) { StartConversation(response, () => BeginDeparture(success)); }

    internal bool HasOrder => dialogue != null && !string.IsNullOrWhiteSpace(requestedText);

    public string RequestedText => requestedText;

    private void Awake()
    {
        interaction = GetComponent<InteractiveObject>();
        if (interaction.onClick == null) interaction.onClick = new UnityEvent();
    }

    internal void LockReward()
    {
        if (rewardItem != null && transform.IsChildOf(rewardItem.transform))
        {
            Debug.LogWarning("Выберите отдельный объект награды из группы «Предметы».", this);
            rewardItem = null;
        }
        if (rewardItem != null) rewardItem.SetActive(false);
    }

    private void Start()
    {
        if (arriving) StartCoroutine(Arrive());
        else LockReward();
    }

    internal void PrepareArrival(GameObject sceneReward, GuestDoor doorway)
    {
        guestDoor = doorway;
        if (sceneReward != null) rewardItem = sceneReward;
        arriving = true;
        GetComponent<InteractiveObject>().enabled = false;
        var portrait = GetComponent<SpriteRenderer>();
        Color color = portrait.color;
        color.a = 0f;
        portrait.color = color;
        portrait.enabled = false;
    }

    private Vector3 DoorwayPosition(SpriteRenderer portrait, Vector3 standingPosition)
    {
        if (guestDoor == null) return standingPosition + (Vector3)departureOffset;
        Vector3 threshold = guestDoor.Threshold;
        float feetOffset = portrait.bounds.min.y - standingPosition.y;
        return new Vector3(threshold.x, threshold.y - feetOffset * guestDoor.GuestScale, standingPosition.z);
    }

    private void SetDoorOpen(bool open)
    {
        if (guestDoor == null || doorOpen == open) return;
        doorOpen = open;
        guestDoor.SetOpen(open);
    }

    private IEnumerator Arrive()
    {
        Vector3 destination = transform.position;
        var portrait = GetComponent<SpriteRenderer>();
        Vector3 entrance = DoorwayPosition(portrait, destination);
        Vector3 standingScale = transform.localScale;
        Vector3 entranceScale = guestDoor != null ? standingScale * guestDoor.GuestScale : standingScale;
        if (guestDoor != null) yield return guestDoor.WaitForPlayer();
        portrait.enabled = true;
        SetDoorOpen(true);
        Color portraitColor = interaction.normalColor;
        float duration = Mathf.Max(0.1f, departureDuration);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float progress = elapsed / duration;
            float walk = Mathf.SmoothStep(0f, 1f, progress);
            float bob = Mathf.Sin(progress * Mathf.PI * 9f) * 0.13f * (1f - progress);
            transform.position = Vector3.Lerp(entrance, destination, walk) + Vector3.up * bob;
            transform.localScale = Vector3.Lerp(entranceScale, standingScale, walk);
            Color color = portraitColor;
            color.a *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 2f));
            portrait.color = color;
            yield return null;
        }

        transform.position = destination;
        transform.localScale = standingScale;
        portrait.color = portraitColor;
        SetDoorOpen(false);
        arriving = false;
        interaction.enabled = true;
    }

    private void OnEnable() => interaction.onClick.AddListener(Interact);

    private void OnDisable()
    {
        SetDoorOpen(false);
        interaction.onClick.RemoveListener(Interact);
        if (conversationEnded != null) DetachFromDialogue();
        if (typewriter != null) typewriter.ReleaseOrder(this);
    }

    public void Interact()
    {
        if (arriving || completed || conversationEnded != null) return;
        if (dialogueRunner == null) dialogueRunner = FindObjectOfType<DialogueRunner>();
        if (typewriter == null) typewriter = FindObjectOfType<TypewriterStation>();
        if (DemoInteraction != null) { DemoInteraction(); return; }
        if (!HasOrder) return;
        if (RoomStationsController.Instance != null && RoomStationsController.Instance.IsOpen) return;
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

        if (DemoSubmission != null) return DemoSubmission(text);
        bool correct = MatchesOrder(text);
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
        Vector3 exit = DoorwayPosition(portrait, origin);
        Vector3 standingScale = transform.localScale;
        Vector3 exitScale = guestDoor != null ? standingScale * guestDoor.GuestScale : standingScale;
        SetDoorOpen(true);
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
            float walk = Mathf.SmoothStep(0f, 1f, progress);
            transform.position = Vector3.Lerp(origin, exit, walk) + Vector3.up * bob;
            transform.localScale = Vector3.Lerp(standingScale, exitScale, walk);
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
        SetDoorOpen(false);
        Destroy(gameObject);
    }

    public bool MatchesOrder(string text) => string.Equals(Normalize(text), Normalize(requestedText), StringComparison.OrdinalIgnoreCase);
    private static string Normalize(string text) => Regex.Replace(Regex.Replace(text.Trim().Replace('ё', 'е').Replace('Ё', 'Е'), @"[^\p{L}\s]", ""), @"\s+", " ");
}
