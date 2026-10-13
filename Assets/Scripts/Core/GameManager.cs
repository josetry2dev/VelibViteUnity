using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header(" --- References --- ")]
    public GameObject musicManagerPrefab;

    void Start()
    {
        if (MusicManager.instance == null)
        {
            Instantiate(musicManagerPrefab);
        }
        MusicManager.instance.PlayMusicForScene(SceneManager.GetActiveScene().name);
    }
}
