using UnityEngine;

public class FinishLine : MonoBehaviour
{ 
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !LevelManager.Instance.IsGameOver())
        {
            var pc = other.GetComponent<PlayerCollision>();
            if (pc != null)
                pc.SetFinished();
            LevelManager.Instance.CompleteLevel();
        }
    }
}
