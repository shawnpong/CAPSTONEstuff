using UnityEngine;
using UnityEngine.UI;

public class Corgi : MonoBehaviour
{
    [Header("Stats")]
    public int maxHappiness = 100;
    public int currentHappiness;

    public int maxHunger = 100;
    public int currentHunger;

    [Header("UI References")]
    public HappinessBar happinessBar;
    public HungerBar hungerBar;

    [Header("Decay Settings")]
    [SerializeField] private float hungerInterval = 15f;
    [SerializeField] private float happinessInterval = 15f;
    [SerializeField] private int hungerDecay = 10;
    [SerializeField] private int happinessDecay = 10;

    [Header("Animation")]
    [SerializeField] private CorgiAnimation corgiAnim;

    private float hungerTimer = 0f;
    private float happinessTimer = 0f;

    private int nextHungerMilestone = 20;
    private int nextHappinessMilestone = 20;

    void Start()
    {
        currentHappiness = maxHappiness;
        happinessBar.SetMaxHappiness(maxHappiness);
        happinessBar.SetHappiness(currentHappiness);

        currentHunger = maxHunger;
        hungerBar.SetMaxHunger(maxHunger);
        hungerBar.SetHunger(currentHunger);

        if (!corgiAnim) corgiAnim = FindObjectOfType<CorgiAnimation>(true);
    }

    void Update()
    {
        hungerTimer += Time.deltaTime;
        happinessTimer += Time.deltaTime;

        if (hungerTimer >= hungerInterval)
        {
            ReduceHunger(hungerDecay);
            hungerTimer = 0f;
        }

        if (happinessTimer >= happinessInterval)
        {
            ReduceHappiness(happinessDecay);
            happinessTimer = 0f;
        }
    }

    void ReduceHappiness(int amount)
    {
        int prev = currentHappiness;
        currentHappiness = Mathf.Clamp(currentHappiness - amount, 0, maxHappiness);
        happinessBar.SetHappiness(currentHappiness);

        CheckHappinessMilestone(prev, currentHappiness);
    }

    void ReduceHunger(int amount)
    {
        int prev = currentHunger;
        currentHunger = Mathf.Clamp(currentHunger - amount, 0, maxHunger);
        hungerBar.SetHunger(currentHunger);

        CheckHungerMilestone(prev, currentHunger);
    }

    public void RestoreHunger(int amount)
    {
        currentHunger = Mathf.Clamp(currentHunger + amount, 0, maxHunger);
        hungerBar.SetHunger(currentHunger);

        hungerTimer = 0f;

        
        if (currentHunger > 20) nextHungerMilestone = 20;
    }

    public void RestoreHappiness(int amount)
    {
        currentHappiness = Mathf.Clamp(currentHappiness + amount, 0, maxHappiness);
        happinessBar.SetHappiness(currentHappiness);

        happinessTimer = 0f;

        if (currentHappiness > 20) nextHappinessMilestone = 20;
    }

    private void CheckHungerMilestone(int prev, int now)
    {
        if (corgiAnim == null) return;
        if (now <= nextHungerMilestone && nextHungerMilestone >= 0 && prev > nextHungerMilestone)
        {
            corgiAnim.PlayDelayedAngry();
            nextHungerMilestone -= 10;
        }
    }

    private void CheckHappinessMilestone(int prev, int now)
    {
        if (corgiAnim == null) return;
        if (now <= nextHappinessMilestone && nextHappinessMilestone >= 0 && prev > nextHappinessMilestone)
        {
            corgiAnim.PlayDelayedAngry();
            nextHappinessMilestone -= 10;
        }
    }
}
