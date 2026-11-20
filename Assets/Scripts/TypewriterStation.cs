using System;
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
    private const int Columns = 28;
    private const int Lines = 8;
    private const float LineFeed = 0.29f;
    private const float InkScale = 0.01f;
    private static readonly string[] KeyRows = { "ЙЦУКЕНГШЩЗХ", "ФЫВАПРОЛДЖЭЁ", "ЯЧСМИТЬБЮ.," };
    private static readonly Regex KeyName = new Regex(@"^Клавиша — ряд ([1-3]) — (\d+)$");

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
            if (!zoomArea.IsFocused && type == EventType.MouseDown && PointerIsOnMachine(pointer))
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

        if (type != EventType.MouseDown || Toolbar.Contains(input.mousePosition)) return;
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
        if (contents.Length > 0) finishedSheets.Enqueue(contents);
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

    private static float UiScale => Mathf.Clamp(Screen.height / 720f, 0.85f, 2f);
    private static Rect Toolbar => new Rect(16f, Screen.height - 108f * UiScale, Mathf.Min(760f * UiScale, Screen.width - 32f), 92f * UiScale);
    private void OnGUI()
    {
        Event input = Event.current;
        HandleMouse(input);
        if (!zoomArea.CanType) return;
        char symbol = input.character;
        if (input.type == EventType.KeyDown && !input.control && !input.alt && !input.command &&
            (char.IsLetterOrDigit(symbol) || symbol == ' ' || char.IsPunctuation(symbol)))
        {
            TryPrint(char.ToUpperInvariant(symbol));
            input.Use();
        }
        float scale = UiScale;
        var button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(20 * scale) };
        var label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(19 * scale), wordWrap = true };
        if (GUI.Button(RoomStationsController.BackButton, "Назад · Esc", button)) zoomArea.ExitTypewriterMode();
        Rect area = Toolbar;
        GUI.Box(area, GUIContent.none);
        if (GUI.Button(new Rect(area.x + 12 * scale, area.y + 10 * scale, 180 * scale, 38 * scale), hasSheet ? "Снять лист" : "Вставить лист", button))
        {
            if (hasSheet) TryRemoveSheet(); else TryInsertSheet();
        }
        GUI.Label(new Rect(area.x + 210 * scale, area.y + 13 * scale, area.width - 220 * scale, 32 * scale),
            $"Листов к передаче: {finishedSheets.Count}", label);
        string hint = !hasSheet ? "Вставьте чистый лист для печати." :
            line + 1 == Lines ? "Последняя строка. Снимите готовый лист." :
            column == Columns ? "Строка заполнена. Перетащите каретку вправо." : "Новая строка — перетащите каретку или рычаг вправо.";
        GUI.Label(new Rect(area.x + 12 * scale, area.y + 53 * scale, area.width - 24 * scale, 36 * scale), hint, label);
    }
}
