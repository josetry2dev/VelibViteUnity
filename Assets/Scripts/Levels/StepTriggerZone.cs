using UnityEngine;

public class StepTriggerZone : MonoBehaviour
{
    [Header(" --- References --- ")]
    public TutoController tutoController;

    [Header(" --- Step Settings --- ")]
    public int step;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (tutoController != null)
            {
                tutoController.OnPlayerEnteredTrigger(step);
                Destroy(gameObject);
            }
        }
    }
}
