using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FlashController : MonoBehaviour
{
    public Image flashImage;       // child Image (Flash)
    public float flashDuration = 0.12f;
    public float maxScale = 1.2f;

    void Awake()
    {
        if (flashImage == null)
        {
            // try find a child named "Flash"
            var t = transform.Find("Flash");
            if (t != null) flashImage = t.GetComponent<Image>();
        }
        if (flashImage != null) flashImage.gameObject.SetActive(false);
    }

    public void PlayFlash()
    {
        if (flashImage != null) StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        flashImage.gameObject.SetActive(true);
        flashImage.transform.localScale = Vector3.one * 0.7f;
        float t = 0f;
        while (t < flashDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = t / flashDuration;
            flashImage.transform.localScale = Vector3.Lerp(Vector3.one * 0.7f, Vector3.one * maxScale, p);
            yield return null;
        }
        flashImage.gameObject.SetActive(false);
    }
}
