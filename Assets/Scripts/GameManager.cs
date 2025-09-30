using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManagerUI : MonoBehaviour
{
    public static GameManagerUI Instance { get; private set; }

    [Header("Gun Settings")]
    public float gunRotationOffset = -90f;
    public float rotationSmoothSpeed = 8f;
    public float gunReturnDelay = 0.15f;

    private Quaternion gunOriginalRot;
    private Quaternion targetGunRot;
    private Coroutine returnCoroutine;

    [Header("Scene Roots")]
    public Canvas mainCanvas;
    public RectTransform levelsRoot;
    public RectTransform questionRoot;
    public RectTransform droppedLettersRoot;
    public RectTransform bulletRoot;

    [Header("Levels")]
    public GameObject[] levelObjects;
    public Image[] questionImages;

    [Header("Gun/Visual")]
    public RectTransform gun;
    public RectTransform firePoint;
    public Image gunFlash;

    [Header("Bullet (UI)")]
    public Sprite bulletSprite;
    public Vector2 bulletSize = new Vector2(40, 80);
    public float bulletSpeed = 16f;
    public bool rotateBulletToDirection = true;
    public float bulletRotationOffset = 90f;

    [Header("HUD")]
    public Image ammoIcon;
    public TMP_Text ammoText;
    public GameObject wrongSignUI;

    [Header("Audio")]
    public AudioSource sfxSource;
    public AudioClip shootClip;
    [Range(0f, 1f)] public float gunVolume = 0.5f;
    public AudioClip wrongClip;
    public float wrongAudioDelay = 0.15f;
    public float correctAudioDelay = 0.15f;

    [Header("Gameplay Settings")]
    public int bulletsPerLevel = 3;
    public float restartDelay = 0.9f;
    public float nextLevelDelay = 1.5f;

    [Header("Tutorial Settings")]
    public float tutorialCorrectDelay = 2.5f;

    [Header("Outro Settings")]
    public AudioSource outroAudio;
    public float outroExtraDelay = 0.1f;

    // --- Internal state ---
    int currentLevelIndex = 0;
    bool levelActive = false;
    bool levelCleared = false;
    int bulletsLeft = 0;
    List<BalloonUI> activeBalloons = new List<BalloonUI>();
    private RectTransform[] sentenceTargets;
    private Coroutine introCoroutine;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (mainCanvas == null)
        {
            Debug.LogError("[GameManagerUI] Main Canvas is not set. Assign it in the inspector.");
            return;
        }

        for (int i = 0; i < levelObjects.Length; i++)
        {
            if (levelObjects[i]) levelObjects[i].SetActive(false);
            if (i < questionImages.Length && questionImages[i]) questionImages[i].gameObject.SetActive(false);
        }

        sentenceTargets = new RectTransform[levelObjects.Length];
        StartLevel(0, true);
    }

    void Update()
    {
        if (gun != null)
        {
            gun.rotation = Quaternion.Slerp(
                gun.rotation,
                targetGunRot,
                Time.deltaTime * rotationSmoothSpeed
            );
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.S))
        {
            if (introCoroutine != null)
            {
                StopCoroutine(introCoroutine);
                introCoroutine = null;
                var intro = levelObjects[currentLevelIndex].transform.Find("LevelAudio")?.GetComponent<AudioSource>();
                if (intro != null) intro.Stop();
                if (currentLevelIndex == 0) StartCoroutine(RunTutorialAfterIntro(0f));
                else levelActive = true;
            }
        }
