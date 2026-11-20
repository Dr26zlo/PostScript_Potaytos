using UnityEngine;
using UnityEngine.UI;

public static class RoomReadableUi
{
    public static readonly Color Ink = new Color(.16f, .115f, .085f);
    public static float Scale => Mathf.Clamp(Screen.height / 900f, .9f, 1.7f);

    public static GUIStyle PaperLabel(int size, FontStyle weight = FontStyle.Normal)
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = weight };
        foreach (GUIStyleState state in new[] { style.normal, style.hover, style.active, style.focused,
            style.onNormal, style.onHover, style.onActive, style.onFocused })
            state.textColor = Ink;
        return style;
    }

    public static void DrawPaper(Sprite sprite, Rect rect)
    {
        if (sprite == null) return;
        Rect source = sprite.rect;
        Texture2D texture = sprite.texture;
        GUI.DrawTextureWithTexCoords(rect, texture, new Rect(source.x / texture.width, source.y / texture.height,
            source.width / texture.width, source.height / texture.height));
    }

    public static void Configure(DialogueRunner runner)
    {
        Canvas canvas = runner.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .8f;
        RectTransform panel = runner.GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = new Vector2(.5f, 0);
        panel.pivot = new Vector2(.5f, 0);
        panel.anchoredPosition = new Vector2(0, 24);
        panel.sizeDelta = new Vector2(1120, 180);
        panel.localScale = Vector3.one;
        if (runner.nameText != null)
        {
            Layout(runner.nameText.rectTransform, new Vector2(.075f, .755f), new Vector2(.33f, .925f));
            runner.nameText.fontSize = 22;
            runner.nameText.resizeTextForBestFit = true;
            runner.nameText.resizeTextMinSize = 18; runner.nameText.resizeTextMaxSize = 22;
            runner.nameText.alignment = TextAnchor.MiddleLeft;
            runner.nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            runner.nameText.color = Ink;
            runner.nameText.raycastTarget = false;
        }
        if (runner.dialogueText != null)
        {
            Layout(runner.dialogueText.rectTransform, new Vector2(.055f, .19f), new Vector2(.945f, .66f));
            runner.dialogueText.fontSize = 24;
            runner.dialogueText.resizeTextForBestFit = true;
            runner.dialogueText.resizeTextMinSize = 21; runner.dialogueText.resizeTextMaxSize = 24;
            runner.dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            runner.dialogueText.verticalOverflow = VerticalWrapMode.Truncate;
            runner.dialogueText.alignment = TextAnchor.UpperLeft;
            runner.dialogueText.lineSpacing = 1.08f;
            runner.dialogueText.color = Ink;
            runner.dialogueText.raycastTarget = false;
        }
        if (runner.choicesContainer != null)
        {
            var rect = (RectTransform)runner.choicesContainer;
            rect.anchorMin = new Vector2(.04f, 1);
            rect.anchorMax = new Vector2(.96f, 1);
            rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(0, 12);
            rect.sizeDelta = new Vector2(0, 0);
            rect.localScale = Vector3.one;
            var group = runner.choicesContainer.GetComponent<VerticalLayoutGroup>();
            if (group == null) group = runner.choicesContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            group.padding = new RectOffset();
            group.childAlignment = TextAnchor.LowerLeft;
            group.spacing = 8; group.childControlHeight = true; group.childControlWidth = true;
            group.childForceExpandHeight = false; group.childForceExpandWidth = true;
        }
    }

    public static void FitContent(DialogueRunner runner)
    {
        RectTransform panel = runner.GetComponent<RectTransform>();
        RectTransform canvas = runner.GetComponentInParent<Canvas>().GetComponent<RectTransform>();
        Canvas.ForceUpdateCanvases();
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(1120, canvas.rect.width - 64));
        if (runner.dialogueText != null) runner.dialogueText.resizeTextForBestFit = false;
        Canvas.ForceUpdateCanvases();
        float needed = runner.dialogueText != null ? (runner.dialogueText.preferredHeight + 12) / .47f : 180;
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Clamp(needed, 180, canvas.rect.height * .3f));
        if (runner.dialogueText != null) runner.dialogueText.resizeTextForBestFit = true;
        if (runner.choicesContainer == null) return;
        float height = 0;
        foreach (Transform child in runner.choicesContainer)
        {
            if (!child.gameObject.activeSelf) continue;
            var element = child.GetComponent<LayoutElement>();
            Text label = child.GetComponentInChildren<Text>();
            if (element == null) continue;
            element.preferredHeight = Mathf.Max(54, label != null ? label.preferredHeight + 24 : 54);
            height += element.preferredHeight + 8;
        }
        ((RectTransform)runner.choicesContainer).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0, height - 8));
    }

    private static void Layout(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    public static void ConfigureChoice(GameObject button)
    {
        var layout = button.GetComponent<LayoutElement>();
        if (layout == null) layout = button.AddComponent<LayoutElement>();
        layout.minHeight = 54; layout.preferredHeight = 54;
        Image paper = button.GetComponent<Image>();
        Button control = button.GetComponent<Button>();
        if (control != null && paper != null)
        {
            paper.enabled = true;
            paper.color = Color.white;
            paper.raycastTarget = true;
            control.targetGraphic = paper;
            control.transition = Selectable.Transition.ColorTint;
            var colors = control.colors;
            colors.normalColor = new Color(1f, .97f, .9f);
            colors.highlightedColor = new Color(.9f, .8f, .62f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.79f, .68f, .51f);
            colors.colorMultiplier = 1;
            control.colors = colors;
        }
        Text text = button.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.fontSize = 23; text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 21; text.resizeTextMaxSize = 23;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Ink;
            var shadow = text.GetComponent<Shadow>();
            if (shadow != null) shadow.enabled = false;
            text.raycastTarget = false;
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(38, 10); text.rectTransform.offsetMax = new Vector2(-38, -10);
        }
    }
}
