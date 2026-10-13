using UnityEngine;

public class ObstacleFourbe : MonoBehaviour
{
    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            transform.position -= new Vector3(0, 0.1f, 0);
        }
    }
}
