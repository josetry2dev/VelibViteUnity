using UnityEngine;

public class CarDestroyer : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        // On vérifie si c'est bien une voiture (grâce au script CarBehavior ou un Tag)
        if (other.GetComponentInParent<CarBehavior>() != null)
        {
            // On détruit l'objet de la voiture
            Destroy(other.transform.gameObject);
        }
    }
}