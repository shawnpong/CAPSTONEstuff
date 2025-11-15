using UnityEngine;
using System.Collections;

[System.Serializable]
public class FoodItem
{
    public string name;
    public GameObject prefab;
    public int hungerRestore;
}

public class SteakThrower : MonoBehaviour
{
    [Header("Food Options")]
    public FoodItem[] foodItems;
    private int selectedFoodIndex = 0;

    [Header("Scene References")]
    [SerializeField] private Camera arCamera;
    [SerializeField] private Transform mouthTarget;
    [SerializeField] private CorgiAnimation corgiAnim;

    [Header("Spawn (Screen-space)")]
    [Range(0f, 1f)] public float screenX = 0.92f;
    [Range(0f, 1f)] public float screenY = 0.08f;
    [SerializeField] private float spawnDistance = 0.35f;

    [Header("Throw Settings")]
    [SerializeField] private float flightDuration = 0.8f;
    [SerializeField] private float arcHeight = 0.15f;
    [SerializeField] private bool rotateToVelocity = true;

    [Header("Impact")]
    [SerializeField] private float lingerSeconds = 3.0f;
    [SerializeField] private bool stickToMouth = true;

    private bool isThrowing = false;
    private GameObject activeFoodObj;

    public void SelectFood(int index)
    {
        if (index < 0 || index >= foodItems.Length)
        {
            Debug.LogWarning("Invalid food index");
            return;
        }

        selectedFoodIndex = index;
        Debug.Log($"Selected Food: {foodItems[index].name}");
    }

    public void FeedWithThrow()
    {
        if (isThrowing) return;

        if (!arCamera || !mouthTarget || !corgiAnim)
        {
            Debug.LogWarning("[SteakThrower] Missing references!");
            return;
        }

        StartCoroutine(ThrowRoutine());
    }

    private IEnumerator ThrowRoutine()
    {
        isThrowing = true;

        FoodItem food = foodItems[selectedFoodIndex];

        if (food.prefab == null)
        {
            Debug.LogWarning("Selected food prefab missing!");
            isThrowing = false;
            yield break;
        }

        // Spawn point
        Vector3 spawn = arCamera.ScreenToWorldPoint(
            new Vector3(Screen.width * screenX, Screen.height * screenY, spawnDistance)
        );

        activeFoodObj = Instantiate(food.prefab, spawn, Quaternion.identity);
        Transform tf = activeFoodObj.transform;

        tf.rotation = Quaternion.Euler(-50f, 0f, 0f);

        Vector3 p0 = spawn;
        Vector3 p2 = mouthTarget.position;
        Vector3 p1 = (p0 + p2) * 0.5f + Vector3.up * arcHeight;

        float end = Time.realtimeSinceStartup + flightDuration;
        Vector3 prev = tf.position;

        // Bezier throw
        while (Time.realtimeSinceStartup < end)
        {
            float u = 1f - ((end - Time.realtimeSinceStartup) / flightDuration);
            u = Mathf.Clamp01(u);

            float t = u * u * (3f - 2f * u);

            Vector3 pos = Bezier(p0, p1, p2, t);
            tf.position = pos;

            if (rotateToVelocity)
            {
                Vector3 vel = pos - prev;
                if (vel.sqrMagnitude > 1e-6f)
                    tf.rotation = Quaternion.LookRotation(vel.normalized, Vector3.up);

                prev = pos;
            }

            yield return null;
        }

        // Move to mouth
        tf.position = mouthTarget.position;
        if (stickToMouth)
            tf.SetParent(mouthTarget, true);

        // Eat + restore hunger
        corgiAnim.PlayEatOnce();
        corgiAnim.RestoreHungerFromFood(food.hungerRestore);

        Debug.Log($"{food.name} restored {food.hungerRestore} hunger!");

        // Linger before disappearing
        yield return new WaitForSeconds(lingerSeconds);

        if (activeFoodObj)
            Destroy(activeFoodObj);

        activeFoodObj = null;
        isThrowing = false;
    }

    private static Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float it = 1f - t;
        return it * it * p0 + 2 * it * t * p1 + t * t * p2;
    }
}
