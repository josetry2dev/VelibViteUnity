using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.InputSystem;

public class NameSelector : MonoBehaviour
{
    public LevelCompletedMenus levelCompletedMenus;
    
    [Header("UI Elements")]
    public TextMeshProUGUI[] letterTexts;
    public Color activeColor = Color.yellow;
    public Color inactiveColor = Color.white;

    [Header("Settings")]
    private const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 !";
    private readonly int[] _charIndices = new int[5];
    private int _currentSlot = 0;

    [Header("Input")]
    public PlayerInput playerInput;
    private InputAction _navigateAction;
    private InputAction _submitAction;
    
    [Header("Audio Settings")]
    public AudioClip navigateSound;
    public AudioClip submitSound;
    [Range(0, 1)] public float uiVolume = 0.7f;
    
    private bool _canInput = true;
    private const float InputCooldown = 0.15f;
    private float _submitDelay = 0.3f;

    private void OnValidate()
    {
        // Ce code s'exécute uniquement dans l'éditeur Unity quand tu modifies un truc
        if (navigateSound == null) 
            navigateSound = Resources.Load<AudioClip>("Audio/UI/selection_menu_a");
        
        if (submitSound == null) 
            submitSound = Resources.Load<AudioClip>("Audio/UI/selection_menu_c");
    }
    private void OnEnable()
    {
        if (playerInput == null) playerInput = FindAnyObjectByType<PlayerInput>();
        _navigateAction = playerInput.actions["Navigate"];
        _submitAction = playerInput.actions["Submit"];
        
        string lastPlayerName = PlayerPrefs.GetString("LastPlayerName", "AAAAA");
        
        for (int i = 0; i < 5; i++)
        {
            char initialChar = (i < lastPlayerName.Length) ? lastPlayerName[i] : 'A';
            _charIndices[i] = Chars.IndexOf(initialChar);
            if (_charIndices[i] == -1) _charIndices[i] = 0;
        }

        _currentSlot = 0;
        UpdateVisuals();
        
        _navigateAction.performed += OnNavigatePerformed;
        _submitAction.performed += OnSubmitPerformed;
    }
    
    void OnDisable()
    {
        _navigateAction.performed -= OnNavigatePerformed;
        _submitAction.performed -= OnSubmitPerformed;
    }

    void Update()
    {
        if (_submitDelay > 0.001f)
        {
            _submitDelay -= Time.unscaledDeltaTime;
        }
    }
    public void OnNavigatePerformed(InputAction.CallbackContext context)
    {
        if (!_canInput) return;
        Vector2 move = context.ReadValue<Vector2>();
        if (Mathf.Abs(move.x) > 0.7f)
        {
            _currentSlot = Mathf.Clamp(_currentSlot + (move.x > 0 ? 1 : -1), 0, 4);
            StartCoroutine(InputTick());
            UpdateVisuals();
        }
        if (Mathf.Abs(move.y) > 0.7f)
        {
            int dir = move.y > 0 ? -1 : 1;
            _charIndices[_currentSlot] = (_charIndices[_currentSlot] + dir + Chars.Length) % Chars.Length;
            StartCoroutine(InputTick());
            UpdateVisuals();
        }
    }
    public void OnSubmitPerformed(InputAction.CallbackContext context)
    {
        if (_submitDelay >  0.001f) return;
        if (!_canInput) return;
        SubmitName();
    }

    void UpdateVisuals()
    {
        for (int i = 0; i < letterTexts.Length; i++)
        {
            letterTexts[i].text = Chars[_charIndices[i]].ToString();
            letterTexts[i].color = (i == _currentSlot) ? activeColor : inactiveColor;
            
            // Petit effet de scale pour la lettre active
            letterTexts[i].transform.localScale = (i == _currentSlot) ? Vector3.one * 1.2f : Vector3.one;
        }
    }

    IEnumerator InputTick()
    {
        _canInput = false;
        AudioManager.Instance.PlaySFX(navigateSound, uiVolume);
        yield return new WaitForSecondsRealtime(InputCooldown);
        _canInput = true;
    }

    public string GetFullName()
    {
        string finalName = "";
        for (int i = 0; i < 5; i++)
        {
            finalName += Chars[_charIndices[i]];
        }
        return finalName.Trim();
    }

    void SubmitName()
    {
        string finalName = GetFullName();
        AudioManager.Instance.PlaySFX(submitSound, uiVolume);
        PlayerPrefs.SetString("LastPlayerName", finalName);
        levelCompletedMenus.NameSelected(finalName);
        gameObject.SetActive(false);
    }
}