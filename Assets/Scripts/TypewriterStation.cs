using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public interface ITypewriterSheetRecipient
{
    bool TryAcceptSheet(string text);
}

[DisallowMultipleComponent]
public sealed class TypewriterStation : MonoBehaviour
{
    private const string TutorialKey = "Typewriter.TutorialV2Completed";
    private const int Columns = 28;
    private const int Lines = 8;
    private const float LineFeed = 0.29f;
    private const float InkScale = 0.01f;
    private static readonly string[] KeyRows = { "ЙЦУКЕНГШЩЗХ", "ФЫВАПРОЛДЖЭЁ", "ЯЧСМИТЬБЮ.," };
    private static readonly Regex KeyName = new Regex(@"^Клавиша — ряд ([1-3]) — (\d+)$");

    [SerializeField] private Sprite orderNote;
    [SerializeField] private Sprite tutorialPaper;
    [SerializeField] private DialogueRunner dialogueRunner;

    private readonly List<TypewriterKey> keys = new List<TypewriterKey>();
    private readonly Queue<string> finishedSheets = new Queue<string>();
    private readonly StringBuilder text = new StringBuilder();
    private readonly List<GameObject> ink = new List<GameObject>();

    private TypewriterZoomArea zoomArea;
    private Camera worldCamera;
    private SpriteRenderer body, paper, roller, lever;
    private Transform carriage;
    private RectTransform inkCanvas;
    private Font inkFont;
    private AudioSource carriageAudio;
    private AudioClip carriageReturnSound;
    private Vector3 carriageRest, paperRest;
    private float leftMargin, strikeHeight, pitch;
    private int column, line;
    private bool hasSheet, dragging, handingOff;
    private float dragPointerStart;

    private Guest activeGuest;
    private float noteProgress;
    private string orderMessage;
    private float messageUntil;
    private bool tutorialComplete;

    public Guest ActiveGuest => activeGuest;
    public bool IsFocused => zoomArea != null && zoomArea.IsFocused;

    public void TakeOrder(Guest guest)
    {
        if (activeGuest != null || guest == null) return;
        activeGuest = guest;
        ShowOrderMessage("Заказ принят. Подойдите к печатной машинке.");
    }

    public void ReleaseOrder(Guest guest)
    {
        if (activeGuest == guest) activeGuest = null;
    }

    public void ShowOrderMessage(string message)
    {
        orderMessage = message;
        messageUntil = Time.unscaledTime + 5f;
    }

