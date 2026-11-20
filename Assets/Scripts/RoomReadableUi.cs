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
        scaler.matchWidthOrHeight = 0.8f;
        RectTransform panel = runner.GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = new Vector2(.5f, 0);
        panel.pivot = new Vector2(.5f, 0);
        panel.anchoredPosition = new Vector2(0, 32);
        panel.sizeDelta = new Vector2(1560, 270);
        panel.localScale = Vector3.one;
        if (runner.nameText != null)
        {
            Layout(runner.nameText.rectTransform, new Vector2(.045f, .77f), new Vector2(.56f, .98f));
            runner.nameText.fontSize = 30; runner.nameText.resizeTextForBestFit = false;
            runner.nameText.alignment = TextAnchor.MiddleLeft;
        }
        if (runner.dialogueText != null)
        {
            Layout(runner.dialogueText.rectTransform, new Vector2(.045f, .12f), new Vector2(.57f, .75f));
            runner.dialogueText.fontSize = 28; runner.dialogueText.resizeTextForBestFit = false;
            runner.dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            runner.dialogueText.alignment = TextAnchor.UpperLeft;
            runner.dialogueText.lineSpacing = 1.1f;
        }
        if (runner.choicesContainer != null)
        {
            Layout((RectTransform)runner.choicesContainer, new Vector2(.62f, .12f), new Vector2(.96f, .86f));
            var group = runner.choicesContainer.GetComponent<VerticalLayoutGroup>();
            if (group == null) group = runner.choicesContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = 10; group.childControlHeight = true; group.childControlWidth = true;
            group.childForceExpandHeight = false; group.childForceExpandWidth = true;
        }
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
        layout.minHeight = 52; layout.preferredHeight = 56;
        Text text = button.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.fontSize = 25; text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.alignment = TextAnchor.MiddleLeft;
        }
    }
}
