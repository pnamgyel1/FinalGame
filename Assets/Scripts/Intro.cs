using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class IntroManager : MonoBehaviour
{
    [Header("Roots")]
    public GameObject menuRoot;          // Main Menu Screen
    public GameObject introRoot;         // Intro UI (Dzongkha text images)
    public GameObject levelsRoot;        // Game Levels Root
    public GameObject questionRoot;      // Question Root
    public GameObject hudRoot;           // HUD Root
    public GameObject droppedLettersRoot;

    [Header("Audio")]
    public AudioSource introAudio1;      // First 7 sec intro
    public AudioSource introAudio2;      // Second 49 sec intro
    public AudioSource wushSfx;          // Wush sound effect

    [Header("Images")]
    public Image[] timedImages;          // Dzongkha images shown in order
    public float[] imageTimings;         // When each image appears (seconds from start of IntroAudio2)

    [Header("Game Manager")]
    public GameManagerUI gameManager;

    private void Start()
    {
        // At start → only Menu visible
        if (menuRoot) menuRoot.SetActive(true);
        if (introRoot) introRoot.SetActive(false);
        if (levelsRoot) levelsRoot.SetActive(false);
        if (questionRoot) questionRoot.SetActive(false);
        if (hudRoot) hudRoot.SetActive(false);
    }

    /// <summary>
    /// Called by Play Button
    /// </summary>
    public void OnPlayButtonClicked()
    {
        if (menuRoot) menuRoot.SetActive(false);
        if (introRoot) introRoot.SetActive(true);

        StartCoroutine(PlayIntroSequence());
    }

    private IEnumerator PlayIntroSequence()
    {
        // Step 1 → Play first 7 sec intro with Wush Sfx
        if (introAudio1 != null)
        {
            introAudio1.Play();
            if (wushSfx != null) wushSfx.Play();
            yield return new WaitForSeconds(introAudio1.clip.length);
        }

        // Step 2 → Play second 49 sec intro + timed images
        if (introAudio2 != null)
        {
            introAudio2.Play();
            if (timedImages != null && imageTimings != null)
            {
                for (int i = 0; i < timedImages.Length; i++)
                {
                    if (timedImages[i] != null)
                        timedImages[i].gameObject.SetActive(false);
                }

                for (int i = 0; i < timedImages.Length; i++)
                {
                    if (timedImages[i] != null && i < imageTimings.Length)
                    {
                        yield return new WaitForSeconds(imageTimings[i]);
                        timedImages[i].gameObject.SetActive(true);

                        if (wushSfx != null) wushSfx.Play();
                    }
                }
            }
            yield return new WaitForSeconds(introAudio2.clip.length);
        }

        // Step 3 → Hide intro, start tutorial (Level0)
        if (introRoot) introRoot.SetActive(false);
        if (levelsRoot) levelsRoot.SetActive(true);
        if (questionRoot) questionRoot.SetActive(true);
        if (hudRoot) hudRoot.SetActive(true);

        if (gameManager != null)
        {
            gameManager.StartLevel(0, true);
        }
    }
}
