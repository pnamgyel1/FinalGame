using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BalloonUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Balloon Settings")]
    public bool isCorrect = false;       // Mark this true if this balloon is the correct answer
    public Image letterImage;            // Child Image with the Dzongkha letter sprite

    [HideInInspector] public RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        // Make sure balloon background is clickable
        var img = GetComponent<Image>();
        if (img) img.raycastTarget = true;

        // Auto-find child named "Letter" if not assigned
        if (letterImage == null)
        {
            var child = transform.Find("Letter");
            if (child != null)
                letterImage = child.GetComponent<Image>();
        }
    }

    // Called by GameManager during level setup (kept for compatibility)
    public void Register() { }

    // Called when balloon is tapped
    public void OnPointerClick(PointerEventData eventData)
    {
        if (GameManagerUI.Instance != null)
        {
            GameManagerUI.Instance.OnBalloonTapped(this);
        }
    }

    // Provide the letter sprite to GameManager
    public Sprite GetLetterSprite()
    {
        return letterImage != null ? letterImage.sprite : null;
    }
}