#endif
    }

    // ---------- LEVEL FLOW ----------
    public void StartLevel(int index, bool playIntro = true)
    {
        for (int i = 0; i < levelObjects.Length; i++)
        {
            if (levelObjects[i]) levelObjects[i].SetActive(i == index);
            if (i < questionImages.Length && questionImages[i]) questionImages[i].gameObject.SetActive(i == index);
        }

        currentLevelIndex = index;
        levelCleared = false;
        levelActive = false;
        bulletsLeft = bulletsPerLevel;

        if (index == 0) ToggleAmmoUI(false);
        else UpdateAmmoUI();

        if (droppedLettersRoot != null)
        {
            foreach (Transform child in droppedLettersRoot) Destroy(child.gameObject);
        }

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

        gun = level.Find("Gun") as RectTransform;
        if (gun != null)
        {
            firePoint = gun.Find("FirePoint") as RectTransform;
            var flash = gun.Find("Flash");
            if (flash != null) gunFlash = flash.GetComponent<Image>();
            if (gunFlash != null) gunFlash.enabled = false;

            gunOriginalRot = gun.rotation;
            targetGunRot = gunOriginalRot;
        }

        DetectLetterSlot(index);

        if (playIntro)
        {
            var intro = level.Find("LevelAudio")?.GetComponent<AudioSource>();
            if (intro != null && intro.clip != null)
            {
                intro.Stop(); intro.Play();
                if (index == 0) introCoroutine = StartCoroutine(RunTutorialAfterIntro(intro.clip.length));
                else introCoroutine = StartCoroutine(EnableInteractionAfterAudio(intro.clip.length));
            }
            else levelActive = true;
        }
        else levelActive = true;

        Debug.Log($"[GameManagerUI] Started Level {index}. BulletsLeft: {bulletsLeft}");
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
            if (slot != null) sentenceTargets[index] = slot as RectTransform;
        }
    }

    IEnumerator EnableInteractionAfterAudio(float delay)
    {
        yield return new WaitForSeconds(delay);
        levelActive = true;
    }

    IEnumerator RunTutorialAfterIntro(float delay)
    {
        yield return new WaitForSeconds(delay);

        BalloonUI wrongBalloon = activeBalloons.Find(b => b != null && !b.isCorrect);
        if (wrongBalloon != null)
        {
            StartCoroutine(PlayWrongAudioAfterDelay(wrongAudioDelay));
            if (wrongSignUI != null) StartCoroutine(FlashWrongSign());

            yield return StartCoroutine(RotateThenShoot(wrongBalloon));
            wrongBalloon.gameObject.SetActive(false);
        }

        yield return new WaitForSeconds(tutorialCorrectDelay);

        BalloonUI correctBalloon = activeBalloons.Find(b => b != null && b.isCorrect);
        if (correctBalloon != null)
        {
            yield return StartCoroutine(RotateThenShoot(correctBalloon));
            StartCoroutine(DropLetterThenNext(correctBalloon, autoAdvance: false));
        }

        yield return new WaitForSeconds(nextLevelDelay);
        StartLevel(1, true);
    }

    // ---------- SHOOTING ----------
    public void OnBalloonTapped(BalloonUI b)
    {
        if (!levelActive || levelCleared) return;
        if (bulletsLeft <= 0) return;

        bulletsLeft--;
        UpdateAmmoUI();
        StartCoroutine(ShootAndHandle(b));
    }

    IEnumerator ShootAndHandle(BalloonUI b)
    {
        yield return StartCoroutine(RotateThenShoot(b));

        if (b.isCorrect)
        {
            levelCleared = true;
            if (currentLevelIndex > 0) StartCoroutine(PlayCorrectAudioAfterDelay(correctAudioDelay));
            StartCoroutine(DropLetterThenNext(b, autoAdvance: currentLevelIndex != 0));
        }
        else
        {
            if (wrongClip != null) StartCoroutine(PlayWrongAudioAfterDelay(wrongAudioDelay));
            if (wrongSignUI != null) StartCoroutine(FlashWrongSign());

            b.gameObject.SetActive(false);

            if (bulletsLeft <= 0 && !levelCleared)
                StartCoroutine(RestartAfterDelay(restartDelay));
        }

        if (returnCoroutine != null) StopCoroutine(returnCoroutine);
        returnCoroutine = StartCoroutine(ReturnGunToOriginalAfterDelay(gunReturnDelay));
    }

    IEnumerator RotateThenShoot(BalloonUI target)
    {
        if (gun == null) yield break;

        Vector3 dir = target.transform.position - gun.position;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + gunRotationOffset;
        targetGunRot = Quaternion.Euler(0, 0, angle);

        while (Quaternion.Angle(gun.rotation, targetGunRot) > 1f)
            yield return null;

        if (gunFlash != null) StartCoroutine(FlashGun());
        if (sfxSource && shootClip) sfxSource.PlayOneShot(shootClip, gunVolume);
        yield return StartCoroutine(ShootBulletToTarget(target));
    }

    IEnumerator ShootBulletToTarget(BalloonUI targetBalloon)
    {
        if (bulletSprite == null) yield break;
        if (gun == null || firePoint == null) yield break;

        var bulletGO = new GameObject("Bullet(Clone)", typeof(RectTransform), typeof(Image));
        bulletGO.transform.SetParent(bulletRoot != null ? bulletRoot : mainCanvas.transform, false);
        var bulletRT = bulletGO.GetComponent<RectTransform>();
        var bulletImg = bulletGO.GetComponent<Image>();
        bulletImg.sprite = bulletSprite;
        bulletImg.preserveAspect = true;
        bulletImg.raycastTarget = false;
        bulletRT.sizeDelta = bulletSize;

        Vector3 startWorld = firePoint.position;
        Vector3 targetWorld = targetBalloon.transform.position;
        bulletRT.position = startWorld;

        bulletRT.rotation = gun.rotation;

        float dist = Vector3.Distance(startWorld, targetWorld);
        float duration = Mathf.Max(0.05f, dist / bulletSpeed);
        float elapsed = 0f;
        Vector3 prevPos = startWorld;

        // use target balloon center as "hit radius"
        float hitRadius = 40f; // adjust depending on balloon size

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Vector3 newPos = Vector3.Lerp(startWorld, targetWorld, t);
            bulletRT.position = newPos;

            if (rotateBulletToDirection)
            {
                Vector3 moveDir = (newPos - prevPos).normalized;
                if (moveDir.sqrMagnitude > 0.0001f)
                {
                    float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg + bulletRotationOffset;
                    bulletRT.rotation = Quaternion.Euler(0, 0, angle);
                }
            }
            prevPos = newPos;

            // --- check collision with balloon ---
            if (Vector3.Distance(newPos, targetBalloon.transform.position) <= hitRadius)
            {
                break; // hit → exit early
            }

            yield return null;
        }

        Destroy(bulletGO);
    }

    // ---------- GUN ROTATION ----------
    private void SmoothRotateGunTo(Vector3 targetWorldPos)
    {
        if (gun == null) return;
        Vector3 dir = targetWorldPos - gun.position;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + gunRotationOffset;
        targetGunRot = Quaternion.Euler(0, 0, angle);
    }

    IEnumerator ReturnGunToOriginalAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        targetGunRot = gunOriginalRot;
        while (Quaternion.Angle(gun.rotation, targetGunRot) > 0.5f)
            yield return null;
        returnCoroutine = null;
    }

    IEnumerator FlashGun()
    {
        if (gunFlash == null) yield break;
        gunFlash.enabled = true;
        yield return new WaitForSeconds(0.1f);
        gunFlash.enabled = false;
    }

    IEnumerator FlashWrongSign()
    {
        if (wrongSignUI != null)
        {
            wrongSignUI.SetActive(true);
            yield return new WaitForSeconds(0.6f);
            wrongSignUI.SetActive(false);
        }
    }

    IEnumerator PlayWrongAudioAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (sfxSource && wrongClip) sfxSource.PlayOneShot(wrongClip);
    }

    IEnumerator PlayCorrectAudioAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        var levelGO = levelObjects[currentLevelIndex];
        var correctAudio = levelGO.transform.Find("CorrectAudio")?.GetComponent<AudioSource>();
        if (correctAudio != null && correctAudio.clip != null)
        {
            correctAudio.Stop(); correctAudio.Play();
        }
    }

    IEnumerator DropLetterThenNext(BalloonUI sourceBalloon, bool autoAdvance = true)
    {
        if (droppedLettersRoot == null) droppedLettersRoot = mainCanvas.transform as RectTransform;
        Image letterImg = sourceBalloon.transform.Find("Letter")?.GetComponent<Image>();

        if (letterImg != null && letterImg.sprite != null)
        {
            RectTransform target = sentenceTargets[currentLevelIndex];
            if (target != null)
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

                Vector2 targetPos = target.anchoredPosition;
                float t = 0f, dur = 0.6f;
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
        yield return new WaitForSeconds(nextLevelDelay);

        if (autoAdvance)
        {
            int next = currentLevelIndex + 1;
            if (next < levelObjects.Length) StartLevel(next, true);
            else GameFinished();
        }
    }

    IEnumerator RestartAfterDelay(float t)
    {
        yield return new WaitForSeconds(t);
        if (!levelCleared) StartLevel(currentLevelIndex, false);
    }

    void UpdateAmmoUI()
    {
        if (ammoText == null) return;
        if (currentLevelIndex == 0) { ToggleAmmoUI(false); return; }
        ToggleAmmoUI(true);
        ammoText.text = "x" + bulletsLeft;
    }

    private void ToggleAmmoUI(bool visible)
    {
        if (ammoIcon != null) ammoIcon.gameObject.SetActive(visible);
        if (ammoText != null) ammoText.gameObject.SetActive(visible);
    }

    void GameFinished()
    {
        levelActive = false;
        Debug.Log("Game finished!");
        StartCoroutine(PlayOutroAfterCorrect());
    }

    IEnumerator PlayOutroAfterCorrect()
    {
        var levelGO = levelObjects[currentLevelIndex];
        var correctAudio = levelGO.transform.Find("CorrectAudio")?.GetComponent<AudioSource>();
        float waitTime = 0f;

        if (correctAudio != null && correctAudio.clip != null)
        {
            if (correctAudio.isPlaying) waitTime = correctAudio.clip.length - correctAudio.time;
            else waitTime = correctAudio.clip.length;
        }

        yield return new WaitForSeconds(waitTime + outroExtraDelay);
        if (outroAudio != null && outroAudio.clip != null)
        {
            outroAudio.Stop(); outroAudio.Play();
        }
    }
}
