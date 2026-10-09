using UnityEngine;
using UnityEngine.SceneManagement;
    
public class GameManager : MonoBehaviour
{
    public GameObject musicManagerPrefab;
    void Start()
    {
        if (MusicManager.instance != null)
        {
            MusicManager.instance.PlayMusicForScene(SceneManager.GetActiveScene().name);
        }
        else
        {
            Instantiate(musicManagerPrefab);
            MusicManager.instance.PlayMusicForScene(SceneManager.GetActiveScene().name);
        }
    }
    
}
