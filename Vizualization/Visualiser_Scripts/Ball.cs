using UnityEngine;

public class Ball : MonoBehaviour
{
    [Header("Floating effect (optional)")]
    public float floatAmplitude = 0.05f;
    public float floatFrequency = 2f;

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        // make it float gently
        transform.position = startPos + Vector3.up * Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
        transform.Rotate(Vector3.up, 40f * Time.deltaTime); // spin
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if the Corgi collided
        if (other.CompareTag("Corgi"))
        {
            Debug.Log("🐾 Food collected!");
            BallManager.Instance.BallCollected(); // notify manager
            Destroy(gameObject);
        }
    }
}
