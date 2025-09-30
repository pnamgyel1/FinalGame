using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class OutroManager : MonoBehaviour
{
    [Header("Roots")]
    public GameObject outroRoot;         // Outro UI Root (Game Over screen)
    public GameObject menuRoot;          // Main Menu Root
    public GameObject gameRoot;          // Game UI Root

    [Header("Audio")]
    public AudioSource outroAudio;       // Outro voice line

    [Header("Images")]
    public Image outroImage;             // Dzongkha "Game Over!" image

    [Header("Game Manager")]
    public GameManagerUI gameManager;

    private void Start()
    {
        if (outroRoot) outroRoot.SetActive(false);
        if (outroImage != null) outroImage.gameObject.SetActive(false);
    }

    /// <summary>
    /// Called by GameManager when all levels are finished
    /// </summary>
    public void PlayOutro()
    {
        if (gameRoot) gameRoot.SetActive(false);
        if (outroRoot) outroRoot.SetActive(true);

        StartCoroutine(PlayOutroSequence());
    }

    private IEnumerator PlayOutroSequence()
    {
        if (outroAudio != null)
        {
            outroAudio.Play();
            yield return new WaitForSeconds(0.5f);

            // Show Dzongkha text after short delay
            if (outroImage != null) outroImage.gameObject.SetActive(true);

            yield return new WaitForSeconds(outroAudio.clip.length);
        }
    }

    /// <summary>
    /// Called by Play Again button
    /// </summary>
    public void OnPlayAgainClicked()
    {
        if (outroRoot) outroRoot.SetActive(false);
        if (menuRoot) menuRoot.SetActive(true);

        if (gameManager != null)
        {
            gameManager.StartLevel(0, true);
        }
    }
}
