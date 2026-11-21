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
    private ITypewriterSheetRecipient pendingRecipient;
    private bool confirmedDelivery;

    public Guest ActiveGuest => activeGuest;
    public bool HasLineBreakHint => IsFocused && hasSheet && activeGuest != null && activeGuest.RequiresLineBreak && line == 0 && column >= activeGuest.RequestedText.Split('\n')[0].Trim().Length;
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
        if (DemoDirector.Instance != null && DemoDirector.Instance.Mode != "room") return;
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
            (noteProgress > 0f && activeGuest != null && NoteRect.Contains(input.mousePosition))) return;
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
        return SpriteHit.Contains(sprite,pointer);
    }

    internal bool TryPrint(char symbol)
    {
        if ((!char.IsLetter(symbol) && symbol != ' ') || !zoomArea.CanType || !hasSheet || dragging || column >= Columns) return false;

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

        if (!confirmedDelivery && recipient is Guest guest && !guest.MatchesOrder(finishedSheets.Peek()))
        {
            pendingRecipient = recipient;
            return false;
        }
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
        if (tutorialComplete || HasLineBreakHint) return;
        string instruction = !hasSheet ? "Вставьте лист в машинку" : text.Length == 0
            ? "Напечатайте образец: нажимайте буквы мышью или на клавиатуре"
            : line == 0 && activeGuest != null && activeGuest.RequiresLineBreak
                ? "Напечатайте первую строку образца"
                : activeGuest != null && line + 1 < activeGuest.RequestedText.Split('\n').Length
                    ? "Завершите текст по образцу"
                    : activeGuest != null && column < activeGuest.RequestedText.Split('\n')[Mathf.Min(line, activeGuest.RequestedText.Split('\n').Length - 1)].Trim().Length
                        ? "Завершите текст по образцу" : "Проверьте текст и снимите готовый лист";
        var style = new GUIStyle(GUI.skin.label) { font = RoomReadableUi.Font,
            fontSize = Mathf.RoundToInt(24 * UiScale), alignment = TextAnchor.MiddleCenter, wordWrap = true };
        var area = new Rect(Screen.width * .16f, Screen.height * .32f, Screen.width * .68f, 80 * UiScale);
        style.normal.textColor = new Color(0, 0, 0, .75f * RoomReadableUi.HintAlpha);
        GUI.Label(new Rect(area.x + 2, area.y + 2, area.width, area.height), instruction, style);
        style.normal.textColor = new Color(1,1,1,RoomReadableUi.HintAlpha);
        GUI.Label(area, instruction, style);
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

    public bool HasDeliveryWarning => pendingRecipient != null;
    public void ResolveDeliveryWarning(bool deliverAnyway)
    {
        var recipient = pendingRecipient;
        pendingRecipient = null;
        if (recipient == null) return;
        if (!deliverAnyway)
        {
            if (finishedSheets.Count > 0) finishedSheets.Dequeue();
            ShowOrderMessage("Испорченный лист выброшен. Вставьте новый лист в машинку.");
            return;
        }
        confirmedDelivery = true;
        try { TryGiveNextSheet(recipient); }
        finally { confirmedDelivery = false; }
    }
    private bool DrawDeliveryWarning()
    {
        if (pendingRecipient == null) return false;
        float s = UiScale;
        var card = new Rect((Screen.width - 570 * s) / 2, (Screen.height - 210 * s) / 2, 570 * s, 210 * s);
        DrawPaper(tutorialPaper, card);
        var style = RoomReadableUi.PaperLabel(Mathf.RoundToInt(19 * s));
        style.wordWrap = true;
        GUI.Label(new Rect(card.x + 45*s, card.y + 35*s, card.width - 90*s, 85*s),
            "Есть опечатка или неверный перенос строки. Можно заменить лист и перепечатать или всё равно отдать его клиенту.", style);
        if (GUI.Button(new Rect(card.x + 45*s, card.y + 135*s, 230*s, 44*s), "Заменить лист", style)) ResolveDeliveryWarning(false);
        if (GUI.Button(new Rect(card.x + 300*s, card.y + 135*s, 230*s, 44*s), "Всё равно отдать", style)) ResolveDeliveryWarning(true);
        return true;
    }

    private void DrawLineBreakHint()
    {
        if(!HasLineBreakHint)return;
        float s=UiScale;var text=DemoDirector.Instance!=null?DemoDirector.Instance.Content.tutorial.lineBreak:"Потяните вправо, чтобы перейти на новую строку";
        var style=new GUIStyle(GUI.skin.label){font=RoomReadableUi.Font,fontSize=Mathf.RoundToInt(25*s),alignment=TextAnchor.MiddleCenter,wordWrap=true};
        style.normal.textColor=new Color(1,1,1,RoomReadableUi.HintAlpha);
        GUI.Label(new Rect(Screen.width*.16f,Screen.height*.32f,Screen.width*.68f,80*s),text,style);
        Vector3 target=worldCamera.WorldToViewportPoint(lever.bounds.center);Vector2 point=new Vector2(target.x*Screen.width,(1-target.y)*Screen.height);
        Color previous=GUI.color;GUI.color=new Color(1,1,1,RoomReadableUi.HintAlpha);
        GUI.DrawTexture(new Rect(point.x-34*s,point.y-2*s,68*s,4*s),Texture2D.whiteTexture);
        Matrix4x4 matrix=GUI.matrix;Vector2 tip=new Vector2(point.x+34*s,point.y);
        GUIUtility.RotateAroundPivot(40,tip);GUI.DrawTexture(new Rect(tip.x-20*s,tip.y-2*s,20*s,4*s),Texture2D.whiteTexture);GUI.matrix=matrix;
        GUIUtility.RotateAroundPivot(-40,tip);GUI.DrawTexture(new Rect(tip.x-20*s,tip.y-2*s,20*s,4*s),Texture2D.whiteTexture);GUI.matrix=matrix;GUI.color=previous;
    }

    private void OnGUI()
    {
        if (DemoDirector.Instance != null && DemoDirector.Instance.Mode != "room") return;
        if (DrawDeliveryWarning()) return;
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
        if (input.type == EventType.KeyDown && !input.control && !input.alt && !input.command &&
            (char.IsLetter(input.character) || input.character == ' '))
        { TryPrint(char.ToUpperInvariant(input.character)); input.Use(); }
        DrawPaper(orderNote, RoomStationsController.BackButton);
        var backStyle = RoomReadableUi.PaperLabel(Mathf.RoundToInt(17 * UiScale));
        backStyle.alignment = TextAnchor.MiddleCenter;
        if (GUI.Button(RoomStationsController.BackButton, "Назад · Esc", backStyle)) zoomArea.ExitTypewriterMode();
        DrawToolbar();
        DrawLineBreakHint();
    }
}
