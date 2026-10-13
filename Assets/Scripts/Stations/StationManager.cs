using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;
using TMPro;
using System;

public class StationManager : MonoBehaviour
{
    #region Params

    [Header(" --- Camera Settings --- ")]
    public GameObject cameraPrefab;
    public Transform[] bikePositions;
    public float smoothSpeed = 5f;
    public float shakeIntensity = 0.1f;

    [Header(" --- Spawn Settings --- ")]
    public Transform spawnPosition;
    public GameObject playerPrefab;

    [Header(" --- Input --- ")]
    public PlayerInput playerInput;

    [Header(" --- Selection Settings --- ")]
    public float probVeloPourri = 0.90f;
    public float messageTime = 2.0f;

    [Header(" --- UI Panels & Text --- ")]
    public GameObject panelSelection;
    public TMP_Text errorText;
    public Button selectBikeButton;

    [Header(" --- Messages --- ")]
    public string[] messagesErrors =
    {
        "Pu de batteri",
        "Wsh le pneu il est crever",
        "Maintenance askip",
        "...",
        "Jarrive pas le sortir ptn",
        "Pu de batteri"
    };

    public string[] messagesSuccess =
    {
        "Ah celui la il est patat",
        "Ptn yes bolide",
        "ouah il tue le biclou",
        "c partiiii",
        "tention les filles jpasse en mode enflure",
        "missil en approche"
    };

    // Camera
    private GameObject _stationCameraInstance;
    private Transform _stationCameraTransform;
    private Transform _targetTransform;

    // Input
    private InputAction _navigateAction;
    private InputAction _submitAction;

    // Selection logic
    private bool _isActive;
    private int _currentIndex;
    private string[] _bikeStatus;
    private int _bikesTriedCount;
    private bool _isSuccess;
    private Action _onSuccessCallback;

    // Spam challenge
    private int _spamCount;
    private float _spamTimer;
    private bool _isSpamMode;
    private const string SpecialErrorMessage = "Jarrive pas le sortir ptn";
    
    #endregion
    
    #region Init & Input
    void Awake()
    {
        if (playerInput == null) playerInput = FindAnyObjectByType<PlayerInput>();
        _navigateAction = playerInput.actions["Navigate"];
        _submitAction = playerInput.actions["Submit"];
    }

    void OnEnable()
    {
        _bikeStatus = new string[bikePositions.Length];
        _bikesTriedCount = 0;
        _currentIndex = 0;
        _isActive = true;
        _isSuccess = false;
        _onSuccessCallback = null;
        panelSelection.SetActive(true);
        if (errorText) errorText.text = "";
        _navigateAction.performed += OnNavigatePerformed;
        _submitAction.performed += OnSubmitPerformed;
        
    }
    void OnDisable()
    {
        _navigateAction.performed -= OnNavigatePerformed;
        _submitAction.performed -= OnSubmitPerformed;
        panelSelection.SetActive(false);
    }
    public void OnNavigatePerformed(InputAction.CallbackContext context)
    {
        if (_isSuccess || !_isActive) return;
        selectBikeButton.Select();
        Vector2 move = context.ReadValue<Vector2>();
        if (move.x > 0.7f) NextBike();
        else if (move.x < -0.7f) PrevBike();
    }
    public void OnSubmitPerformed(InputAction.CallbackContext context)
    {
        if (_isSuccess || !_isActive) return;
        TrySelectBike();
    }
    
    #endregion
    
    void Update()
    {
        if (!_isActive) return;
        if (!_isSpamMode)
        {
            _stationCameraTransform.position =
                Vector3.Lerp(_stationCameraTransform.position, _targetTransform.position, Time.deltaTime * smoothSpeed);
            _stationCameraTransform.rotation =
                Quaternion.Slerp(_stationCameraTransform.rotation, _targetTransform.rotation, Time.deltaTime * smoothSpeed);
        }

        if (_isSpamMode)
        {
            _spamTimer += Time.deltaTime;
            if (_spamTimer > 2f) // Si trop lent (plus de 2 sec)
            {
                ResetSpam();
            }
        }
    }

    #region Selection

