using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManagerUI : MonoBehaviour
{
    public static GameManagerUI Instance { get; private set; }

    [Header("Gun Settings")]
    public float gunRotationOffset = -90f;
    public float gunReturnSpeed = 1.5f; // lower = slower natural return
    private Quaternion gunOriginalRotation;

    [Header("Scene Roots")]
    public Canvas mainCanvas;
    public RectTransform levelsRoot;
    public RectTransform questionRoot;
    public RectTransform droppedLettersRoot;

    [Header("Levels")]
    public GameObject[] levelObjects;       // Level1, Level2, ...
    public Image[] questionImages;          // Question_Level1, Question_Level2, ...

    [Header("Gun / Flash")]
    public RectTransform gun;
    public Image gunFlash;
    public float flashDuration = 0.1f;

    [Header("HUD")]
    public Image[] ammoIcons;               // ammo images (3)
    public GameObject wrongSignUI;

    [Header("Audio")]
    public AudioSource sfxSource;
    public AudioClip shootClip;
    public AudioClip wrongClip;

    [Header("Gameplay Settings")]
    public int bulletsPerLevel = 3;
    public float restartDelay = 0.9f;
    public float nextLevelDelay = 1.5f; // extra delay after letter fills

    int currentLevelIndex = 0;
    bool levelActive = false;
    bool levelCleared = false;
    int bulletsLeft = 0;
    List<BalloonUI> activeBalloons = new List<BalloonUI>();

    // Auto-detected LetterSlots (instead of dragging manually)
    private RectTransform[] sentenceTargets;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // deactivate all levels & questions at start
        for (int i = 0; i < levelObjects.Length; i++)
        {
            if (levelObjects[i]) levelObjects[i].SetActive(false);
            if (i < questionImages.Length && questionImages[i]) questionImages[i].gameObject.SetActive(false);
        }

        // create storage for sentence targets
        sentenceTargets = new RectTransform[levelObjects.Length];

        StartLevel(0, true);
    }

    void Update()
    {
        // Smoothly return gun to original rotation
        if (gun != null && !gunReturningInstant)
        {
            gun.rotation = Quaternion.Slerp(gun.rotation, gunOriginalRotation, Time.deltaTime * gunReturnSpeed);
        }
    }

    private bool gunReturningInstant = false;

    public void StartLevel(int index, bool playIntro = true)
    {
        // deactivate others
        for (int i = 0; i < levelObjects.Length; i++)
        {
            if (levelObjects[i]) levelObjects[i].SetActive(i == index);
            if (i < questionImages.Length && questionImages[i]) questionImages[i].gameObject.SetActive(i == index);
        }

        currentLevelIndex = index;
        levelCleared = false;
        levelActive = false;
        bulletsLeft = bulletsPerLevel;
        UpdateAmmoUI();

        // clear dropped letters
        if (droppedLettersRoot != null)
        {
            foreach (Transform child in droppedLettersRoot)
                Destroy(child.gameObject);
        }

        // cache balloons
        activeBalloons.Clear();
        var level = levelObjects[currentLevelIndex].transform;

        var balloonsRoot = level.Find("Balloons");
        if (balloonsRoot != null)
        {
            foreach (Transform child in balloonsRoot)
            {
                child.gameObject.SetActive(true);
                var b = child.GetComponent<BalloonUI>();
                if (b != null)
                {
                    activeBalloons.Add(b);
                    b.Register();
                }
            }
        }

        // cache gun
        gun = level.Find("Gun") as RectTransform;
        if (gun != null)
        {
            var flash = gun.Find("Flash");
            if (flash != null) gunFlash = flash.GetComponent<Image>();
            if (gunFlash != null) gunFlash.enabled = false;

            gunOriginalRotation = gun.rotation; // store reset point
        }

        // ✅ auto-detect the Sentence → LetterSlot for this level
        DetectLetterSlot(index);

        // 🔊 Play intro before enabling taps
        if (playIntro)
        {
            var intro = level.Find("LevelAudio")?.GetComponent<AudioSource>();
            if (intro != null && intro.clip != null)
            {
                intro.Stop();
                intro.Play();
                StartCoroutine(EnableInteractionAfterAudio(intro.clip.length));
            }
            else
            {
                levelActive = true;
            }
        }
        else
        {
            levelActive = true;
        }
    }

    private void DetectLetterSlot(int index)
    {
        if (index >= questionImages.Length) return;

        var question = questionImages[index];
        if (question == null) return;

        var sentence = question.transform.Find("Sentence");
        if (sentence != null)
        {
            var slot = sentence.Find("LetterSlot");
            if (slot != null)
            {
                sentenceTargets[index] = slot as RectTransform;
                Debug.Log($"[GameManagerUI] Auto-detected LetterSlot for level {index}");
            }
        }
    }

    IEnumerator EnableInteractionAfterAudio(float delay)
    {
        yield return new WaitForSeconds(delay);
        levelActive = true;
    }

    public void OnBalloonTapped(BalloonUI b)
    {
        if (!levelActive || levelCleared) return;
        if (bulletsLeft <= 0) return;

        // 🔫 rotate gun towards balloon
        if (gun != null)
        {
            Vector3 dir = b.transform.position - gun.position;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            gun.rotation = Quaternion.AngleAxis(angle + gunRotationOffset, Vector3.forward);
        }

        StartCoroutine(FlashGun());
        if (sfxSource && shootClip) sfxSource.PlayOneShot(shootClip);

        bulletsLeft--;
        UpdateAmmoUI();

        if (b.isCorrect)
        {
            levelCleared = true;

            // ✅ play correct audio
            var levelGO = levelObjects[currentLevelIndex];
            var correctAudio = levelGO.transform.Find("CorrectAudio")?.GetComponent<AudioSource>();
            if (correctAudio != null && correctAudio.clip != null)
            {
                correctAudio.Stop();
                correctAudio.Play();
            }

            StartCoroutine(DropLetterThenNext(b));
        }
        else
        {
            if (sfxSource && wrongClip) sfxSource.PlayOneShot(wrongClip);
            StartCoroutine(FlashWrongSign());
            b.gameObject.SetActive(false);

            if (bulletsLeft <= 0 && !levelCleared)
            {
                StartCoroutine(RestartAfterDelay(restartDelay));
            }
        }

        // start gun return after shot
        StartCoroutine(ReturnGunSmoothly());
    }

    IEnumerator ReturnGunSmoothly()
    {
        gunReturningInstant = true;
        yield return new WaitForSeconds(0.15f); // small pause after firing
        gunReturningInstant = false; // Update() will smoothly rotate it back
    }

    IEnumerator FlashGun()
    {
        if (gunFlash != null)
        {
            gunFlash.enabled = true;
            yield return new WaitForSeconds(flashDuration);
            gunFlash.enabled = false;
        }
    }

    IEnumerator FlashWrongSign()
    {
        if (wrongSignUI)
        {
            wrongSignUI.SetActive(true);
            yield return new WaitForSeconds(0.6f);
            wrongSignUI.SetActive(false);
        }
    }

    IEnumerator DropLetterThenNext(BalloonUI sourceBalloon)
    {
        if (droppedLettersRoot == null) droppedLettersRoot = mainCanvas.transform as RectTransform;

        Image letterImg = sourceBalloon.transform.Find("Letter")?.GetComponent<Image>();

        if (letterImg != null && letterImg.sprite != null)
        {
            RectTransform target = sentenceTargets[currentLevelIndex];
            if (target == null)
            {
                Debug.LogWarning($"⚠ No LetterSlot found for level {currentLevelIndex}");
            }
            else
            {
                var dropGO = new GameObject("DroppedLetter", typeof(RectTransform), typeof(Image));
                dropGO.transform.SetParent(droppedLettersRoot, false);
                var dropRT = dropGO.GetComponent<RectTransform>();
                var dropImage = dropGO.GetComponent<Image>();
                dropImage.sprite = letterImg.sprite;
                dropImage.preserveAspect = true;

                RectTransform canvasRT = mainCanvas.GetComponent<RectTransform>();
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, letterImg.rectTransform.position);
                Vector2 startLocal;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenPoint, null, out startLocal);
                dropRT.anchoredPosition = startLocal;

                // animate to slot
                Vector2 targetPos = target.anchoredPosition;
                float t = 0f, dur = 0.6f; // slightly longer animation
                while (t < dur)
                {
                    t += Time.deltaTime;
                    dropRT.anchoredPosition = Vector2.Lerp(startLocal, targetPos, t / dur);
                    yield return null;
                }
                dropRT.SetParent(target, false);
                dropRT.anchoredPosition = Vector2.zero;
                dropRT.sizeDelta = target.sizeDelta;
            }
        }

        sourceBalloon.gameObject.SetActive(false);

        // ✅ Wait until letter fills + extra pause
        yield return new WaitForSeconds(nextLevelDelay);

        int next = currentLevelIndex + 1;
        if (next < levelObjects.Length)
            StartLevel(next, true);
        else
            GameFinished();
    }

    IEnumerator RestartAfterDelay(float t)
    {
        yield return new WaitForSeconds(t);
        if (!levelCleared)
        {
            StartLevel(currentLevelIndex, false);
        }
    }

    void UpdateAmmoUI()
    {
        if (ammoIcons == null) return;
        for (int i = 0; i < ammoIcons.Length; i++)
        {
            if (ammoIcons[i]) ammoIcons[i].gameObject.SetActive(i < bulletsLeft);
        }
    }

    void GameFinished()
    {
        levelActive = false;
        Debug.Log("Game finished!");
    }
}
