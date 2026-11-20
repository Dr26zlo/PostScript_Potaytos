using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(AudioSource))]
public sealed class WindowRain : MonoBehaviour
{
    [SerializeField] private SpriteRenderer outside;
    [SerializeField] private AudioClip rainLoop;
    [SerializeField, Range(0f, 1f)] private float rainVolume = 0.25f;
    [SerializeField, Min(1)] private int dropCount = 180;

    private struct Drop
    {
        public Vector2 position;
        public float speed;
        public float length;
    }

    private static readonly Vector2 Fall = new Vector2(-0.29f, -1f);
    private readonly System.Random random = new System.Random(8327);
    private Drop[] drops;
    private Vector3[] vertices;
    private Mesh mesh;
    private Material rainMaterial;
    private AudioSource audioSource;
    private Rect area;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = rainLoop;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.panStereo = -0.22f;
        audioSource.volume = rainVolume;

        if (outside == null || outside.sprite == null) return;

        var bounds = outside.sprite.bounds;
        area = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        int count = Mathf.Max(1, dropCount);
        drops = new Drop[count];
        vertices = new Vector3[count * 4];
        var colors = new Color32[vertices.Length];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[count * 6];
        for (int i = 0; i < count; i++)
        {
            ResetDrop(ref drops[i], RandomRange(area.yMin, area.yMax));
            int v = i * 4;
            int t = i * 6;
            triangles[t] = v;
            triangles[t + 1] = v + 2;
            triangles[t + 2] = v + 1;
            triangles[t + 3] = v + 1;
            triangles[t + 4] = v + 2;
            triangles[t + 5] = v + 3;
            byte alpha = (byte)RandomRange(38f, 104f);
            colors[v] = colors[v + 1] = new Color32(140, 178, 203, 0);
            colors[v + 2] = colors[v + 3] = new Color32(165, 200, 219, alpha);
        }

        mesh = new Mesh { name = "Window Rain", indexFormat = IndexFormat.UInt32 };
        mesh.MarkDynamic();
        mesh.vertices = vertices;
        mesh.colors32 = colors;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.bounds = new Bounds(area.center, new Vector3(area.width + 2f, area.height + 3f, 1f));
        GetComponent<MeshFilter>().sharedMesh = mesh;

        rainMaterial = new Material(Shader.Find("Sprites/Default"));
        rainMaterial.mainTexture = Texture2D.whiteTexture;
        var renderer = GetComponent<MeshRenderer>();
        renderer.sharedMaterial = rainMaterial;
        renderer.sortingLayerID = outside.sortingLayerID;
        renderer.sortingOrder = outside.sortingOrder + 1;
        UpdateDrops(0f);
    }

    private void OnEnable()
    {
        if (audioSource != null && audioSource.clip != null) audioSource.Play();
    }

    private void OnDisable()
    {
        if (audioSource != null) audioSource.Stop();
    }

    private void Update()
    {
        if (mesh != null) UpdateDrops(Time.deltaTime);
    }

    private void UpdateDrops(float seconds)
    {
        for (int i = 0; i < drops.Length; i++)
        {
            ref Drop drop = ref drops[i];
            drop.position += Fall * (drop.speed * seconds);
            if (drop.position.y < area.yMin - 1f)
                ResetDrop(ref drop, area.yMax + 0.5f);
            if (drop.position.x < area.xMin - 0.4f)
                drop.position.x += area.width + 0.8f;

            Vector2 tail = drop.position - Fall * drop.length;
            Vector2 edge = Vector2.right * (0.012f + drop.length * 0.013f);
            int v = i * 4;
            vertices[v] = tail - edge;
            vertices[v + 1] = tail + edge;
            vertices[v + 2] = drop.position - edge;
            vertices[v + 3] = drop.position + edge;
        }
        mesh.vertices = vertices;
    }

    private void ResetDrop(ref Drop drop, float y)
    {
        drop.position = new Vector2(RandomRange(area.xMin, area.xMax), y);
        drop.speed = RandomRange(5f, 10f);
        drop.length = RandomRange(0.24f, 0.77f);
    }

    private float RandomRange(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (rainMaterial != null) Destroy(rainMaterial);
    }
}
