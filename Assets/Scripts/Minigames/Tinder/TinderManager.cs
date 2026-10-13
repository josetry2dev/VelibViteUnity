using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TinderManager : MonoBehaviour
{
    [Header(" --- Score Settings --- ")]
    public int goalScore = 300;
    public int pointsPerMatch = 10;
    public int pointsPenalty = 20;

    [Header(" --- Time Settings --- ")]
    public float timeLimit = 60f;

    [Header(" --- UI References --- ")]
    public GameObject matchImage;
    public Image profileImage;
    public RectTransform cardTransform;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI nameText;

    [Header(" --- Profiles Data --- ")]
    public List<TinderProfile> allProfiles;

    private PlayerInput _playerInput;
    private InputAction _moveAction;
    private InputAction _submitAction;

    private int _currentIndex;
    private int _currentScore;
    private float _currentTime;
    private bool _gameActive = true;
    private bool _canSwipe = true;
    private bool _win;
    private Vector2 _startPos;

    void Awake()
    {
        if (_playerInput == null) _playerInput = FindAnyObjectByType<PlayerInput>();
        _moveAction = _playerInput.actions["Move"];
        _submitAction = _playerInput.actions["Submit"];
        _startPos = cardTransform.anchoredPosition;
    }

    void OnEnable()
    {
        _moveAction.performed += OnMovePerformed;
        _moveAction.canceled += OnMoveCanceled;
        _submitAction.performed += OnSubmit;
    }

    void OnDisable()
    {
        _moveAction.performed -= OnMovePerformed;
        _moveAction.canceled -= OnMoveCanceled;
        _submitAction.performed -= OnSubmit;
    }

    void Start()
    {
        _currentTime = timeLimit;
        UpdateUI();
    }

    void Update()
    {
        if (!_gameActive) return;

        _currentTime -= Time.deltaTime;
        timerText.text = Mathf.Max(0, _currentTime).ToString("F1") + "s";

        if (_currentTime <= 0)
        {
            GameOver();
        }
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        if (!_gameActive || !_canSwipe) return;

        Vector2 moveInput = context.ReadValue<Vector2>();

        if (moveInput.x > 0.7f) // SWIPE DROITE
        {
            StartCoroutine(AnimateSwipe(true));
        }
        else if (moveInput.x < -0.7f) // SWIPE GAUCHE
        {
            StartCoroutine(AnimateSwipe(false));
        }
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        _canSwipe = true;
    }

    private void OnSubmit(InputAction.CallbackContext context)
    {
        if (!_gameActive)
        {
            if (_win) {
                NextLevel();
            } else {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }
    }
    
    IEnumerator AnimateSwipe(bool right)
    {
        _canSwipe = false;
        float duration = 0.20f;
        float elapsed = 0f;

        Vector2 endPos = _startPos + new Vector2(right ? 400 : -400, 100f);
        float endRotation = right ? -25f : 25f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percent = elapsed / duration;
            cardTransform.anchoredPosition = Vector2.Lerp(_startPos, endPos, percent);
            cardTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(0, endRotation, percent));
            yield return null;
        }

        ProcessSwipeLogic(right);
    }

    void ProcessSwipeLogic(bool swipedRight)
    {
        TinderProfile currentProfile = allProfiles[_currentIndex];
        
        // Logique : isTrap nécessite Gauche, Normal nécessite Droite
        bool isError = (currentProfile.isTrap && swipedRight) || (!currentProfile.isTrap && !swipedRight);

        if (isError) {
            _currentScore -= pointsPenalty;
            // On peut même clamp le score pour pas qu'il soit négatif si tu préfères
            _currentScore = Mathf.Max(0, _currentScore); 
        } else {
            _currentScore += pointsPerMatch;
        }

        // --- LA BOUCLE ---
        _currentIndex++;
        if (_currentIndex >= allProfiles.Count) 
        {
            _currentIndex = 0; // On recommence la liste
        }

        // --- CONDITION DE VICTOIRE ---
        UpdateUI();
        cardTransform.anchoredPosition = _startPos;
        cardTransform.localRotation = Quaternion.identity;
        
        if (_currentScore >= goalScore)
        {
            _win = true;
            GameOver();
        }
    }

    void UpdateUI()
    {
        profileImage.sprite = allProfiles[_currentIndex].photo;
        nameText.text = allProfiles[_currentIndex].profileName;
        scoreText.text = $"{_currentScore} sur {goalScore}";
    }

    void GameOver()
    {
        _gameActive = false;
        if (_win) {
            timerText.text = "";
            matchImage.SetActive(true);
        } else {
            timerText.text = "TROP LENT...";
        }
    }
    
    public void NextLevel()
    {
        if (SceneManager.GetActiveScene().buildIndex != SceneManager.sceneCountInBuildSettings-1)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        }
    }
}