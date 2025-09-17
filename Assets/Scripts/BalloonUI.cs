using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BalloonUI : MonoBehaviour, IPointerClickHandler
{
    public bool isCorrect = false;

    [HideInInspector] public RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        // Make sure this Image can receive raycasts for tapping
        var img = GetComponent<Image>();
        if (img) img.raycastTarget = true;
    }

    // Called by GameManager during level setup
    public void Register() { }

    // Tap on balloon
    public void OnPointerClick(PointerEventData eventData)
    {
        if (GameManagerUI.Instance != null)
        {
            GameManagerUI.Instance.OnBalloonTapped(this);
        }
    }
}
