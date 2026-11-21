using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Adapts the board and four-card archive from the small Postcript build to this room camera.
public sealed class RoomStationsController : MonoBehaviour
{
    public static RoomStationsController Instance { get; private set; }
    public bool IsOpen => panel != null;
    private Camera cam;
    private DialogueRunner dialogue;
    private CameraMovement movement;
    private Transform room, focus;
    private GameObject panel, shade;
    private readonly List<Transform> cards = new List<Transform>();
    private readonly List<Vector3> rests = new List<Vector3>();
    private AudioSource audioSource;
    private AudioClip paperSound;
    private int selected = -1, kind;
    private float openedAt;
    private float Scale => RoomReadableUi.Scale;
    public static Rect BackButton => new Rect(16, 16, 150 * RoomReadableUi.Scale, 42 * RoomReadableUi.Scale);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= Install;
        SceneManager.sceneLoaded += Install;
    }
    private static void Install(Scene scene, LoadSceneMode mode)
    {
        Camera camera = Camera.main;
        if (camera == null || camera.gameObject.scene != scene || camera.GetComponent<CameraMovement>() == null) return;
        if (camera.GetComponent<RoomStationsController>() == null) camera.gameObject.AddComponent<RoomStationsController>();
    }
    private void Start()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        dialogue = FindObjectOfType<DialogueRunner>();
        movement = GetComponent<CameraMovement>();
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            if (root.transform.Find("Освещение") != null) { room = root.transform; break; }
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        paperSound = Resources.Load<AudioClip>("RoomStations/Paper");
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    public Vector3 PointerWorld()
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(Input.mousePosition.x / Screen.width, Input.mousePosition.y / Screen.height));
        var plane = new Plane(Vector3.forward, Vector3.zero);
        return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : Vector3.zero;
    }
    private void Update()
    {
        if (DemoDirector.Instance != null && DemoDirector.Instance.Mode != "room") return;
        if (room == null) return;
        Vector3 world = PointerWorld();
        if (IsOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) { Close(); return; }
            if (kind == 2) HoverCards(world);
            if (Time.unscaledTime - openedAt > 0.3f && Input.GetMouseButtonDown(0) && !BoundsOf(panel).Contains(world)) Close();
            return;
        }
        if (movement.HasFocus || (dialogue != null && dialogue.IsDialogueActive) || !Input.GetMouseButtonDown(0)) return;
        Vector3 at = room.InverseTransformPoint(world);
        if (at.x > 13.6f && at.x < 20f && at.y > -4f && at.y < 6.5f) Open(1);
        else if (at.x > 6f && at.x < 16.5f && at.y < -2.8f && at.y > -11f) Open(2);
    }
    public void Open(int station)
    {
        if (station == 1 && DemoDirector.Instance != null) { DemoDirector.Instance.OpenJournal(); return; }
        if (IsOpen || movement.HasFocus || (dialogue != null && dialogue.IsDialogueActive) || station < 1 || station > 2) return;
        GameObject prefab = Resources.Load<GameObject>(station == 1 ? "RoomStations/Board" : "RoomStations/Archive");
        if (prefab == null) return;
        kind = station;
        panel = Instantiate(prefab);
        panel.name = station == 1 ? "Доска — крупный план" : "Архив — крупный план";
        foreach (SpriteRenderer sprite in panel.GetComponentsInChildren<SpriteRenderer>())
        {
            sprite.sortingLayerID = SortingLayer.NameToID("FG");
            sprite.sortingOrder += 100;
        }
        if (station == 2) BuildCards();
        Bounds bounds = BoundsOf(panel);
        float extra = station == 2 ? 1.8f : 0.25f;
        float size = Mathf.Max(bounds.extents.y + extra, (bounds.extents.x + .35f) / cam.aspect) * 1.14f;
        // Fit the imported prefab instead of zooming beyond the room background.
        float maximumSize = Mathf.Max(movement.zoomedInSize, movement.defaultZoom * .9f);
        float fit = Mathf.Min(1f, maximumSize / size);
        panel.transform.localScale *= fit;
        size *= fit;
        bounds = BoundsOf(panel);
        Vector3 centre = cam.transform.position; centre.z = 0;
        panel.transform.position += centre - bounds.center;
        bounds = BoundsOf(panel);
        focus = new GameObject("Фокус крупного плана").transform;
        focus.position = bounds.center;
        movement.FocusOn(focus, size);
        MakeShade(bounds.center, size);
        openedAt = Time.unscaledTime;
    }
    private void MakeShade(Vector3 centre, float size)
    {
        shade = new GameObject("Затемнение за крупным планом");
        var texture = new Texture2D(1, 1); texture.SetPixel(0, 0, Color.white); texture.Apply();
        var renderer = shade.AddComponent<SpriteRenderer>();
        renderer.sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
        renderer.sortingLayerID = SortingLayer.NameToID("FG");
        renderer.sortingOrder = 90; renderer.color = new Color(.012f, .012f, .025f, .83f);
        shade.transform.position = centre;
        shade.transform.localScale = new Vector3(size * cam.aspect * 4, size * 4, 1);
    }
    private Transform Find(string name)
    {
        foreach (Transform child in panel.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
        return null;
    }
    private void BuildCards()
    {
        Transform sample = Find("Архив — вытягиваемая карточка");
        if (sample == null) return;
        sample.gameObject.SetActive(false);
        foreach (SpriteRenderer sprite in panel.GetComponentsInChildren<SpriteRenderer>())
            if (sprite.name.Contains("разделитель") || sprite.name == "Архив — задняя карточка") sprite.gameObject.SetActive(false);
        for (int i = 0; i < 4; i++)
        {
            Transform card = Instantiate(sample.gameObject, sample.parent).transform;
            card.name = "Карточка " + (i + 1); card.gameObject.SetActive(true);
            card.localPosition = sample.localPosition + new Vector3((i % 3 - 1) * .055f, i * .38f, 0);
            card.GetComponent<SpriteRenderer>().sortingOrder = 105 - i;
            cards.Add(card); rests.Add(card.localPosition);
        }
        Transform front = Find("Архив — передняя стенка");
        if (front != null) front.GetComponent<SpriteRenderer>().sortingOrder = 150;
    }
    private void HoverCards(Vector3 world)
    {
        int candidate = -1;
        Vector3 at = panel.transform.InverseTransformPoint(world);
        // Use the resting tabs, so the raised card does not flicker between hover states.
        for (int i = cards.Count - 1; i >= 0; i--)
        {
            Bounds b = cards[i].GetComponent<SpriteRenderer>().sprite.bounds;
            float edge = rests[i].y + b.max.y;
            if (Mathf.Abs(at.x - rests[i].x) < b.extents.x && at.y >= edge - .38f && at.y <= edge + .02f) { candidate = i; break; }
        }
        if (candidate < 0 && selected >= 0 && cards[selected].GetComponent<SpriteRenderer>().bounds.Contains(world)) candidate = selected;
        if (candidate != selected && candidate >= 0 && paperSound != null) audioSource.PlayOneShot(paperSound, .5f);
        selected = candidate;
        for (int i = 0; i < cards.Count; i++)
            cards[i].localPosition = Vector3.Lerp(cards[i].localPosition, rests[i] + Vector3.up * (i == selected ? 1.8f : 0), Time.deltaTime * 10);
    }
    private static Bounds BoundsOf(GameObject obj)
    {
        bool found = false; Bounds result = default;
        foreach (SpriteRenderer sprite in obj.GetComponentsInChildren<SpriteRenderer>())
            if (sprite.enabled) { if (!found) { result = sprite.bounds; found = true; } else result.Encapsulate(sprite.bounds); }
        return result;
    }
    public void Close()
    {
        if (!IsOpen) return;
        Destroy(panel); panel = null;
        if (shade != null) { Sprite sprite = shade.GetComponent<SpriteRenderer>().sprite; Destroy(sprite.texture); Destroy(sprite); Destroy(shade); }
        if (movement.CurrentFocus == focus) movement.ResetZoom();
        if (focus != null) Destroy(focus.gameObject);
        cards.Clear(); rests.Clear(); selected = -1;
    }
    private void OnGUI()
    {
        if (!IsOpen) return;
        Sprite paper = dialogue != null ? dialogue.choiceButtonPrefab.GetComponent<Image>().sprite : null;
        RoomReadableUi.DrawPaper(paper, BackButton);
        var button = RoomReadableUi.PaperLabel(Mathf.RoundToInt(17 * Scale));
        button.alignment = TextAnchor.MiddleCenter;
        if (GUI.Button(BackButton, "Назад · Esc", button)) Close();
        var label = RoomReadableUi.PaperLabel(Mathf.RoundToInt(16 * Scale));
        label.alignment = TextAnchor.MiddleCenter;
        string title = kind == 1 ? "Доска заметок" : "Архив · наведите на верхний край карточки";
        Rect caption = new Rect(16, Screen.height - 56 * Scale, Mathf.Min(Screen.width - 32, 580 * Scale), 42 * Scale);
        RoomReadableUi.DrawPaper(paper, caption);
        GUI.Label(caption, title, label);
    }
}
