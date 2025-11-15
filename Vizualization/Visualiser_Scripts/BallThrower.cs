using UnityEngine;
using TMPro;
using System.Collections;

public class BallThrower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera arCamera; 
    [SerializeField] private Transform corgiRoot;
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private TextMeshProUGUI collectedText;

    [Header("Throw Settings")]
    [SerializeField] private float spawnDistance = 0.3f;    // distance in front of camera
    [SerializeField] private float screenX = 0.15f;         // X position (bottom-left corner)
    [SerializeField] private float screenY = 0.15f;
    [SerializeField] private float throwDuration = 1.8f;    // flight time
    [SerializeField] private float arcHeight = 0.4f;        // arc height
    [SerializeField] private float landingDistance = 0.8f;  // how far in front of corgi it lands
    [SerializeField] private AnimationCurve easingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private GameObject activeBall;
    private bool hasBallActive = false;
    private Collider[] corgiColliders;

    public bool HasActiveBall => hasBallActive;

    private void Start()
    {
        if (collectedText)
            collectedText.gameObject.SetActive(false);

        if (corgiRoot)
            corgiColliders = corgiRoot.GetComponentsInChildren<Collider>();
    }

    public void ThrowBall()
    {
        if (hasBallActive)
        {
            Debug.Log("Ignored — Ball already active.");
            return;
        }

        if (!arCamera || !corgiRoot || !ballPrefab)
        {
            Debug.LogWarning("[BallThrower] Missing references!");
            return;
        }

        StartCoroutine(ThrowRoutine());
    }

    private IEnumerator ThrowRoutine()
    {
        hasBallActive = true;

        // Start point — bottom-left of the screen
        Vector3 spawn = arCamera.ScreenToWorldPoint(
            new Vector3(Screen.width * screenX, Screen.height * screenY, spawnDistance)
        );

        // End point — 0.5 m in front of the Corgi in world space
        Vector3 endPos = corgiRoot.TransformPoint(new Vector3(0f, 0.05f, landingDistance));

        // Instantiate ball
        activeBall = Instantiate(ballPrefab, spawn, Quaternion.identity);
        activeBall.transform.SetParent(null, true); // detach — stays in world space

        Rigidbody rb = activeBall.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Prevent collision with corgi during flight
        Collider ballCollider = activeBall.GetComponent<Collider>();
        if (ballCollider && corgiColliders != null)
        {
            foreach (var c in corgiColliders)
                Physics.IgnoreCollision(ballCollider, c, true);
        }

        // Add collectible script
        var collectible = activeBall.GetComponent<BallCollectible>();
        if (collectible == null)
            collectible = activeBall.AddComponent<BallCollectible>();
        collectible.Init(this);

        // Smooth throw arc
        float elapsed = 0f;
        while (elapsed < throwDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / throwDuration);
            float easedT = easingCurve.Evaluate(t);

            Vector3 pos = Vector3.Lerp(spawn, endPos, easedT);
            pos.y += Mathf.Sin(easedT * Mathf.PI) * arcHeight; // arc effect

            if (activeBall)
                activeBall.transform.position = pos;

            yield return null;
        }

        // Lock the ball at landing position
        if (activeBall)
        {
            activeBall.transform.position = endPos;

            if (rb)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // Re-enable collision after landing
            if (ballCollider && corgiColliders != null)
            {
                foreach (var c in corgiColliders)
                    Physics.IgnoreCollision(ballCollider, c, false);
            }

            Debug.Log("Ball landed and stays fixed in world space.");
        }
    }

    public void OnBallCollected(GameObject ball)
    {
        if (ball == activeBall)
        {
            Destroy(activeBall);
            activeBall = null;
            hasBallActive = false;

            Debug.Log("Ball collected by corgi!");

            if (collectedText)
                StartCoroutine(ShowCollectedText());
        }
    }

    private IEnumerator ShowCollectedText()
    {
        collectedText.gameObject.SetActive(true);
        collectedText.text = "Ball Collected!";
        yield return new WaitForSeconds(5f);
        collectedText.gameObject.SetActive(false);
    }
}
