using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SteppedRoomClock : MonoBehaviour
{
    private Transform minuteHand, hourHand;
    private double startedAt, initialSeconds;
    public static double SimulatedTicks(double realSeconds) => Math.Floor(Math.Max(0, realSeconds) * 2.0);
    public static float MinuteAngle(double seconds) => (float)(-(seconds % 3600.0) * 0.1);
    public static float HourAngle(double seconds) => (float)(-(seconds % 43200.0) / 120.0);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= Install;
        SceneManager.sceneLoaded += Install;
    }
    private static void Install(Scene scene, LoadSceneMode mode)
    {
        if (Camera.main == null || Camera.main.gameObject.scene != scene || Camera.main.GetComponent<CameraMovement>() == null) return;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (SpriteRenderer sprite in root.GetComponentsInChildren<SpriteRenderer>(true))
                if (sprite.sprite != null && sprite.sprite.name == "Кривые часы" && sprite.GetComponent<SteppedRoomClock>() == null)
                    sprite.gameObject.AddComponent<SteppedRoomClock>();
    }
    private void Awake()
    {
        var face = GetComponent<SpriteRenderer>();
        if (face == null || face.sprite == null) { enabled = false; return; }
        Sprite sprite = face.sprite;
        Vector3 axle = new Vector3((sprite.rect.width * .478f - sprite.pivot.x) / sprite.pixelsPerUnit,
            (sprite.rect.height * .442f - sprite.pivot.y) / sprite.pixelsPerUnit, 0);
        minuteHand = MakeHand("Минутная стрелка", "RoomStations/ClockMinute", axle, 1.02f, face, 2);
        hourHand = MakeHand("Часовая стрелка", "RoomStations/ClockHour", axle, .72f, face, 3);
        initialSeconds = Math.Floor(DateTime.Now.TimeOfDay.TotalSeconds);
        startedAt = Time.unscaledTimeAsDouble;
        Update();
    }
    private Transform MakeHand(string label, string resource, Vector3 axle, float length, SpriteRenderer face, int order)
    {
        Sprite sprite = Resources.Load<Sprite>(resource);
        if (sprite == null) return null;
        var hand = new GameObject(label);
        hand.transform.SetParent(transform, false); hand.transform.localPosition = axle;
        var renderer = hand.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite; renderer.sortingLayerID = face.sortingLayerID;
        renderer.sortingOrder = face.sortingOrder + order; renderer.sharedMaterial = face.sharedMaterial;
        float tipLength = (sprite.rect.height - sprite.pivot.y) / sprite.pixelsPerUnit;
        hand.transform.localScale = Vector3.one * (length / Mathf.Max(.01f, tipLength));
        return hand.transform;
    }
    private void Update()
    {
        double seconds = initialSeconds + SimulatedTicks(Time.unscaledTimeAsDouble - startedAt);
        if (minuteHand != null) minuteHand.localRotation = Quaternion.Euler(0, 0, MinuteAngle(seconds));
        if (hourHand != null) hourHand.localRotation = Quaternion.Euler(0, 0, HourAngle(seconds));
    }
}
