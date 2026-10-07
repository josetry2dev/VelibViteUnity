using UnityEngine;

public class StepTriggerZone : MonoBehaviour
{
    public TutoController tutoController;
    public int step
        ;
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if(tutoController != null) 
            {
                tutoController.OnPlayerEnteredTrigger(step);
                Destroy(gameObject); 
            }
        }
    }
}