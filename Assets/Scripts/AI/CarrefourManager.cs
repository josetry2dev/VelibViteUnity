using UnityEngine;

public class CarrefourManager : MonoBehaviour
{
    [Header(" --- Distance Settings --- ")]
    public float activationDistance = 150f; // Distance pour commencer à faire bouger les voitures
    public float destroyDistance = 30f; // Distance DERRIÈRE le joueur pour supprimer le carrefour

    private Transform player;
    private bool isActive;
    private CarSpawner[] spawners; // Référence aux spawners pour les allumer/éteindre

    void Start()
    {
        // Trouve le joueur via le Tag "Player" (assure-toi que ton joueur a ce tag)
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        spawners = GetComponentsInChildren<CarSpawner>();

        // Au début, on désactive les spawners pour économiser
        ToggleSpawners(false);
    }

    void Update()
    {
        if (player == null) return;

        float distanceZ = transform.position.z - player.position.z;

        // 1. Activation (Le carrefour est devant nous, assez proche)
        if (!isActive && distanceZ < activationDistance && distanceZ > 0)
        {
            isActive = true;
            ToggleSpawners(true);
        }

        // 2. Suppression (Le carrefour est derrière nous)
        // Si distanceZ est négatif, c'est que le carrefour est derrière.
        // Si on a dépassé de 'destroyDistance', on supprime tout.
        if (distanceZ < -destroyDistance)
        {
            Destroy(gameObject);
        }
    }

    void ToggleSpawners(bool state)
    {
        foreach (var spawner in spawners)
        {
            spawner.enabled = state;
        }
    }
}
