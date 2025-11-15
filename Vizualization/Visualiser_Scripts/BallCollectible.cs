using UnityEngine;

public class BallCollectible : MonoBehaviour
{
    private BallThrower thrower;

    public void Init(BallThrower parentThrower)
    {
        thrower = parentThrower;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Corgi"))
        {
            Debug.Log("🎯 Ball hit Corgi!");
            thrower.OnBallCollected(gameObject);
        }
    }
}
