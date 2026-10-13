using UnityEngine;

public class CarSpawner : MonoBehaviour
{
    [Header(" --- References --- ")]
    public GameObject carPrefab;

    [Header(" --- Spawn Settings --- ")]
    public float spawnRate = 3f;
    public float spawnCheckRadius = 3f;
    public LayerMask blockingLayer; // Layers qui empêchent le spawn

    private float timer;

    void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0)
        {
            TrySpawnCar();
            timer = spawnRate;
        }
    }

    void TrySpawnCar()
    {
        // Sécurité : Si le prefab n'est pas assigné ou a été détruit
        if (carPrefab == null)
        {
            Debug.LogWarning("Attention : Le CarPrefab est manquant sur " + gameObject.name);
            return;
        }

        if (!Physics.CheckSphere(transform.position, spawnCheckRadius, blockingLayer))
        {
            Instantiate(carPrefab, transform.position, transform.rotation, transform);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, spawnCheckRadius);
    }
}
