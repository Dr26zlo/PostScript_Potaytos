using System.Collections;
using UnityEngine;

// A call blocks the guest cycle until the receiver is clicked and the dialogue ends.
public sealed class RoomPhoneCall : MonoBehaviour
{
    public bool IsRinging { get; private set; }
    public bool IsTalking { get; private set; }
    private Transform phone;
    private Vector3 rest;
    private Quaternion rotation;
    private AudioSource audioSource;
    private DialogueRunner dialogue;
    private CameraMovement movement;
    private DialogueObject question, reply, thanks;

    private void Awake()
    {
        foreach (Transform item in FindObjectsOfType<Transform>())
            if (item.name == "Телефон") { phone = item; break; }
        dialogue = FindObjectOfType<DialogueRunner>();
        movement = Camera.main != null ? Camera.main.GetComponent<CameraMovement>() : null;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.volume = .55f;
        if (phone != null) { rest = phone.localPosition; rotation = phone.localRotation; }
        question = Node("Голос в трубке", "Алло… Это лавка писем и воспоминаний?");
        reply = Node("Голос в трубке", "Я правильно набрал номер. Мне нужно кое-что вспомнить… Я зайду к вам позже.");
        thanks = Node("Вы", "Спасибо за звонок. Будем вас ждать.");
        question.choices.Add(new DialogueChoice { choiceText = "Да, вы позвонили в лавку писем и воспоминаний. Слушаю вас.", nextNode = reply });
        reply.nextNode = thanks;
        dialogue.NodeShown += Speak;
    }
    public void SetConversation(string[] lines)
    {
        question.choices.Clear(); question.nextNode = null;
        question.dialogueText = lines != null && lines.Length > 0 ? lines[0] : "Алло…";
        var tail = question;
        if (lines != null) for (int i = 1; i < lines.Length; i++) { var n = Node("Голос в трубке", lines[i]); tail.nextNode = n; tail = n; }
        thanks.dialogueText = "Спасибо за звонок. До свидания.";
        tail.choices.Add(new DialogueChoice { choiceText = "Спасибо. До свидания.", nextNode = thanks });
    }
    private static DialogueObject Node(string who, string text)
    {
        var node = ScriptableObject.CreateInstance<DialogueObject>();
        node.speakerName = who; node.dialogueText = text;
        return node;
    }
    public IEnumerator RingAndWait()
    {
        if (phone == null || dialogue == null) yield break;
        IsRinging = true;
        audioSource.clip = Resources.Load<AudioClip>("PhoneAudio/Ring");
        audioSource.loop = true; audioSource.Play();
        yield return new WaitUntil(() => !IsRinging && !IsTalking);
    }
    private void Update()
    {
        if (!IsRinging || phone == null) return;
        float phase = Time.unscaledTime % 2;
        float shake = phase < .55f || (phase > .75f && phase < 1.3f) ? 1 : 0;
        phone.localPosition = rest + Vector3.right * (Mathf.Sin(Time.unscaledTime * 85) * .025f * shake);
        phone.localRotation = rotation * Quaternion.Euler(0, 0, Mathf.Sin(Time.unscaledTime * 70) * .7f * shake);
        if (!Input.GetMouseButtonDown(0) || dialogue.IsDialogueActive || (movement != null && movement.HasFocus)) return;
        if (RoomStationsController.Instance != null && RoomStationsController.Instance.IsOpen) return;
        Camera camera = Camera.main;
        Ray ray = camera.ViewportPointToRay(new Vector3(Input.mousePosition.x / Screen.width, Input.mousePosition.y / Screen.height));
        var plane = new Plane(Vector3.forward, phone.position);
        if (!plane.Raycast(ray, out float distance)) return;
        Vector3 at = ray.GetPoint(distance);
        foreach (var sprite in phone.GetComponentsInChildren<SpriteRenderer>())
        {
            Bounds b = sprite.bounds;
            if (!SpriteHit.Contains(sprite,at)) continue;
            Answer(); break;
        }
    }
    public void Answer()
    {
        if (!IsRinging || dialogue.IsDialogueActive) return;
        IsRinging = false; IsTalking = true;
        phone.localPosition = rest; phone.localRotation = rotation;
        audioSource.Stop(); audioSource.loop = false;
        dialogue.onDialogueEnd.AddListener(Finish);
        if (movement != null) movement.FocusOn(phone);
        dialogue.StartDialogue(question);
    }
    private void Speak(DialogueObject node)
    {
        if (!IsTalking) return;
        audioSource.Stop();
        string clip = node.speakerName == "Голос в трубке" ? (node == question ? "VoiceQuestion" : "VoiceReply") : null;
        if (clip != null) audioSource.PlayOneShot(Resources.Load<AudioClip>("PhoneAudio/" + clip));
    }
    private void Finish()
    {
        audioSource.Stop(); IsTalking = false;
        dialogue.onDialogueEnd.RemoveListener(Finish);
        if (movement != null && movement.CurrentFocus == phone) movement.ResetZoom();
    }
    private void OnDestroy()
    {
        if (dialogue != null) { dialogue.NodeShown -= Speak; dialogue.onDialogueEnd.RemoveListener(Finish); }
        if (phone != null) { phone.localPosition = rest; phone.localRotation = rotation; }
        Destroy(question); Destroy(reply); Destroy(thanks);
    }
}
