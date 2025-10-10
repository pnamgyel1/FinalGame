using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages UI-based balloon shooting gameplay and level flow
/// </summary>
public class GameManagerUI : MonoBehaviour
{
    public static GameManagerUI Instance { get; private set; }

    #region Inspector Fields

    [Header("Gun Settings")]
    public float gunRotationOffset = -90f;
    public float rotationSmoothSpeed = 8f;
    public float gunReturnDelay = 0.15f;

    [Header("Scene Roots")]
    public Canvas mainCanvas;
    public RectTransform levelsRoot;
    public RectTransform questionRoot;
    public RectTransform droppedLettersRoot;
    public RectTransform bulletRoot;

    [Header("Levels")]
    public GameObject[] levelObjects;
    public Image[] questionImages;

    [Header("Gun")]
    public RectTransform gun;
    public RectTransform firePoint;

    [Header("Bullet (UI)")]
    public Sprite bulletSprite;
    public Vector2 bulletSize = new Vector2(40, 80);
    public float bulletSpeed = 16f;
    public bool rotateBulletToDirection = true;
    public float bulletRotationOffset = 90f;

    [Header("HUD")]
    public Image ammoIcon;
    public TMP_Text ammoText;

    [Header("Audio")]
    public AudioSource sfxSource;
    public AudioClip shootClip;
    [Range(0f, 1f)] public float gunVolume = 0.5f;
    public AudioClip wrongClip;
    public float wrongAudioDelay = 0.15f;

    [Header("Extra SFX")]
    public AudioClip balloonPopClip;
    [Range(0f, 1f)] public float balloonPopVolume = 0.7f;
    public AudioClip tutorialCorrectClip;
    [Range(0f, 1f)] public float tutorialCorrectVolume = 0.8f;

    [Header("Out-of-ammo / Restart SFX")]
    public AudioClip outOfAmmoClip;
    [Range(0f, 1f)] public float outOfAmmoVolume = 0.8f;

    [Header("Gameplay Settings")]
    public int bulletsPerLevel = 3;
    public float restartDelay = 0.9f;
    public float nextLevelDelay = 1.5f;

    [Header("Tutorial Settings")]
    public float tutorialCorrectDelay = 2.5f;

    [Header("Outro Settings")]
    public AudioSource outroAudio;
    public float outroExtraDelay = 0.1f;

    [Header("Options")]
    [Tooltip("Play outro audio after the final level.")]
    public bool playOutroAfterLastLevel = true;

    #endregion

    #region Private Fields

    int currentLevelIndex = 0;
    bool levelActive = false;
    bool levelCleared = false;
    int bulletsLeft = 0;
    bool isProcessingShot = false;
    bool isRestarting = false;

    List<BalloonUI> activeBalloons = new List<BalloonUI>();
    RectTransform[] sentenceTargets;
    AudioSource currentLevelCorrectAudioSource;
    int remainingCorrectThisLevel = 0;

    Quaternion gunOriginalRot;
    Quaternion targetGunRot;
    Coroutine returnCoroutine;
    Coroutine rotationCoroutine;
    Coroutine introCoroutine;

    List<Coroutine> runningCoroutines = new List<Coroutine>();

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (mainCanvas == null)
        {
            Debug.LogError("[GameManagerUI] Main Canvas missing!");
            return;
        }

        for (int i = 0; i < levelObjects.Length; i++)
        {
            if (levelObjects[i]) levelObjects[i].SetActive(false);
            if (i < questionImages.Length && questionImages[i]) questionImages[i].gameObject.SetActive(false);
        }

        sentenceTargets = new RectTransform[Mathf.Max(1, levelObjects.Length)];
        StartLevel(0, true);
    }

    void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Skip intro audio with Space/S
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.S))
        {
            if (introCoroutine != null)
            {
                StopCoroutine(introCoroutine);
                introCoroutine = null;
                var intro = levelObjects[currentLevelIndex].transform.Find("LevelAudio")?.GetComponent<AudioSource>();
                if (intro) intro.Stop();
                if (currentLevelIndex == 0) StartCoroutine(RunTutorialAfterIntro(0f));
                else levelActive = true;
            }
        }
