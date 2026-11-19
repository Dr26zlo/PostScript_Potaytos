using UnityEngine;
using System.Collections;

[RequireComponent(typeof(InteractiveObject))]
public class TypewriterKey : MonoBehaviour
{
    [Header("Animation Settings")]
    public float pressDepth = 0.05f;
    public float animationDuration = 0.1f;

    [Header("Audio")]
    public AudioClip keySound;
    [Range(0f, 1f)]
    public float volume = 0.8f;

    [SerializeField] private TypewriterZoomArea zoomArea;

    private TypewriterStation station;
    private char symbol;
    internal SpriteRenderer KeySprite { get; private set; }

    internal void Bind(TypewriterStation owner, char keySymbol)
    {
        station = owner;
        symbol = keySymbol;
        KeySprite = GetComponent<SpriteRenderer>();
    }

    private AudioSource audioSource;
    private Vector3 originalPosition;
    private bool isPressed = false;

    void Start()
    {
        if (zoomArea == null)
            zoomArea = GetComponentInParent<TypewriterZoomArea>();
        if (zoomArea == null)
            zoomArea = FindObjectOfType<TypewriterZoomArea>();

        originalPosition = transform.localPosition;
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        if (station == null)
            GetComponent<InteractiveObject>().onClick?.AddListener(OnKeyPressed);
    }

    public void OnKeyPressed()
    {
        if (isPressed) return;
        if (station != null)
        {
            if (!station.TryPrint(symbol)) return;
        }
        else if (zoomArea == null || !zoomArea.CanType)
        {
            return;
        }
        StartCoroutine(PressAnimation());
    }

    IEnumerator PressAnimation()
    {
        isPressed = true;

        if (keySound != null)
        {
            audioSource.PlayOneShot(keySound, volume);
        }

        Vector3 targetPos = originalPosition + new Vector3(0, -pressDepth, 0);
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            transform.localPosition = Vector3.Lerp(originalPosition, targetPos, t);
            yield return null;
        }

        yield return new WaitForSeconds(0.05f);


        elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            transform.localPosition = Vector3.Lerp(targetPos, originalPosition, t);
            yield return null;
        }

        transform.localPosition = originalPosition;
        isPressed = false;
    }
}