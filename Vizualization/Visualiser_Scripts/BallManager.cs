using UnityEngine;
using TMPro;

public class BallManager : MonoBehaviour
{
    public static BallManager Instance;

    [Header("Ball setup")]
    public GameObject ballPrefab;
    public Transform spawnArea; // optional — where to spawn near
    public Vector3 spawnOffset = new Vector3(0, 0.1f, 0.3f);

    [Header("UI")]
    public TextMeshProUGUI ballCollectedText;
    public float messageDuration = 2f;

    private GameObject activeBall;

    void Awake()
    {
        Instance = this;
        if (ballCollectedText)
            ballCollectedText.gameObject.SetActive(false);
    }

    public void SpawnBall()
    {
        if (!ballPrefab)
        {
            Debug.LogWarning("[BallManager] No ball prefab assigned!");
            return;
        }

        if (activeBall) Destroy(activeBall);

        Vector3 pos = Vector3.zero;

        if (spawnArea)
            pos = spawnArea.position + spawnArea.forward * spawnOffset.z + spawnArea.up * spawnOffset.y;
        else
            pos = new Vector3(0, 0, 0.5f);

        activeBall = Instantiate(ballPrefab, pos, Quaternion.identity);
        Debug.Log("Ball spawned!");
    }

    public void BallCollected()
    {
        if (ballCollectedText)
            StartCoroutine(ShowCollectedMessage());
    }

    private System.Collections.IEnumerator ShowCollectedMessage()
    {
        ballCollectedText.text = "Food Collected!";
        ballCollectedText.gameObject.SetActive(true);

        yield return new WaitForSeconds(messageDuration);

        ballCollectedText.gameObject.SetActive(false);
    }
}
