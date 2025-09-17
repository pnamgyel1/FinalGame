using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameManagerUI : MonoBehaviour
{
    public static GameManagerUI Instance { get; private set; }

    [Header("Gun Settings")]
    public float gunRotationOffset = -90f; // adjust based on your sprite orientation

    [Header("Scene Roots")]
    public Canvas mainCanvas;
    public RectTransform levelsRoot;
    public RectTransform questionRoot;
    public RectTransform droppedLettersRoot;

    [Header("Levels")]
    public GameObject[] levelObjects;       // Level1, Level2, ...
    public Image[] questionImages;          // Question_Level1, Question_Level2, ...
    public RectTransform[] sentenceTargets; // SentenceTarget per level

    [Header("Gun / Flash")]
    public RectTransform gun;               // reference to gun in current level
    public Image gunFlash;                  // flash image child of gun
    public float flashDuration = 0.1f;

    [Header("HUD")]
    public Image[] ammoIcons;               // ammo images (3)
    public GameObject wrongSignUI;

    [Header("Audio")]
    public AudioSource sfxSource;
    public AudioClip shootClip;
    public AudioClip wrongClip;             // same for all levels

    [Header("Gameplay Settings")]
    public int bulletsPerLevel = 3;
    public float restartDelay = 0.9f;

    int currentLevelIndex = 0;
    bool levelActive = false;
    bool levelCleared = false;
    int bulletsLeft = 0;
    List<BalloonUI> activeBalloons = new List<BalloonUI>();

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

        StartLevel(0, true); // first level, play intro
    }

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
        levelActive = false; // lock until intro finishes
        bulletsLeft = bulletsPerLevel;
        UpdateAmmoUI();

        // reset any old dropped letters
        if (droppedLettersRoot != null)
        {
            foreach (Transform child in droppedLettersRoot)
                Destroy(child.gameObject);
        }

        // cache balloons & gun for this level
        activeBalloons.Clear();
        var level = levelObjects[currentLevelIndex].transform;

        var balloonsRoot = level.Find("Balloons");
        if (balloonsRoot != null)
        {
            foreach (Transform child in balloonsRoot)
            {
                child.gameObject.SetActive(true); // reset popped balloons
                var b = child.GetComponent<BalloonUI>();
                if (b != null)
                {
                    activeBalloons.Add(b);
                    b.Register();
                }
            }
        }

        gun = level.Find("Gun") as RectTransform;
        if (gun != null)
        {
            var flash = gun.Find("Flash");
            if (flash != null) gunFlash = flash.GetComponent<Image>();
            if (gunFlash != null) gunFlash.enabled = false;
        }

        // 🔊 Play intro line before allowing taps
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
                levelActive = true; // no intro, unlock immediately
            }
        }
        else
        {
            // skip intro → unlock immediately
            levelActive = true;
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

        // 🔥 flash + bang
        StartCoroutine(FlashGun());
        if (sfxSource && shootClip) sfxSource.PlayOneShot(shootClip);

        // consume one shot
        bulletsLeft--;
        UpdateAmmoUI();

        if (b.isCorrect)
        {
            levelCleared = true;

            // ✅ play level-specific correct audio
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
            // ❌ wrong answer sound
            if (sfxSource && wrongClip) sfxSource.PlayOneShot(wrongClip);
            StartCoroutine(FlashWrongSign());

            // hide balloon
            b.gameObject.SetActive(false);

            // if out of ammo now, restart
            if (bulletsLeft <= 0 && !levelCleared)
            {
                StartCoroutine(RestartAfterDelay(restartDelay));
            }
        }
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
        // create dropped letter
        if (droppedLettersRoot == null) droppedLettersRoot = mainCanvas.transform as RectTransform;

        Sprite letterSprite = sourceBalloon.GetComponent<Image>()?.sprite;
        if (letterSprite != null && droppedLettersRoot != null && questionImages.Length > currentLevelIndex)
        {
            RectTransform target = (sentenceTargets.Length > currentLevelIndex && sentenceTargets[currentLevelIndex] != null)
                ? sentenceTargets[currentLevelIndex]
                : questionImages[currentLevelIndex].rectTransform;

            var dropGO = new GameObject("DroppedLetter", typeof(RectTransform), typeof(Image));
            dropGO.transform.SetParent(droppedLettersRoot, false);
            var dropRT = dropGO.GetComponent<RectTransform>();
            var dropImage = dropGO.GetComponent<Image>();
            dropImage.sprite = letterSprite;
            dropRT.sizeDelta = new Vector2(120, 120);

            RectTransform canvasRT = mainCanvas.GetComponent<RectTransform>();
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, sourceBalloon.rectTransform.position);
            Vector2 startLocal;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screenPoint, null, out startLocal);
            dropRT.anchoredPosition = startLocal;

            Vector2 targetPos = target.anchoredPosition;
            float t = 0f, dur = 0.45f;
            while (t < dur)
            {
                t += Time.deltaTime;
                dropRT.anchoredPosition = Vector2.Lerp(startLocal, targetPos, t / dur);
                yield return null;
            }
            dropRT.anchoredPosition = targetPos;
            dropRT.SetParent(target, true);
        }

        // hide balloon
        sourceBalloon.gameObject.SetActive(false);

        yield return new WaitForSeconds(0.6f);

        int next = currentLevelIndex + 1;
        if (next < levelObjects.Length)
            StartLevel(next, true); // next level, play intro
        else
            GameFinished();
    }

    IEnumerator RestartAfterDelay(float t)
    {
        yield return new WaitForSeconds(t);
        if (!levelCleared)
        {
            StartLevel(currentLevelIndex, false); // restart without intro
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
        // you can show a finish panel here if needed
    }
}
