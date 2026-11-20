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
                if (material == null || material.shader.name == "Sprites/Default")
                    sprite.sharedMaterial = litMaterial;
            }
        }

        foreach (Light2D ambient in FindObjectsOfType<Light2D>())
        {
            if (ambient.gameObject.scene == gameObject.scene && ambient.lightType == Light2D.LightType.Global)
                ambient.intensity = 0.42f;
        }

        Vector3 center = lamp.bounds.center;
        roomLight = CreateLight("Тёплый свет комнаты", center + new Vector3(0.7f, -3.7f, 0f),
            new Color(1f, 0.81f, 0.58f), 0.83f, 2.1f, 7.1f);
        shadeLight = CreateLight("Свет абажура", center, new Color(1f, 0.90f, 0.71f),
            0.72f, 0.65f, 2.1f);
        ApplyState();
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
        if (!Input.GetMouseButtonDown(0) || wallSwitch == null || cameraMovement.HasFocus) return;
        Vector3 mouse = Input.mousePosition;
        // The room camera renders to the full-screen RawImage through a RenderTexture.
        Ray ray = roomCamera.ViewportPointToRay(new Vector3(mouse.x / Screen.width, mouse.y / Screen.height));
        var plane = new Plane(wallSwitch.transform.forward, wallSwitch.transform.position);
        if (!plane.Raycast(ray, out float distance)) return;
        Vector3 point = ray.GetPoint(distance);
        Bounds bounds = wallSwitch.bounds;
        if (point.x >= bounds.min.x && point.x <= bounds.max.x && point.y >= bounds.min.y && point.y <= bounds.max.y)
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
    }
}
