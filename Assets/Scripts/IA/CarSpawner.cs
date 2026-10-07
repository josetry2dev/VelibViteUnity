using UnityEngine;

public class CarSpawner : MonoBehaviour
{
    public GameObject carPrefab;
    public float spawnRate = 3f;
    public float spawnCheckRadius = 3f;
    
    // NOUVEAU : On définit quels layers empêchent le spawn
    public LayerMask blockingLayer; 

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
        // Sécurité 1 : Si le prefab n'est pas assigné ou a été détruit
        if (carPrefab == null) 
        {
            Debug.LogWarning("Attention : Le CarPrefab est manquant sur " + gameObject.name);
            return; 
        }

        // Sécurité 2 : Si le Spawner lui-même est en train d'être détruit (optimisation manager)
        if (this == null || transform == null) return;

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