#endif
    }

    #endregion

    #region Level Management

    public void StartLevel(int index, bool playIntro = true)
    {
        StopAllManagedCoroutines();
        StopRotationCoroutine();
        isProcessingShot = false;
        isRestarting = false;

        if (returnCoroutine != null) { StopCoroutine(returnCoroutine); returnCoroutine = null; }

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

        if (droppedLettersRoot)
            foreach (Transform child in droppedLettersRoot) Destroy(child.gameObject);

        SetupBalloons(index);
        SetupGun(index);
        DetectLetterSlot(index);

        if (playIntro)
        {
            var intro = levelObjects[index].transform.Find("LevelAudio")?.GetComponent<AudioSource>();
            if (intro && intro.clip)
            {
                intro.Stop(); intro.Play();
                introCoroutine = (index == 0)
                    ? StartCoroutine(RunTutorialAfterIntro(intro.clip.length))
                    : StartCoroutine(EnableInteractionAfterAudio(intro.clip.length));
            }
            else levelActive = true;
        }
        else levelActive = true;
    }

    void SetupBalloons(int index)
    {
        activeBalloons.Clear();
        var level = levelObjects[index].transform;
        var balloonsRoot = level.Find("Balloons");
        if (!balloonsRoot) return;

        foreach (Transform child in balloonsRoot)
        {
            child.gameObject.SetActive(true);
            child.localScale = Vector3.one;

            var b = child.GetComponent<BalloonUI>();
            if (b != null)
            {
                b.Register();
                activeBalloons.Add(b);
            }
        }

        remainingCorrectThisLevel = 0;
        foreach (var b in activeBalloons)
            if (b.isCorrect) remainingCorrectThisLevel++;

        currentLevelCorrectAudioSource = level.Find("CorrectAudio")?.GetComponent<AudioSource>();
    }

    void SetupGun(int index)
    {
        var level = levelObjects[index].transform;
        gun = level.Find("Gun") as RectTransform;
        if (!gun) return;

        firePoint = gun.Find("FirePoint") as RectTransform;
        gunOriginalRot = gun.rotation;
        targetGunRot = gunOriginalRot;
        gun.rotation = gunOriginalRot;
    }

    void DetectLetterSlot(int index)
    {
        if (index >= questionImages.Length) return;
        var q = questionImages[index];
        if (!q) return;
        var sentence = q.transform.Find("Sentence");
        if (!sentence) return;
        var slot = sentence.Find("LetterSlot");
        if (slot) sentenceTargets[index] = slot as RectTransform;
    }

    void ResetLevelStateBeforeRestart()
    {
        StopRotationCoroutine();
        if (returnCoroutine != null) { StopCoroutine(returnCoroutine); returnCoroutine = null; }
        if (gun) gun.rotation = targetGunRot = gunOriginalRot;

        var balloonsRoot = levelObjects[currentLevelIndex].transform.Find("Balloons");
        if (balloonsRoot)
        {
            foreach (Transform child in balloonsRoot)
            {
                child.localScale = Vector3.one;
                child.gameObject.SetActive(true);
                child.GetComponent<BalloonUI>()?.Register();
            }
        }

        if (droppedLettersRoot)
            foreach (Transform child in droppedLettersRoot) Destroy(child.gameObject);

        isProcessingShot = false;
        isRestarting = false;
    }

    IEnumerator RestartAfterDelay(float t)
    {
        yield return new WaitForSeconds(t);
        if (!levelCleared) StartLevel(currentLevelIndex, false);
    }

    IEnumerator EnableInteractionAfterAudio(float delay)
    {
        yield return new WaitForSeconds(delay);
        levelActive = true;
    }

    void GameFinished()
    {
        levelActive = false;
        StartCoroutine(PlayOutroAfterCorrect());
    }

    #endregion

    #region Tutorial

    IEnumerator RunTutorialAfterIntro(float delay)
    {
        yield return new WaitForSeconds(delay);

        BalloonUI wrong = activeBalloons.Find(b => !b.isCorrect);
        if (wrong)
        {
            yield return RotateThenShoot(wrong);
            if (sfxSource && balloonPopClip) sfxSource.PlayOneShot(balloonPopClip, balloonPopVolume);
            wrong.gameObject.SetActive(false);
            if (wrongClip) StartCoroutine(PlayWrongAudioAfterDelay(wrongAudioDelay));
        }

        yield return new WaitForSeconds(tutorialCorrectDelay);

        BalloonUI correct = activeBalloons.Find(b => b.isCorrect);
        if (correct)
        {
            yield return RotateThenShoot(correct);
            if (sfxSource && balloonPopClip) sfxSource.PlayOneShot(balloonPopClip, balloonPopVolume);
            correct.gameObject.SetActive(false);
            if (sfxSource && tutorialCorrectClip) sfxSource.PlayOneShot(tutorialCorrectClip, tutorialCorrectVolume);
            remainingCorrectThisLevel--;
            StartCoroutine(DropLetterThenNext(correct, false));
        }

        yield return new WaitForSeconds(nextLevelDelay);
        StartLevel(1, true);
    }

    #endregion

    #region Input Handling

    public void OnBalloonTapped(BalloonUI b)
    {
        if (!levelActive || levelCleared || bulletsLeft <= 0 || isProcessingShot || isRestarting) return;
        if (returnCoroutine != null) { StopCoroutine(returnCoroutine); returnCoroutine = null; }

        isProcessingShot = true;
        bulletsLeft--;
        UpdateAmmoUI();
        StartManagedCoroutine(ShootAndHandle(b));
    }

    #endregion

    #region Shooting & Combat

    IEnumerator ShootAndHandle(BalloonUI b)
    {
        yield return RotateThenShoot(b);
        if (sfxSource && balloonPopClip) sfxSource.PlayOneShot(balloonPopClip, balloonPopVolume);
        if (currentLevelIndex > 0 && b.isActiveAndEnabled) yield return PopBalloonAnimation(b);

        if (b.isCorrect)
        {
            remainingCorrectThisLevel--;
            if (currentLevelCorrectAudioSource && currentLevelCorrectAudioSource.clip)
            {
                currentLevelCorrectAudioSource.Stop();
                currentLevelCorrectAudioSource.Play();
            }
            yield return DropLetterThenNext(b, currentLevelIndex != 0);
            if (remainingCorrectThisLevel <= 0) { levelCleared = true; levelActive = false; }
        }
        else
        {
            if (wrongClip) StartCoroutine(PlayWrongAudioAfterDelay(wrongAudioDelay));
            b.gameObject.SetActive(false);
            if (bulletsLeft <= 0 && !levelCleared)
            {
                if (outOfAmmoClip && sfxSource)
                {
                    isRestarting = true; levelActive = false;
                    sfxSource.PlayOneShot(outOfAmmoClip, outOfAmmoVolume);
                    yield return new WaitForSeconds(outOfAmmoClip.length);
                    ResetLevelStateBeforeRestart();
                    StartLevel(currentLevelIndex, false);
                    isRestarting = false;
                    isProcessingShot = false;
                    yield break;
                }
                else StartCoroutine(RestartAfterDelay(restartDelay));
            }
        }

        if (returnCoroutine != null) StopCoroutine(returnCoroutine);
        returnCoroutine = StartCoroutine(ReturnGunToOriginalAfterDelay(gunReturnDelay));
        yield return new WaitForSeconds(0.05f);
        isProcessingShot = false;
    }

    IEnumerator RotateThenShoot(BalloonUI target)
    {
        if (!gun || !target) yield break;
        if (returnCoroutine != null) { StopCoroutine(returnCoroutine); returnCoroutine = null; }

        Vector3 dir = target.transform.position - gun.position;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + gunRotationOffset;
        targetGunRot = Quaternion.Euler(0, 0, angle);

        StopRotationCoroutine();
        yield return RotateTo(targetGunRot);

        if (sfxSource && shootClip) sfxSource.PlayOneShot(shootClip, gunVolume);
        yield return ShootBulletToTarget(target);
    }

    IEnumerator ShootBulletToTarget(BalloonUI targetBalloon)
    {
        if (!bulletSprite || !mainCanvas || !gun || !firePoint || !targetBalloon) yield break;

        RectTransform canvasRT = mainCanvas.GetComponent<RectTransform>();
        Camera cam = mainCanvas.worldCamera;

        Vector2 startLocal, targetLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT,
            RectTransformUtility.WorldToScreenPoint(cam, firePoint.position), cam, out startLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT,
            RectTransformUtility.WorldToScreenPoint(cam, targetBalloon.transform.position), cam, out targetLocal);

        var bullet = new GameObject("Bullet(Clone)", typeof(RectTransform), typeof(Image));
        bullet.transform.SetParent(bulletRoot ? bulletRoot : mainCanvas.transform, false);

        var rt = bullet.GetComponent<RectTransform>();
        var img = bullet.GetComponent<Image>();
        img.sprite = bulletSprite; img.preserveAspect = true; img.raycastTarget = false;
        rt.sizeDelta = bulletSize; rt.localScale = Vector3.one;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.anchoredPosition = startLocal;

        if (rotateBulletToDirection)
        {
            Vector2 dir = (targetLocal - startLocal).normalized;
            if (dir.sqrMagnitude > 0.001f)
            {
                float a = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + bulletRotationOffset;
                rt.localRotation = Quaternion.Euler(0, 0, a);
            }
        }

        float dist = Vector2.Distance(startLocal, targetLocal);
        float dur = Mathf.Max(0.05f, dist / Mathf.Max(0.001f, bulletSpeed));
        float elapsed = 0f;

        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            rt.anchoredPosition = Vector2.Lerp(startLocal, targetLocal, elapsed / dur);
            yield return null;
        }
        Destroy(bullet);
    }

    #endregion

    #region Gun Rotation

    IEnumerator RotateTo(Quaternion dest)
    {
        rotationCoroutine = StartCoroutine(RotateToInner(dest));
        yield return rotationCoroutine;
    }

    IEnumerator RotateToInner(Quaternion dest)
    {
        while (gun && Quaternion.Angle(gun.rotation, dest) > 0.5f)
        {
            gun.rotation = Quaternion.Slerp(gun.rotation, dest, Time.deltaTime * rotationSmoothSpeed);
            yield return null;
        }
        if (gun) gun.rotation = dest;
        rotationCoroutine = null;
    }

    IEnumerator ReturnGunToOriginalAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!gun) yield break;
        StopRotationCoroutine();
        yield return RotateTo(gunOriginalRot);
        returnCoroutine = null;
    }

    void StopRotationCoroutine()
    {
        if (rotationCoroutine != null) { StopCoroutine(rotationCoroutine); rotationCoroutine = null; }
    }

    #endregion

    #region Animations

    IEnumerator PopBalloonAnimation(BalloonUI balloon)
    {
        RectTransform rt = balloon.GetComponent<RectTransform>();
        if (!rt) yield break;

        float t = 0f, dur = 0.18f;
        Vector3 start = Vector3.one;
        while (t < dur)
        {
            t += Time.deltaTime;
            rt.localScale = start * Mathf.Lerp(1f, 1.25f, t / dur);
            yield return null;
        }
        rt.localScale = start;
        balloon.gameObject.SetActive(false);
    }

    IEnumerator DropLetterThenNext(BalloonUI sourceBalloon, bool autoAdvance = true)
    {
        if (!droppedLettersRoot) droppedLettersRoot = mainCanvas.transform as RectTransform;
        Image letterImg = sourceBalloon.transform.Find("Letter")?.GetComponent<Image>();
        if (letterImg && letterImg.sprite)
        {
            RectTransform target = sentenceTargets[currentLevelIndex];
            if (target)
            {
                var drop = new GameObject("DroppedLetter", typeof(RectTransform), typeof(Image));
                drop.transform.SetParent(droppedLettersRoot, false);
                var rt = drop.GetComponent<RectTransform>();
                var img = drop.GetComponent<Image>();
                img.sprite = letterImg.sprite;
                img.preserveAspect = true;
                rt.sizeDelta = target.sizeDelta;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    mainCanvas.GetComponent<RectTransform>(),
                    RectTransformUtility.WorldToScreenPoint(mainCanvas.worldCamera, letterImg.rectTransform.position),
                    mainCanvas.worldCamera, out var startLocal);

                rt.anchoredPosition = startLocal;
                Vector2 targetPos = target.anchoredPosition;

                float t = 0f, dur = 0.4f;
                while (t < dur)
                {
                    t += Time.deltaTime;
                    rt.anchoredPosition = Vector2.Lerp(startLocal, targetPos, Mathf.SmoothStep(0f, 1f, t / dur));
                    yield return null;
                }
                rt.SetParent(target, false);
                rt.anchoredPosition = Vector2.zero;
            }
        }

        sourceBalloon.gameObject.SetActive(false);
        yield return new WaitForSeconds(nextLevelDelay);

        if (autoAdvance && remainingCorrectThisLevel <= 0)
        {
            int next = currentLevelIndex + 1;
            if (next < levelObjects.Length) StartLevel(next, true);
            else if (playOutroAfterLastLevel) GameFinished();
        }
    }

    #endregion

    #region Audio

    IEnumerator PlayWrongAudioAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (sfxSource && wrongClip) sfxSource.PlayOneShot(wrongClip);
    }

    IEnumerator PlayOutroAfterCorrect()
    {
        var correctAudio = levelObjects[currentLevelIndex].transform.Find("CorrectAudio")?.GetComponent<AudioSource>();
        float wait = 0f;
        if (correctAudio && correctAudio.clip)
            wait = correctAudio.isPlaying ? correctAudio.clip.length - correctAudio.time : correctAudio.clip.length;
        yield return new WaitForSeconds(wait + outroExtraDelay);
        if (outroAudio && outroAudio.clip) { outroAudio.Stop(); outroAudio.Play(); }
    }

    #endregion

    #region UI

    void UpdateAmmoUI()
    {
        if (ammoText == null) return;
        if (currentLevelIndex == 0) { ToggleAmmoUI(false); return; }
        ToggleAmmoUI(true);
        ammoText.text = "x" + bulletsLeft;
    }

    void ToggleAmmoUI(bool visible)
    {
        if (ammoIcon) ammoIcon.gameObject.SetActive(visible);
        if (ammoText) ammoText.gameObject.SetActive(visible);
    }

    #endregion

    #region Coroutine Management

    Coroutine StartManagedCoroutine(IEnumerator e)
    {
        var c = StartCoroutine(e);
        runningCoroutines.Add(c);
        return c;
    }

    void StopAllManagedCoroutines()
    {
        foreach (var c in runningCoroutines) if (c != null) StopCoroutine(c);
        runningCoroutines.Clear();
        if (introCoroutine != null) { StopCoroutine(introCoroutine); introCoroutine = null; }
        if (returnCoroutine != null) { StopCoroutine(returnCoroutine); returnCoroutine = null; }
        StopRotationCoroutine();
    }

    #endregion
}