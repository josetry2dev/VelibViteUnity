using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    [Header(" --- References --- ")]
    public ScoreManager scoreManager;
    public LevelCompletedMenus levelCompletedMenus;
    public PlayerInput _playerInput;

    [Header(" --- Level Infos --- ")]
    public LevelInfos levelInfos;

    [Header(" --- Timer --- ")]
    public float elapsedTime;
    public TextMeshProUGUI timeDisplayedText;

    [Header(" --- Crash UI --- ")]
    public GameObject crashPanel;
    public TMP_Text crashText;
    public GameObject brokenPanel;

    [Header(" --- Pause & Game Over UI --- ")]
    public GameObject pausePanel;
    public GameObject gameOverPanel;
    public TMP_Text gameOverText;

    // State machine
    private enum GameState
    {
        Running,
        Paused
    }
    private GameState _gameState;

    private bool _gameOver;
    private InputAction _pauseAction;

    private readonly string[] _crashLevel0Messages =
    {
        "BIG PRAOUT",
        "T rincer",
        "Chacal c honteux",
        "pourri",
        "nan vraiment",
        "sa pu la croket",
        "degage",
        "tu sais pas piloter",
        "sayer jme barre",
        "Le capitalisme organise la production par le travail, puis s’approprie structurellement la valeur qu’il n’a pas creee.",
        "macron explosion",
        "c a cause de hidalgo",
        "NOOB",
        "oh oh fait attention meme",
        "tu sais pas jouer",
        "nan nan tas iech de fou",
        "gabriel kouyenski."
    };
    private readonly string[] _crashLevel1Messages =
    {
        "Salo ! ta peter le velo !"
    };
    private readonly string[] _gameOverMessages =
    {
        "T mort chacal",
        "T virer de chez virer tu degage"
    };
    
    void Awake()
    {
        Instance = this;
        // Inputs
        _pauseAction = _playerInput.actions["Pause"];
        
        // State machine
        _gameState = GameState.Running;
        
        scoreManager.InitCurrentLevel(levelInfos);
    }

    private void Update()
    {
        if (_gameState == GameState.Running)
        {
            elapsedTime += Time.deltaTime;
            timeDisplayedText.text = elapsedTime.ToString("0.00");
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
    
    private void OnEnable()
    {
        _pauseAction.performed += OnPausePerformed;
    }

    private void OnDisable()
    {
        _pauseAction.performed -= OnPausePerformed;
    }

    public void OnPausePerformed(InputAction.CallbackContext ctx)
    {
        if (_gameState == GameState.Running || _gameState == GameState.Paused)
            TogglePause();
    }
    
    public void TogglePause()
    {
        if (Time.timeScale != 0f)
        {
            Time.timeScale = 0f;
            pausePanel.SetActive(true);
            _gameState = GameState.Paused;
        }
        else
        {
            Time.timeScale = 1f;
            pausePanel.SetActive(false);
            _gameState = GameState.Running;
        }
    }
    
    public void Restart()
    {
        TogglePause();
        _gameOver = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    public void QuitGame()
    {
        Application.Quit();
    }

    public void ReturnToMainMenu()
    {
        TogglePause();
        SceneManager.LoadScene(0);
    }
    
    public void CompleteLevel()
    {
        _gameState = GameState.Paused;
        levelCompletedMenus.LevelCompleted(levelInfos, elapsedTime);
    }
    
    public void OnCrash(int crashLevel, float respawnDelay)
    {
        _gameOver = true;
        switch (crashLevel)
        {
            case 0: // Vies restantes
                int index0 = Random.Range(0, _crashLevel0Messages.Length);
                crashText.text = _crashLevel0Messages[index0];
                crashText.color = Color.white;
                crashPanel.SetActive(true);
                break;
            case 1: // Plus de vies
                int index1 = Random.Range(0, _crashLevel1Messages.Length);
                crashText.text = _crashLevel1Messages[index1];
                crashText.color = Color.red;
                crashPanel.SetActive(true);
                brokenPanel.SetActive(true);
                break;
            case 2: // Mort complete
                int index2 = Random.Range(0, _gameOverMessages.Length);
                brokenPanel.SetActive(false);
                gameOverText.text = _gameOverMessages[index2];
                gameOverPanel.SetActive(true);
                Invoke(nameof(TogglePause), 2.5f); //Wait panel animation
                break;
        }
        Invoke(nameof(ResetCrashPanel), respawnDelay);
    }
    
    public void ResetCrashPanel()
    {
        if (crashPanel == null) return;
        crashPanel.SetActive(false);
        _gameOver = false;
    }
    public void ResetBrokenPanel()
    {
        if (brokenPanel == null) return;
        brokenPanel.SetActive(false);
    }
    
    public bool IsGameOver()
    {
        return _gameOver;
    }
    
    public bool IsLevelUnlocked(string levelID)
    {
        return PlayerPrefs.GetInt("Unlocked_" + levelID, 0) == 1;
    }
}