    public void StartSelection(Transform pos, Action onComplete = null)
    {
        _onSuccessCallback = onComplete;
        _stationCameraInstance = Instantiate(cameraPrefab, pos.position, pos.rotation);
        _stationCameraTransform = _stationCameraInstance.transform;
        _targetTransform = bikePositions[_currentIndex];
        selectBikeButton.Select();
    }
    
    public void NextBike()
    {
        _isSpamMode = false;
        _currentIndex = (_currentIndex + 1) % bikePositions.Length;
        _targetTransform = bikePositions[_currentIndex];
        if (errorText) errorText.text = ""; // Efface l'erreur quand on change
    }
    public void PrevBike()
    {
        _isSpamMode = false;
        _currentIndex--;
        if (_currentIndex < 0) _currentIndex = bikePositions.Length - 1;
        _targetTransform = bikePositions[_currentIndex];
        if (errorText) errorText.text = "";
    }

    public void TrySelectBike()
    {
        if (_isSuccess) return;
        if (_isSpamMode)
        {
            HandleSpam();
            return;
        }
        
        if (!string.IsNullOrEmpty(_bikeStatus[_currentIndex]))
        {
            ProcessResult(_bikeStatus[_currentIndex]);
            return;
        }
        string result;
        if (_bikesTriedCount == bikePositions.Length - 1) result = "SUCCESS";
        else
        {
            bool win = UnityEngine.Random.value > probVeloPourri; 
            if (win) result = "SUCCESS";
            else
            {
                int index = UnityEngine.Random.Range(0, messagesErrors.Length);
                result = messagesErrors[index];
            }
        }

        _bikeStatus[_currentIndex] = result;
        _bikesTriedCount++;
        ProcessResult(result);
    }

    #endregion
    
    private void StartGame()
    {
        // TODO a changer 
        _isActive = false;
        _onSuccessCallback?.Invoke();
        Spawn();
    }

    private void Spawn()
    {
        
        Destroy(_stationCameraInstance);
        // if (!playerInput.gameObject.CompareTag("Player")) Destroy(playerInput.gameObject);
        Instantiate(playerPrefab, spawnPosition.position, spawnPosition.rotation);
        enabled = false;
    }
    private void ProcessResult(string result)
    {
        if (result == "SUCCESS")
        {
            _isSuccess = true;
            int index = UnityEngine.Random.Range(0, messagesSuccess.Length);
            errorText.color = Color.white;
            errorText.text = messagesSuccess[index];
            Invoke(nameof(StartGame), messageTime);
        }
        else if (result == SpecialErrorMessage)
        {
            errorText.color = Color.red;
            errorText.text = result;
            StartCoroutine(StartSpamChallenge());
        }
        else
        {
            errorText.color = Color.red;
            errorText.text = result;
        }
    }
    
    #region Spam Velib Bloqué
    IEnumerator StartSpamChallenge()
    {
        _isSpamMode = true;
        StartCoroutine(ShakeCamera(0.1f, shakeIntensity));
        _spamCount = 1;
        _spamTimer = 0f;
        yield return null; 
    }

    private void HandleSpam()
    {
        _spamCount++;
        // Petit shake à chaque pression
        StartCoroutine(ShakeCamera(0.1f, shakeIntensity));

        if (_spamCount >= 10 && _spamTimer <= 2f)
        {
            _isSpamMode = false;
            _isSuccess = true;
            errorText.text = "voilaaa bordel tu vois quand tu veux";
            errorText.color = Color.white;
            Invoke(nameof(StartGame), messageTime);
        }
    }

    private void ResetSpam()
    {
        _isSpamMode = false;
        _spamCount = 0;
    }
    
    IEnumerator ShakeCamera(float duration, float magnitude)
    {
        Vector3 originalPos = _targetTransform.position;
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            float x = UnityEngine.Random.Range(-1f, 1f) * magnitude;
            float y = UnityEngine.Random.Range(-1f, 1f) * magnitude;

            _stationCameraTransform.position = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);
            elapsed += Time.deltaTime;
            yield return null;
        }
        _stationCameraTransform.position = originalPos;
    }
    #endregion
}