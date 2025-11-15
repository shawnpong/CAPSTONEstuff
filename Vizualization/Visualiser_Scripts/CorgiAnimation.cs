using UnityEngine;
using System.Collections;

public class CorgiAnimation : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator corgiAnimator;

    [Header("Feed action")]
    [SerializeField] private string eatTrigger = "TrEat";
    [SerializeField] private int feedAmount = 20;
    [SerializeField] private float feedDelay = 5.0f;
    private Coroutine pendingFeed;
    private float feedDeadlineRealtime = -1f;

    [Header("Pet action")]
    [SerializeField] private string petTrigger = "TrPet";
    [SerializeField] private int petAmount = 10;
    [SerializeField] private float petDelay = 1.0f;
    private Coroutine pendingPet;
    private float petDeadlineRealtime = -1f;

    [Header("Angry action")]
    [SerializeField] private string angryTrigger = "TrAngry";

    [Header("Running action")]
    [SerializeField] private string runTrigger = "TrRun";

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip barkClip;

    [Header("Game logic")]
    [SerializeField] private Corgi corgiScript;

    void Awake()
    {
        if (!corgiAnimator)
            corgiAnimator = GetComponentInChildren<Animator>(true);

        if (!corgiScript)
            corgiScript = FindObjectOfType<Corgi>(true);

        if (!audioSource)
            audioSource = GetComponent<AudioSource>();
    }

    // ---------- FEED ----------
    public void PlayEatOnce()
    {
        if (!corgiAnimator) { Debug.LogWarning("[CorgiAnimation] No Animator."); return; }

        corgiAnimator.ResetTrigger(eatTrigger);
        corgiAnimator.SetTrigger(eatTrigger);

        feedDeadlineRealtime = Time.realtimeSinceStartup + feedDelay;
        if (pendingFeed == null)
            pendingFeed = StartCoroutine(WaitThenFeed());
    }

    private IEnumerator WaitThenFeed()
    {
        while (Time.realtimeSinceStartup < feedDeadlineRealtime)
            yield return null;

        if (corgiScript)
            corgiScript.RestoreHunger(feedAmount);

        pendingFeed = null;
    }

    // ---------- PET ----------
    public void PlayPetOnce()
    {
        if (!corgiAnimator) { Debug.LogWarning("[CorgiAnimation] No Animator."); return; }

        corgiAnimator.ResetTrigger(petTrigger);
        corgiAnimator.SetTrigger(petTrigger);

        petDeadlineRealtime = Time.realtimeSinceStartup + petDelay;
        if (pendingPet == null)
            pendingPet = StartCoroutine(WaitThenPet());
    }

    private IEnumerator WaitThenPet()
    {
        while (Time.realtimeSinceStartup < petDeadlineRealtime)
            yield return null;

        if (corgiScript)
            corgiScript.RestoreHappiness(petAmount);

        pendingPet = null;
    }

    // ---------- RUNNING ----------
    public void StartRunning()
    {
        if (!corgiAnimator) return;
        corgiAnimator.SetTrigger(runTrigger);
    }

    // ---------- ANGRY ----------
    public void PlayAngryOnce()
    {
        if (!corgiAnimator) return;

        corgiAnimator.ResetTrigger(angryTrigger);
        corgiAnimator.SetTrigger(angryTrigger);
    }

    // ---------- BARK with DELAY ----------
    public void PlayDelayedAngry()
    {
        StartCoroutine(DelayedAngryRoutine());
    }

    private IEnumerator DelayedAngryRoutine()
    {
        PlayAngryOnce();
        yield return new WaitForSeconds(0.8f);
        PlayBark();
    }

    public void PlayBark()
    {
        if (audioSource && barkClip)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(barkClip);
        }
    }

    // ---------- FOR STEAKTHROWER ----------
    public void RestoreHungerFromFood(int amount)
    {
        if (corgiScript)
            corgiScript.RestoreHunger(amount);
    }
}