    public int FinishedSheetCount => finishedSheets.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= InstallInScene;
        SceneManager.sceneLoaded += InstallInScene;
    }

    private static void InstallInScene(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform item in root.GetComponentsInChildren<Transform>())
            {
                if (item.name == "Печатная машинка" && item.GetComponent<TypewriterStation>() == null)
                    item.gameObject.AddComponent<TypewriterStation>();
            }
        }
    }

    private void Awake()
    {
        tutorialComplete = PlayerPrefs.GetInt(TutorialKey) != 0;
        if (dialogueRunner == null) dialogueRunner = FindObjectOfType<DialogueRunner>();
        carriage = transform.Find("Каретка");
        body = FindSprite(transform, "Машинка — корпус");
        paper = FindSprite(carriage, "Машинка — бумага");
        roller = FindSprite(carriage, "Машинка — каретка");
        lever = FindSprite(carriage, "Машинка — рычаг");
        zoomArea = GetComponentInChildren<TypewriterZoomArea>();
        CameraMovement cameraMovement = zoomArea != null ? zoomArea.cameraScript : null;
        if (cameraMovement == null && Camera.main != null)
            cameraMovement = Camera.main.GetComponent<CameraMovement>();

        if (body == null || paper == null || roller == null || lever == null || cameraMovement == null)
        {
            Debug.LogWarning("Печатная машинка: не найдены спрайты или камера.", this);
            enabled = false;
            return;
        }

        bool needsZoomArea = zoomArea == null;
        if (needsZoomArea)
        {
            EnsureCollider(body.gameObject);
            zoomArea = body.gameObject.AddComponent<TypewriterZoomArea>();
        }
        zoomArea.station = this;
        zoomArea.cameraScript = cameraMovement;
        if (needsZoomArea || zoomArea.focusPoint == null)
        {
            var focus = new GameObject("Фокус печатной машинки");
            focus.transform.SetParent(transform, false);
            focus.transform.localPosition = new Vector3(-0.15f, 3.35f, 0f);
            zoomArea.focusPoint = focus.transform;
        }
        worldCamera = cameraMovement.GetComponent<Camera>();
        zoomArea.minimumZoomSize = 4.9f * Mathf.Abs(transform.lossyScale.y);

        carriageReturnSound = Resources.Load<AudioClip>("RoomAudio/CarriageReturn");
        if (carriageReturnSound != null)
        {
            carriageAudio = carriage.gameObject.AddComponent<AudioSource>();
            carriageAudio.playOnAwake = false;
        }

        carriageRest = carriage.localPosition;
        paperRest = paper.transform.localPosition;
        Bounds paperBounds = paper.sprite.bounds;
        pitch = paperBounds.size.x * 0.78f / Columns;
        leftMargin = paperBounds.min.x + paperBounds.size.x * 0.11f + pitch * 0.5f;
        float rollerTop = paper.transform.InverseTransformPoint(
            new Vector3(roller.bounds.center.x, roller.bounds.max.y, paper.transform.position.z)).y;
        strikeHeight = rollerTop + 0.17f;
        float paperLift = Mathf.Max(0f, strikeHeight + 0.23f - paperBounds.max.y);
        paperRest.y += paperLift;
        strikeHeight -= paperLift;
        inkFont = Resources.Load<Font>("Typewriter/Ink-Bold");
        if (inkFont == null) inkFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var inkObject = new GameObject("Текст на бумаге", typeof(RectTransform), typeof(Canvas));
        inkCanvas = (RectTransform)inkObject.transform;
        inkCanvas.SetParent(paper.transform, false);
        inkCanvas.sizeDelta = Vector2.zero;
        inkCanvas.localScale = Vector3.one * InkScale;
        Canvas canvas = inkObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingLayerID = paper.sortingLayerID;
        canvas.sortingOrder = paper.sortingOrder + 1;
        paper.enabled = false;
        PlaceCarriage(0f);

        foreach (SpriteRenderer sprite in GetComponentsInChildren<SpriteRenderer>())
        {
            char symbol = GetSymbol(sprite);
            if (symbol == '\0') continue;
            var key = sprite.GetComponent<TypewriterKey>();
            if (key == null)
            {
                EnsureCollider(sprite.gameObject);
                key = sprite.gameObject.AddComponent<TypewriterKey>();
            }
            key.Bind(this, symbol);
            keys.Add(key);
        }
    }

    private static void EnsureCollider(GameObject target)
    {
        if (target.GetComponent<Collider2D>() == null)
            target.AddComponent<BoxCollider2D>();
    }

    private static SpriteRenderer FindSprite(Transform parent, string name)
    {
        Transform child = parent != null ? parent.Find(name) : null;
        return child != null ? child.GetComponent<SpriteRenderer>() : null;
    }

    private static char GetSymbol(SpriteRenderer sprite)
    {
        string name = sprite.sprite != null ? sprite.sprite.name : sprite.name;
        if (name == "Машинка — пробел" || sprite.name == "Машинка — пробел") return ' ';
        Match match = KeyName.Match(name);
        if (!match.Success) match = KeyName.Match(sprite.name);
        if (!match.Success || !int.TryParse(match.Groups[2].Value, out int index)) return '\0';
        string row = KeyRows[match.Groups[1].Value[0] - '1'];
        return index >= 1 && index <= row.Length ? row[index - 1] : '\0';
    }

    private void Update()
    {
        if (!zoomArea.CanType) CancelDrag();
        if (IsFocused && dialogueRunner != null && dialogueRunner.IsDialogueActive)
            zoomArea.ExitTypewriterMode();

        noteProgress = Mathf.MoveTowards(noteProgress, IsFocused && activeGuest != null ? 1f : 0f,
            Time.unscaledDeltaTime * 4f);
    }

    private void HandleMouse(Event input)
    {
        EventType type = input.type;
        if (RoomStationsController.Instance != null && RoomStationsController.Instance.IsOpen) return;
        if (RoomStationsController.BackButton.Contains(input.mousePosition)) return;
        if (input.button != 0 || (type != EventType.MouseDown && type != EventType.MouseDrag && type != EventType.MouseUp))
            return;

        Vector3 pointer = PointerOnMachine(input.mousePosition);
        if (!zoomArea.CanType)
        {
            if (!IsFocused && type == EventType.MouseDown &&
                (dialogueRunner == null || !dialogueRunner.IsDialogueActive) && PointerIsOnMachine(pointer))
                zoomArea.EnterTypewriterMode();
            return;
        }

        float pointerX = transform.InverseTransformPoint(pointer).x;
        if (dragging)
        {
            float travel = column * pitch;
            float returned = Mathf.Clamp(pointerX - dragPointerStart, 0f, travel);
            PlaceCarriage(travel - returned);
            if (returned >= travel - Mathf.Min(pitch * 0.25f, 0.025f))
                FinishReturn();
            else if (type == EventType.MouseUp)
                CancelDrag();
            input.Use();
            return;
        }

        if (type != EventType.MouseDown || Toolbar.Contains(input.mousePosition) ||
            (noteProgress > 0f && activeGuest != null && NoteRect.Contains(input.mousePosition)) ||
            (!tutorialComplete && TutorialRect.Contains(input.mousePosition))) return;
        if (hasSheet && column > 0 && line + 1 < Lines && (Hits(lever, pointer) || Hits(roller, pointer)))
        {
            dragging = true;
            dragPointerStart = pointerX;
            input.Use();
            return;
        }

        FindClickedKey(pointer)?.OnKeyPressed();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) CancelDrag();
    }

    private Vector3 PointerOnMachine(Vector2 guiPosition)
    {
        Vector3 viewport = new Vector3(guiPosition.x / Screen.width, 1f - guiPosition.y / Screen.height);
        Ray ray = worldCamera.ViewportPointToRay(viewport);
        var plane = new Plane(transform.forward, transform.position);
        return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : transform.position;
    }

    private bool PointerIsOnMachine(Vector3 pointer)
    {
        return Hits(body, pointer) || Hits(roller, pointer) || Hits(lever, pointer) ||
            Hits(paper, pointer) || FindClickedKey(pointer) != null;
    }

    private TypewriterKey FindClickedKey(Vector3 pointer)
    {
        TypewriterKey result = null;
        float nearest = float.PositiveInfinity;
        foreach (TypewriterKey key in keys)
        {
            SpriteRenderer sprite = key.KeySprite;
            if (!key.isActiveAndEnabled || !Hits(sprite, pointer)) continue;
            float distance = ((Vector2)(pointer - sprite.bounds.center)).sqrMagnitude;
            if (distance >= nearest) continue;
            nearest = distance;
            result = key;
        }
        return result;
    }

    private static bool Hits(SpriteRenderer sprite, Vector3 pointer)
    {
        if (!sprite.enabled || !sprite.gameObject.activeInHierarchy) return false;
        Bounds bounds = sprite.bounds;
        return pointer.x >= bounds.min.x && pointer.x <= bounds.max.x &&
            pointer.y >= bounds.min.y && pointer.y <= bounds.max.y;
    }

    internal bool TryPrint(char symbol)
    {
        if (!zoomArea.CanType || !hasSheet || dragging || column >= Columns) return false;

        if (symbol != ' ')
        {
            var stamp = new GameObject("Оттиск", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)stamp.transform;
            rect.SetParent(inkCanvas, false);
            rect.anchoredPosition = new Vector2(leftMargin + column * pitch, strikeHeight - line * LineFeed) / InkScale;
            rect.sizeDelta = new Vector2(pitch, LineFeed) / InkScale;
            var glyph = stamp.GetComponent<Text>();
            glyph.font = inkFont;
            glyph.fontSize = 16;
            glyph.alignment = TextAnchor.MiddleCenter;
            glyph.horizontalOverflow = HorizontalWrapMode.Overflow;
            glyph.verticalOverflow = VerticalWrapMode.Overflow;
            glyph.raycastTarget = false;
            glyph.supportRichText = false;
            glyph.color = new Color(0.055f, 0.04f, 0.03f);
            glyph.text = symbol.ToString();
            // Fit the new typeface into the existing typewriter cell without changing carriage spacing.
            float cellWidth = pitch / InkScale;
            rect.localScale = new Vector3(Mathf.Min(1f, cellWidth * 0.94f / Mathf.Max(1f, glyph.preferredWidth)), 1f, 1f);
            ink.Add(stamp);
        }

        text.Append(symbol);
        column++;
        PlaceCarriage(column * pitch);
        return true;
    }

    private void PlaceCarriage(float travel)
    {
        carriage.localPosition = carriageRest + Vector3.right * (Columns * pitch * 0.5f - travel);
    }

    private void CancelDrag()
    {
        if (!dragging) return;
        dragging = false;
        PlaceCarriage(column * pitch);
    }

    private void FinishReturn()
    {
        dragging = false;
        column = 0;
        line++;
        text.Append('\n');
        PlaceCarriage(0f);
        paper.transform.localPosition = paperRest + Vector3.up * (line * LineFeed);
        if (carriageAudio != null) carriageAudio.PlayOneShot(carriageReturnSound, 0.75f);
    }

    public bool TryInsertSheet()
    {
        if (!isActiveAndEnabled || !zoomArea.CanType || hasSheet) return false;
        hasSheet = true;
        paper.transform.localPosition = paperRest;
        paper.enabled = true;
        return true;
    }

    public bool TryRemoveSheet()
    {
        if (!isActiveAndEnabled || !zoomArea.CanType || !hasSheet || dragging) return false;
        string contents = text.ToString().TrimEnd();
        if (contents.Length > 0)
        {
            finishedSheets.Enqueue(contents);
            if (!tutorialComplete)
            {
                tutorialComplete = true;
                PlayerPrefs.SetInt(TutorialKey, 1);
                PlayerPrefs.Save();
            }
        }
        hasSheet = false;
        paper.enabled = false;
        foreach (GameObject stamp in ink)
        {
            stamp.SetActive(false);
            Destroy(stamp);
        }
        ink.Clear();
        text.Clear();
        column = 0;
        line = 0;
        PlaceCarriage(0f);
        return true;
    }

    public bool TryGiveNextSheet(ITypewriterSheetRecipient recipient)
    {
        if (recipient == null || finishedSheets.Count == 0 || handingOff) return false;

        handingOff = true;
        try
        {
            if (!recipient.TryAcceptSheet(finishedSheets.Peek())) return false;
            finishedSheets.Dequeue();
            return true;
        }
        finally
        {
            handingOff = false;
        }
    }

    private static float UiScale => RoomReadableUi.Scale;
    private static Rect Toolbar => new Rect((Screen.width - Mathf.Min(560f * UiScale, Screen.width - 32f)) / 2f,
        Screen.height - 104f * UiScale, Mathf.Min(560f * UiScale, Screen.width - 32f), 86f * UiScale);
    private Rect NoteRect
    {
        get
        {
            float width = Mathf.Min(370f * UiScale, Screen.width * .36f);
            float shownX = Screen.width - width - 12f;
            float slide = Mathf.SmoothStep(0f, 1f, noteProgress);
            return new Rect(Mathf.Lerp(Screen.width, shownX, slide), 18f, width, width * 0.42f);
        }
    }

    private void DrawOrderNote()
    {
        if (noteProgress <= 0f || activeGuest == null) return;
        Rect rect = NoteRect;
        DrawPaper(orderNote, rect);

        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Clamp(Mathf.RoundToInt(rect.width / 22f), 16, Mathf.RoundToInt(24 * UiScale)),
            wordWrap = true
        };
        if (orderNote != null) style.normal.textColor = new Color(0.18f, 0.12f, 0.09f);
        float inset = rect.width * 0.065f;
        GUI.Label(new Rect(rect.x + inset, rect.y, rect.width - 2f * inset, rect.height),
            activeGuest.RequestedText, style);
    }

    private static void DrawPaper(Sprite sprite, Rect rect)
    {
        if (sprite == null)
        {
            GUI.Box(rect, GUIContent.none);
            return;
        }

        Texture2D texture = sprite.texture;
        Rect source = sprite.rect;
        GUI.DrawTextureWithTexCoords(rect, texture, new Rect(source.x / texture.width, source.y / texture.height,
            source.width / texture.width, source.height / texture.height));
    }

    private static readonly Color PaperInk = new Color(0.20f, 0.12f, 0.08f);
    private static readonly Color Burgundy = new Color(0.36f, 0.15f, 0.12f);

    private static Rect TutorialRect
    {
        get
        {
            float width = Mathf.Min(290f * UiScale, Screen.width * .34f);
            return new Rect(16f, RoomStationsController.BackButton.yMax + 14f, width, width * 0.59f);
        }
    }

    private static GUIStyle PaperLabel(Color color, int size, FontStyle fontStyle = FontStyle.Normal)
    {
        return RoomReadableUi.PaperLabel(size, fontStyle);
    }

    private void DrawTutorial()
    {
        if (tutorialComplete) return;

        int step;
        string title;
        string instruction;
        if (!hasSheet)
        {
            step = 1;
            title = "Вставьте бумагу";
            instruction = "Нажмите «Вставить лист» на нижней панели.";
        }
        else if (text.Length == 0)
        {
            step = 2;
            title = "Напечатайте текст";
            instruction = "Печатайте с клавиатуры или нажимайте клавиши машинки мышью.";
        }
        else
        {
            step = 3;
            title = "Подготовьте лист";
            instruction = "Для новой строки потяните рычаг вправо. Готовый лист снимите и отдайте гостю.";
        }

        Rect card = TutorialRect;
        DrawPaper(tutorialPaper, card);
        float inset = card.width * 0.1f;
        float width = card.width - inset * 2f;
        float left = card.x + inset;
        float scale = card.width / 290f;
        Color ink = tutorialPaper != null ? PaperInk : GUI.skin.label.normal.textColor;
        Color accent = tutorialPaper != null ? Burgundy : ink;

        var caption = PaperLabel(accent, Mathf.Max(9, Mathf.RoundToInt(13 * scale)), FontStyle.Bold);
        GUI.Label(new Rect(left, card.y + card.height * 0.13f, width, card.height * 0.16f),
            $"ПЕРВАЯ ПЕЧАТЬ  ·  {step} / 3", caption);
        var heading = PaperLabel(ink, Mathf.Max(11, Mathf.RoundToInt(22 * scale)), FontStyle.Bold);
        GUI.Label(new Rect(left, card.y + card.height * 0.31f, width, card.height * 0.23f), title, heading);
        var body = PaperLabel(ink, Mathf.Max(9, Mathf.RoundToInt(16 * scale)));
        body.wordWrap = true;
        GUI.Label(new Rect(left, card.y + card.height * 0.54f, width, card.height * 0.38f), instruction, body);
    }

    private void DrawToolbar()
    {
        float scale = UiScale;
        Rect area = Toolbar;
        DrawPaper(orderNote, area);
        float inset = 30f * scale;
        float left = area.x + inset;
        var action = new Rect(left, area.y + 17f * scale, 195f * scale, 30f * scale);
        var button = RoomReadableUi.PaperLabel(Mathf.RoundToInt(18 * scale), FontStyle.Bold);
        button.alignment = TextAnchor.MiddleLeft;
        if (GUI.Button(action, hasSheet ? "−  Снять лист" : "+  Вставить лист", button))
        {
            if (hasSheet) TryRemoveSheet(); else TryInsertSheet();
        }
        if (action.Contains(Event.current.mousePosition))
        {
            Color previous = GUI.color;
            GUI.color = RoomReadableUi.Ink;
            GUI.DrawTexture(new Rect(action.x, action.yMax - 2f * scale, action.width - 12f * scale, scale), Texture2D.whiteTexture);
            GUI.color = previous;
        }
        var counter = PaperLabel(PaperInk, Mathf.RoundToInt(16 * scale));
        counter.alignment = TextAnchor.MiddleRight;
        GUI.Label(new Rect(action.xMax + 8f * scale, action.y, area.xMax - inset - action.xMax - 8f * scale, action.height),
            $"Готовых листов: {finishedSheets.Count}", counter);
        string hint = !hasSheet ? "Вставьте лист, чтобы начать печать." :
            line + 1 == Lines ? "Последняя строка · снимите готовый лист." :
            column == Columns ? "Строка заполнена · потяните рычаг вправо." :
            "Рычаг вправо — новая строка · Esc — назад";
        var hintStyle = PaperLabel(PaperInk, Mathf.RoundToInt(14 * scale));
        hintStyle.wordWrap = true;
        GUI.Label(new Rect(left, area.y + 50f * scale, area.width - inset * 2f, 25f * scale), hint, hintStyle);
    }

    private void OnGUI()
    {
        Event input = Event.current;
        HandleMouse(input);
        DrawOrderNote();
        if (!IsFocused && Time.unscaledTime < messageUntil &&
            (dialogueRunner == null || !dialogueRunner.IsDialogueActive))
        {
            float width = Mathf.Min(420f * UiScale, Screen.width - 32f);
            var messageArea = new Rect((Screen.width - width) / 2f, 16f, width, 60f * UiScale);
            DrawPaper(orderNote, messageArea);
            Color ink = orderNote != null ? PaperInk : GUI.skin.label.normal.textColor;
            var message = PaperLabel(ink, Mathf.RoundToInt(15 * UiScale), FontStyle.Bold);
            message.wordWrap = true;
            message.alignment = TextAnchor.MiddleCenter;
            float inset = Mathf.Min(24f, width * 0.09f);
            GUI.Label(new Rect(messageArea.x + inset, messageArea.y + 12f * UiScale, width - inset * 2f, 36f * UiScale),
                orderMessage, message);
        }
        if (!zoomArea.CanType) return;
        DrawTutorial();
        char symbol = input.character;
        if (input.type == EventType.KeyDown && !input.control && !input.alt && !input.command &&
            (char.IsLetterOrDigit(symbol) || symbol == ' ' || char.IsPunctuation(symbol)))
        {
            TryPrint(char.ToUpperInvariant(symbol));
            input.Use();
        }

        DrawPaper(orderNote, RoomStationsController.BackButton);
        var backStyle = RoomReadableUi.PaperLabel(Mathf.RoundToInt(17 * UiScale));
        backStyle.alignment = TextAnchor.MiddleCenter;
        if (GUI.Button(RoomStationsController.BackButton, "Назад · Esc", backStyle)) zoomArea.ExitTypewriterMode();
        DrawToolbar();
    }
}
