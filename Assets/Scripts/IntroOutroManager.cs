using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class IntroOutroManager : MonoBehaviour
{
    public static IntroOutroManager Instance { get; private set; }

    [Header("Scene References")]
    public GameObject levelsRoot;
    public GameObject questionRoot;
    public GameObject hudRoot;

    [Header("Intro/Outro UI")]
    public GameObject introOutroRoot;
    public Image characterImage;
    public Image titleImage;
    public Image tutorialImage;
    public Button skipTutorialButton;
    public Image outroTitleImage;

    [Header("Audio Sources")]
    public AudioSource intro1Audio;
    public AudioSource titleSfx;
    public AudioSource intro2Audio;
    public AudioSource tutorialSfx;
    public AudioSource outroAudio;
    public AudioSource outroTitleSfx;

    [Header("Character Sprites")]
    public Sprite idleSprite;
    public Sprite[] blinkFrames;
    public Sprite[] mouthFrames;

    [Header("Intro Timings")]
    public float blinkInterval = 3f;
    public float blinkSpeed = 0.1f;
    public float mouthSpeed = 0.08f;
    public float titleFadeTime = 0.6f;
    public float titleHoldTime = 1.8f;

    [Header("Tutorial Image Timings")]
    [Tooltip("At what second during intro2 audio the image should appear")]
    public float tutorialImageShowAt = 1.5f;
    [Tooltip("At what second during intro2 audio the image should disappear")]
    public float tutorialImageHideAt = 3.5f;
    public float tutorialImageFadeTime = 0.5f;

    Coroutine blinkRoutine, mouthRoutine;
    bool introFinished;
    bool hasSkipped;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        InitializeImages();
        SetupSkipButton();
        StartCoroutine(PlayIntro());
    }

    void SetupSkipButton()
    {
        if (skipTutorialButton)
        {
            skipTutorialButton.onClick.RemoveAllListeners();
            skipTutorialButton.onClick.AddListener(SkipTutorial);
            skipTutorialButton.gameObject.SetActive(false);
        }
    }

    void InitializeImages()
    {
        if (titleImage) { titleImage.gameObject.SetActive(false); titleImage.canvasRenderer.SetAlpha(0); }
        if (tutorialImage) { tutorialImage.gameObject.SetActive(false); tutorialImage.canvasRenderer.SetAlpha(0); }
        if (outroTitleImage) { outroTitleImage.gameObject.SetActive(false); outroTitleImage.canvasRenderer.SetAlpha(0); }
    }

    public IEnumerator PlayIntro()
    {
        if (introOutroRoot) introOutroRoot.SetActive(true);
        StartAnimations();

        // Play greeting
        yield return PlayAudioClip(intro1Audio);

        // Show title
        yield return ShowImage(titleImage, titleFadeTime, titleHoldTime, titleSfx);

        // Play tutorial audio with tutorial image
        yield return PlayTutorialSequence();

        introFinished = true;
        StopAnimations();
        if (introOutroRoot) introOutroRoot.SetActive(false);
        ToggleGameplay(true);

        // Start level and let GameManagerUI handle tutorial delay
        GameManagerUI.Instance?.StartLevel(0, false);
    }

    IEnumerator PlayTutorialSequence()
    {
        if (!intro2Audio) yield break;

        intro2Audio.Play();

        if (tutorialImage)
        {
            // Wait until intro2 reaches the specified time to show image
            yield return new WaitForSeconds(tutorialImageShowAt);
            tutorialImage.gameObject.SetActive(true);
            if (skipTutorialButton) skipTutorialButton.gameObject.SetActive(true);
            if (tutorialSfx) tutorialSfx.Play();
            yield return FadeImage(tutorialImage, 0f, 1f, tutorialImageFadeTime);

            // Wait until it's time to hide the image
            float waitTime = tutorialImageHideAt - tutorialImageShowAt - tutorialImageFadeTime;
            if (waitTime > 0) yield return new WaitForSeconds(waitTime);

            // Fade out image
            yield return FadeImage(tutorialImage, 1f, 0f, tutorialImageFadeTime);
            tutorialImage.gameObject.SetActive(false);
            if (skipTutorialButton) skipTutorialButton.gameObject.SetActive(false);
        }

        // Wait for audio to finish
        yield return new WaitWhile(() => intro2Audio && intro2Audio.isPlaying);
    }

    IEnumerator PlayAudioClip(AudioSource audio)
    {
        if (audio && audio.clip)
        {
            audio.Play();
            yield return new WaitWhile(() => audio && audio.isPlaying);
        }
    }

    IEnumerator ShowImage(Image img, float fadeTime, float holdTime, AudioSource sfx)
    {
        if (!img) yield break;

        yield return new WaitForSeconds(0.3f);
        img.gameObject.SetActive(true);
        if (sfx) sfx.Play();
        yield return FadeImage(img, 0f, 1f, fadeTime);
        yield return new WaitForSeconds(holdTime);
        yield return FadeImage(img, 1f, 0f, fadeTime);
        img.gameObject.SetActive(false);
    }

    public void PlayOutro() => StartCoroutine(PlayOutroRoutine());

    IEnumerator PlayOutroRoutine()
    {
        ToggleGameplay(false);
        if (introOutroRoot) introOutroRoot.SetActive(true);
        StartAnimations();

        // Play outro audio
        yield return PlayAudioClip(outroAudio);

        // Show outro title
        yield return ShowImage(outroTitleImage, titleFadeTime, titleHoldTime, outroTitleSfx);

        StopAnimations();
        if (introOutroRoot) introOutroRoot.SetActive(false);

        // Game finished
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void SkipTutorial()
    {
        if (hasSkipped) return;
        
        hasSkipped = true;
        StopAllCoroutines();
        StopAnimations();
        SafeStopAudio(intro1Audio);
        SafeStopAudio(intro2Audio);
        InitializeImages();
        ToggleGameplay(true);
        if (introOutroRoot) introOutroRoot.SetActive(false);
        
        // Skip directly to level 1
        GameManagerUI.Instance?.StartLevel(1, true);
    }

    IEnumerator FadeImage(Image img, float from, float to, float dur)
    {
        float t = 0f;
        img.canvasRenderer.SetAlpha(from);
        while (t < dur)
        {
            t += Time.deltaTime;
            img.canvasRenderer.SetAlpha(Mathf.Lerp(from, to, t / dur));
            yield return null;
        }
        img.canvasRenderer.SetAlpha(to);
    }

    void ToggleGameplay(bool visible)
    {
        if (levelsRoot) levelsRoot.SetActive(visible);
        if (questionRoot) questionRoot.SetActive(visible);
        if (hudRoot) hudRoot.SetActive(visible);
    }

    void StartAnimations()
    {
        StopAnimations();
        if (characterImage && idleSprite) characterImage.sprite = idleSprite;
        blinkRoutine = StartCoroutine(BlinkLoop());
        mouthRoutine = StartCoroutine(MouthLoop());
    }

    void StopAnimations()
    {
        if (blinkRoutine != null) StopCoroutine(blinkRoutine);
        if (mouthRoutine != null) StopCoroutine(mouthRoutine);
        if (characterImage && idleSprite) characterImage.sprite = idleSprite;
    }

    IEnumerator BlinkLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(blinkInterval);
            foreach (var f in blinkFrames)
            {
                if (characterImage) characterImage.sprite = f;
                yield return new WaitForSeconds(blinkSpeed);
            }
            if (characterImage && idleSprite) characterImage.sprite = idleSprite;
        }
    }

    IEnumerator MouthLoop()
    {
        while (true)
        {
            foreach (var f in mouthFrames)
            {
                if (characterImage) characterImage.sprite = f;
                yield return new WaitForSeconds(mouthSpeed);
            }
            if (characterImage && idleSprite) characterImage.sprite = idleSprite;
        }
    }

    void SafeStopAudio(AudioSource audio)
    {
        if (audio) audio.Stop();
    }
}