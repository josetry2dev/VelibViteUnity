using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System;

public class SmsManager : MonoBehaviour
{
    [Header("Input Settings")]
    private PlayerInput _playerInput;
    private InputAction _navigateAction;
    private bool _canProcessInput = true;

    [Header("UI Elements")]
    public GameObject smsPanel;
    public TMP_Text historyText;
    public TMP_Text currentInputText;
    public Transform keySequenceContainer;
    public TMP_Text timeOutTxt;
    
    public float pulseSpeed = 3f;
    public float pulseAmount = 0.1f;
    public float selectedScale = 1f;

    [Header("Prefabs")]
    public GameObject keyIconPrefab;
    
    [Header("Audio Settings")]
    public AudioClip receptionSound;
    public AudioClip sendingSound;
    public AudioClip typingSound;
    [Range(0, 1)] public float smsVolume = 0.7f;

    private SmsDialogue _currentDialogue;
    private int _currentMsgIndex = 0;
    private List<Vector2> _requiredSequence = new List<Vector2>();
    private int _sequenceIndex = 0;
    private bool _isWaitingForInput = false;
    private float _timer = 0f;
    private float _maxTimer = 0f;
    
    private Action _onDialogueEndCallback; 
    
    
    void Awake()
    {
        if (_playerInput == null) _playerInput = FindAnyObjectByType<PlayerInput>();
        _navigateAction = _playerInput.actions["Navigate"];
    }

    void OnEnable()
    {
        _navigateAction.performed += OnNavigatePerformed;
    }

    void OnDisable()
    {
        _navigateAction.performed -= OnNavigatePerformed;
    }

    public void StartDialogue(SmsDialogue dialogue, Action onComplete = null)
    {
        AudioManager.Instance.PlaySFX(receptionSound);
        if (dialogue == null || dialogue.messages.Count == 0) return;
        _onDialogueEndCallback = onComplete;
        _currentDialogue = dialogue;
        _currentMsgIndex = 0;
        smsPanel.SetActive(true);
        historyText.text = "";
        NextMessage();
    }

    void NextMessage()
    {
        if (_currentMsgIndex >= _currentDialogue.messages.Count)
        {
            StartCoroutine(CloseAfterDelay(2f));
            return;
        }

        var msg = _currentDialogue.messages[_currentMsgIndex];

        if (msg.isPlayer)
        {
            PreparePlayerResponse(msg);
        }
        else
        {
            AudioManager.Instance.PlaySFX(receptionSound);
            historyText.text += $"\n<color=#00CCFF><b>{msg.senderName}:</b></color> {msg.content}";
            _currentMsgIndex++;
            Invoke("NextMessage", msg.tempo);
        }
    }

    void PreparePlayerResponse(SmsMessage msg)
    {
        _isWaitingForInput = true;
        currentInputText.text = "";
        _sequenceIndex = 0;
        
        if(msg.timeOut > 0) 
        {
            _timer = msg.timeOut;
            _maxTimer = msg.timeOut;
            timeOutTxt.text = _timer.ToString("0.00");
        } else
        {
            timeOutTxt.text = "";
        }

        // Génération de la séquence de flèches
        int numKeys = Mathf.Max(3, msg.content.Length / 7);
        _requiredSequence.Clear();
        foreach (Transform child in keySequenceContainer) Destroy(child.gameObject);

        Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

        for (int i = 0; i < numKeys; i++)
        {
            Vector2 dir = directions[UnityEngine.Random.Range(0, 4)];
            _requiredSequence.Add(dir);
            
            // Création de l'icône visuelle
            GameObject iconObj = Instantiate(keyIconPrefab, keySequenceContainer);
            iconObj.GetComponent<SmsKeyIcon>().Setup(dir);
        }
        
        keySequenceContainer.gameObject.SetActive(true);
    }

    // C'est ici que le New Input System nous envoie la direction
    private void OnNavigatePerformed(InputAction.CallbackContext context)
    {
        if (!_isWaitingForInput || !_canProcessInput) return;

        Vector2 inputDir = context.ReadValue<Vector2>();

        // On compare la direction (avec une petite marge d'erreur pour les sticks analogiques)
        if (inputDir == _requiredSequence[_sequenceIndex])
        {
            OnCorrectKey();
        }
    }
    void OnCorrectKey()
    {
        // On grise l'icône correspondante
        keySequenceContainer.GetChild(_sequenceIndex).GetComponent<SmsKeyIcon>().SetValidated();
        
        AudioManager.Instance.PlaySFX(typingSound, smsVolume);
        _sequenceIndex++;

        // Mise à jour progressive du texte de réponse
        string fullText = _currentDialogue.messages[_currentMsgIndex].content;
        int charsToShow = Mathf.FloorToInt((float)_sequenceIndex / _requiredSequence.Count * fullText.Length);
        currentInputText.text = fullText.Substring(0, charsToShow);

        if (_sequenceIndex >= _requiredSequence.Count)
        {
            FinishMessage();
        }
    }

    void FinishMessage()
    {
        AudioManager.Instance.PlaySFX(sendingSound, 0.3f);
        keySequenceContainer.gameObject.SetActive(false);
        timeOutTxt.text = "";
        _requiredSequence.Clear();
        _isWaitingForInput = false;
        var msg = _currentDialogue.messages[_currentMsgIndex];
        historyText.text += $"\n<color=#00FF44><b>Moi:</b></color> {msg.content}";
        currentInputText.text = "";
        _currentMsgIndex++;
        Invoke("NextMessage", msg.tempo);
    }

    void Update()
    {
        ApplyPulseEffect();
        
        if (!_isWaitingForInput || _timer <= 0) return;

        _timer -= Time.deltaTime;
        timeOutTxt.text = _timer.ToString("0.00");

        if (_timer <= 0)
        {
            _currentDialogue.messages[_currentMsgIndex].onTimeout?.Invoke();
            CloseSms();
        }
    }

    IEnumerator CloseAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        CloseSms();
    }

    void CloseSms()
    {
        _isWaitingForInput = false;
        smsPanel.SetActive(false);
        if (_onDialogueEndCallback != null)
        {
            _onDialogueEndCallback.Invoke();
            _onDialogueEndCallback = null; // On vide pour ne pas la rappeler par erreur
        }
    }
    
    private void ApplyPulseEffect()
    {
        if (_requiredSequence.Count == 0) return;
        float wave = Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
        float currentScale = selectedScale + wave;
        keySequenceContainer.GetChild(_sequenceIndex).localScale = Vector3.one * currentScale;
    }
}