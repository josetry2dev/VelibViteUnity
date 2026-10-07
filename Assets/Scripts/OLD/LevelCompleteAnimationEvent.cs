using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelCompleteAnimationEvent : MonoBehaviour
{
    public void LoadNextLevel()
    {
        if (SceneManager.GetActiveScene().buildIndex != SceneManager.sceneCountInBuildSettings-1)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        }
        else
        {
            Debug.Log("Game completed !");
        }
        
    }
}
