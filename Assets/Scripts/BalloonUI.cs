using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BalloonUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Balloon Settings")]
    public bool isCorrect = false;

    [Header("Floating Motion")]
    public float verticalAmplitude = 12f;
    public float verticalSpeed = 0.8f;
    public float horizontalAmplitude = 4f;
    public float horizontalSpeed = 0.4f;
    public float rotationAmplitude = 4f;
    public float rotationSpeed = 0.5f;
    public bool randomizeMotion = true;

    [HideInInspector] public RectTransform rectTransform;

    float baseX, baseY, offset;

    void Awake()
    {
        // Auto-assign rectTransform and ensure raycast works
        rectTransform = GetComponent<RectTransform>();
        var img = GetComponent<Image>();
        if (img) img.raycastTarget = true;
    }

    void Start()
    {
        baseX = rectTransform.localPosition.x;
        baseY = rectTransform.localPosition.y;
        offset = randomizeMotion ? Random.Range(0f, Mathf.PI * 2f) : 0f;
    }

    void Update()
    {
        float t = Time.time + offset;
        float y = baseY + Mathf.Sin(t * verticalSpeed) * verticalAmplitude;
        float x = baseX + Mathf.Sin(t * horizontalSpeed) * horizontalAmplitude;
        float r = Mathf.Sin(t * rotationSpeed) * rotationAmplitude;

        rectTransform.localPosition = new Vector3(x, y, 0f);
        rectTransform.localRotation = Quaternion.Euler(0, 0, r);
    }

    public void Register()
    {
        gameObject.SetActive(true);
        if (!rectTransform) rectTransform = GetComponent<RectTransform>();
        rectTransform.localScale = Vector3.one;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        GameManagerUI.Instance?.OnBalloonTapped(this);
    }
}
