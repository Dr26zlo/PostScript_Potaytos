using UnityEngine;
using UnityEngine.UI;

public static class RoomReadableUi
{
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
        panel.anchoredPosition = new Vector2(0, 32);
        panel.sizeDelta = new Vector2(1560, 300);
        panel.localScale = Vector3.one;
        if (runner.nameText != null)
        {
            Layout(runner.nameText.rectTransform, new Vector2(.075f, .755f), new Vector2(.285f, .925f));
            runner.nameText.fontSize = 26;
            runner.nameText.resizeTextForBestFit = true;
            runner.nameText.resizeTextMinSize = 20; runner.nameText.resizeTextMaxSize = 26;
            runner.nameText.alignment = TextAnchor.MiddleLeft;
            runner.nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            runner.nameText.color = new Color(.12f, .09f, .075f);
        }
        if (runner.dialogueText != null)
        {
            Layout(runner.dialogueText.rectTransform, new Vector2(.055f, .15f), new Vector2(.6f, .66f));
            runner.dialogueText.fontSize = 28;
            runner.dialogueText.resizeTextForBestFit = true;
            runner.dialogueText.resizeTextMinSize = 22; runner.dialogueText.resizeTextMaxSize = 28;
            runner.dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            runner.dialogueText.verticalOverflow = VerticalWrapMode.Truncate;
            runner.dialogueText.alignment = TextAnchor.UpperLeft;
            runner.dialogueText.lineSpacing = 1.05f;
            runner.dialogueText.color = new Color(.105f, .075f, .06f);
        }
        if (runner.choicesContainer != null)
        {
            Layout((RectTransform)runner.choicesContainer, new Vector2(.64f, .15f), new Vector2(.945f, .67f));
            var group = runner.choicesContainer.GetComponent<VerticalLayoutGroup>();
            if (group == null) group = runner.choicesContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = 10; group.childControlHeight = true; group.childControlWidth = true;
            group.childForceExpandHeight = false; group.childForceExpandWidth = true;
        }
    }
    public static void FitContent(DialogueRunner runner)
    {
        RectTransform panel = runner.GetComponent<RectTransform>();
        RectTransform canvas = runner.GetComponentInParent<Canvas>().GetComponent<RectTransform>();
        Canvas.ForceUpdateCanvases();
        float width = Mathf.Min(1560, canvas.rect.width - 64);
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        bool choices = runner.choicesContainer != null && runner.choicesContainer.gameObject.activeSelf;
        if (runner.dialogueText != null)
        {
            Layout(runner.dialogueText.rectTransform, new Vector2(.055f, .15f), new Vector2(choices ? .60f : .945f, .66f));
            runner.dialogueText.resizeTextForBestFit = false;
        }
        Canvas.ForceUpdateCanvases();
        float needed = runner.dialogueText != null ? (runner.dialogueText.preferredHeight + 24) / .51f : 300;
        if (choices)
        {
            float height = 0;
            foreach (Transform child in runner.choicesContainer)
            {
                var element = child.GetComponent<LayoutElement>();
                if (element != null)
                {
                    Text label = child.GetComponentInChildren<Text>();
                    if (label != null) element.preferredHeight = Mathf.Max(64, label.preferredHeight + 24);
                    height += element.preferredHeight + 10;
                }
            }
            needed = Mathf.Max(needed, height / .52f);
        }
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Clamp(needed, 300, Mathf.Max(300, canvas.rect.height * .48f)));
        if (runner.dialogueText != null) runner.dialogueText.resizeTextForBestFit = true;
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
        layout.minHeight = 64; layout.preferredHeight = 64;
        Text text = button.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.fontSize = 25; text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 21; text.resizeTextMaxSize = 25;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(.105f, .075f, .06f);
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(24, 12); text.rectTransform.offsetMax = new Vector2(-24, -12);
        }
    }
}
