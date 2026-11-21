using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class RoomLamp : MonoBehaviour
{
    private SpriteRenderer lamp, wallSwitch;
    private CameraMovement cameraMovement;
    public bool IsOn => isOn;
    private Sprite litSprite, unlitSprite;
    private Light2D roomLight, shadeLight;
    private InteractiveObject interaction;
    private Camera roomCamera;
    private AudioSource audioSource;
    private AudioClip switchSound;
    private bool isOn;
    private readonly List<Light2D> accentLights = new List<Light2D>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= InstallInScene;
        SceneManager.sceneLoaded += InstallInScene;
    }

    private static void InstallInScene(Scene scene, LoadSceneMode mode)
    {
        Camera camera = Camera.main;
        if (camera == null || camera.gameObject.scene != scene || camera.GetComponent<CameraMovement>() == null)
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform fixtures = root.transform.Find("Освещение");
            if (fixtures == null) continue;
            Transform lit = fixtures.Find("Включённая лампа");
            Transform unlit = fixtures.Find("Выключенная лампа");
            if (lit == null || unlit == null || fixtures.GetComponent<RoomLamp>() != null) continue;
            var litRenderer = lit.GetComponent<SpriteRenderer>();
            var unlitRenderer = unlit.GetComponent<SpriteRenderer>();
            if (litRenderer == null || unlitRenderer == null) continue;

            var controller = fixtures.gameObject.AddComponent<RoomLamp>();
            controller.Configure(camera, root, litRenderer, unlitRenderer);
            break;
        }
    }

    private void Configure(Camera camera, GameObject room, SpriteRenderer lit, SpriteRenderer unlit)
    {
        roomCamera = camera;
        lamp = lit;
        litSprite = lit.sprite;
        unlitSprite = unlit.sprite;
        isOn = lit.gameObject.activeSelf;
        unlit.gameObject.SetActive(false);
        lit.gameObject.SetActive(true);

        cameraMovement = camera.GetComponent<CameraMovement>();
        Sprite switchSprite = Resources.Load<Sprite>("RoomStations/WallSwitch");
        var switchObject = new GameObject("Выключатель на стене");
        switchObject.transform.SetParent(room.transform, false);
        switchObject.transform.localPosition = new Vector3(-2.25f, -.45f, 0);
        wallSwitch = switchObject.AddComponent<SpriteRenderer>();
        wallSwitch.sprite = switchSprite;
        wallSwitch.sortingLayerID = lamp.sortingLayerID;
        wallSwitch.sortingOrder = 60;
        if (switchSprite != null) switchObject.transform.localScale = Vector3.one * (0.62f / switchSprite.bounds.size.x);
        switchObject.AddComponent<BoxCollider2D>();
        interaction = switchObject.AddComponent<InteractiveObject>();
        interaction.UseAutomaticMouseClicks = false;
        if (interaction.onClick == null) interaction.onClick = new UnityEvent();
        interaction.onClick.AddListener(Toggle);

        audioSource = lamp.gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        switchSound = Resources.Load<AudioClip>("RoomAudio/LampSwitch");

        Material litMaterial = Resources.Load<Material>("RoomLighting/RoomSpriteLit");
        if (litMaterial != null)
        {
            foreach (SpriteRenderer sprite in room.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Material material = sprite.sharedMaterial;
                if (material == null || material.shader.name == "Sprites/Default" || material.shader.name.Contains("Sprite-Unlit"))
                    sprite.sharedMaterial = litMaterial;
            }
        }

        foreach (Light2D ambient in FindObjectsOfType<Light2D>())
        {
            if (ambient.gameObject.scene == gameObject.scene && ambient.lightType == Light2D.LightType.Global)
            {
                ambient.intensity = .23f;
                ambient.color = new Color(.64f, .75f, 1f);
            }
            else if (ambient.gameObject.scene == gameObject.scene) ambient.intensity *= .25f;
        }

        Vector3 center = lamp.bounds.center;
        roomLight = CreateLight("Тёплый свет комнаты", center + new Vector3(0.7f, -3.7f, 0f),
            new Color(1f, .83f, .64f), .45f, 1.5f, 6.4f);
        shadeLight = CreateLight("Свет абажура", center, new Color(1f, 0.90f, 0.71f),
            1.05f, .35f, 1.9f);
        FitDoor(room.transform);
        AddAccent("Свет в зоне клиента", new Vector3(.54f, -.7f, 0),
            new Color(1f, .82f, .66f), .66f, .45f, 2.6f);
        AddAccent("Свет на двери", FindCentre(room.transform, "Закрытая дверь", Vector3.zero),
            new Color(1f, .86f, .72f), .38f, .5f, 2.8f);
        AddAccent("Свет на доске", room.transform.TransformPoint(new Vector3(15.6f, .4f, 0)),
            new Color(1f, .87f, .71f), .65f, .65f, 3.3f);
        AddAccent("Свет на рабочем столе", room.transform.TransformPoint(new Vector3(-.2f, -7.2f, 0)),
            new Color(1f, .86f, .69f), .64f, 1f, 4.4f);
        // Window light remains when the lamp is off, keeping the scene legible.
        CreateLight("Холодный свет окна", room.transform.TransformPoint(new Vector3(-10.4f, 1.1f, 0)),
            new Color(.58f, .72f, 1f), .24f, .8f, 4.7f);
        CreateLight("Лунный отблеск на ближней поверхности", room.transform.TransformPoint(new Vector3(-5.4f,-4.8f,0)),
            new Color(.68f,.79f,1f),.16f,.2f,3.4f);
        ApplyState();
    }

    private void AddAccent(string name, Vector3 position, Color color, float intensity, float inner, float outer)
    {
        accentLights.Add(CreateLight(name, position, color, intensity, inner, outer));
    }
    private static Vector3 FindCentre(Transform room, string name, Vector3 fallback)
    {
        foreach (Transform part in room.GetComponentsInChildren<Transform>(true))
        {
            if (part.name != name) continue;
            var sprite = part.GetComponent<SpriteRenderer>();
            if (sprite != null) return sprite.bounds.center;
        }
        return fallback;
    }
    private static void FitDoor(Transform room)
    {
        Transform door = room.Find("Дверь");
        if (door == null) return;
        SpriteRenderer closed = null;
        foreach (var sprite in door.GetComponentsInChildren<SpriteRenderer>(true))
            if (sprite.name == "Закрытая дверь") closed = sprite;
        if (closed == null) return;
        float bottom = closed.bounds.min.y;
        door.localScale = Vector3.Scale(door.localScale, new Vector3(1.07f, 1.035f, 1));
        door.position += Vector3.up * (bottom - closed.bounds.min.y);
    }

    private Light2D CreateLight(string label, Vector3 position, Color color, float intensity, float innerRadius, float outerRadius)
    {
        var lightObject = new GameObject(label);
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.position = position;
        Light2D light = lightObject.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.pointLightOuterRadius = outerRadius;
        light.pointLightInnerRadius = innerRadius;
        return light;
    }

    private void Update()
    {
        if (DemoDirector.Instance != null && DemoDirector.Instance.Mode != "room") return;
        if (!Input.GetMouseButtonDown(0) || wallSwitch == null || cameraMovement.HasFocus) return;
        Vector3 mouse = Input.mousePosition;
        // The room camera renders to the full-screen RawImage through a RenderTexture.
        Ray ray = roomCamera.ViewportPointToRay(new Vector3(mouse.x / Screen.width, mouse.y / Screen.height));
        var plane = new Plane(wallSwitch.transform.forward, wallSwitch.transform.position);
        if (!plane.Raycast(ray, out float distance)) return;
        Vector3 point = ray.GetPoint(distance);
        Bounds bounds = wallSwitch.bounds;
        if (SpriteHit.Contains(wallSwitch,point))
            interaction.Click();
    }

    private void Toggle()
    {
        isOn = !isOn;
        ApplyState();
        if (switchSound != null)
        {
            audioSource.pitch = isOn ? 1f : 0.88f;
            audioSource.PlayOneShot(switchSound, 0.78f);
        }
    }

    private void ApplyState()
    {
        lamp.sprite = isOn ? litSprite : unlitSprite;
        if (wallSwitch != null) wallSwitch.flipY = !isOn;
        roomLight.enabled = isOn;
        shadeLight.enabled = isOn;
        foreach (Light2D light in accentLights) if (light != null) light.enabled = isOn;
    }
}
