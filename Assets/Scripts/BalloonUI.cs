using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BalloonUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Balloon Settings")]
    public bool isCorrect = false; // Marks if this balloon is the correct answer

    [HideInInspector] public RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        // Ensure the balloon image receives clicks
        var img = GetComponent<Image>();
        if (img) img.raycastTarget = true;
    }

    // Reset balloon when level restarts
    public void Register()
    {
        gameObject.SetActive(true);
        if (rectTransform != null)
            rectTransform.localScale = Vector3.one;
    }

    // Handle balloon tap
    public void OnPointerClick(PointerEventData eventData)
    {
        GameManagerUI.Instance?.OnBalloonTapped(this);
    }
}
