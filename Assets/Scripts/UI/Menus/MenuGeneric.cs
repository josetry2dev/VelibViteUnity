using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.Events;

[System.Serializable]
public class MenuOption
{
    public string optionName;
    public TextMeshProUGUI textElement;
    public UnityEvent onSelectAction;
}

public class MenuGeneric : MonoBehaviour
{
    [Header(" --- Options --- ")]
    public List<MenuOption> options = new List<MenuOption>();
    
    [Header(" --- Visual Settings --- ")]
    public Color activeColor = Color.yellow;
    public Color inactiveColor = Color.white;
    public float baseScale = 1f;
    public float selectedScale = 1.2f;
    
    [Header(" --- Juice Settings (Wave & Click) --- ")]
    public float pulseSpeed = 3f;
    public float pulseAmount = 0.1f;
    public float clickPunchScale = 1.5f;
    public float clickDuration = 0.1f;
    
    [Header(" --- Input --- ")]
    public PlayerInput playerInput;

    [Header(" --- Audio Settings --- ")]
    public AudioClip navigateSound;
    public AudioClip submitSound;
    [Range(0, 1)] public float uiVolume = 0.7f;

    private InputAction _navigateAction;
    private InputAction _submitAction;
    private int _currentIndex;
    private bool _canInput = true;
    private bool _isClicking;
    private const float InputCooldown = 0.1f;
    private float _activationCooldown = 0.2f;

    private void OnValidate()
    {
        // Ce code s'exécute uniquement dans l'éditeur Unity quand tu modifies un truc
        if (navigateSound == null) 
            navigateSound = Resources.Load<AudioClip>("Audio/UI/selection_menu_a");
        
        if (submitSound == null) 
            submitSound = Resources.Load<AudioClip>("Audio/UI/selection_menu_b");
    }
    private void OnEnable()
    {
        _activationCooldown = 0.2f;
        _canInput = true;

        if (playerInput == null) playerInput = FindAnyObjectByType<PlayerInput>();
        
        _navigateAction = playerInput.actions["Navigate"];
        _submitAction = playerInput.actions["Submit"];

        _navigateAction.performed += OnNavigate;
        _submitAction.performed += OnSubmit;

        _currentIndex = 0;
        ResetAllScales();
    }

    private void OnDisable()
    {
        if (_navigateAction != null) _navigateAction.performed -= OnNavigate;
        if (_submitAction != null) _submitAction.performed -= OnSubmit;
        
        if (Gamepad.current != null)
        {
            Gamepad.current.SetMotorSpeeds(0f, 0f);
        }
    }

    private void Update()
    {
        if (_activationCooldown > 0)
            _activationCooldown -= Time.unscaledDeltaTime;

        // --- EFFET WAVE (PULSE) ---
        if (options.Count > 0 && !_isClicking)
        {
            ApplyPulseEffect();
        }
    }

    private void ApplyPulseEffect()
    {
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i].textElement == null) continue;

            if (i == _currentIndex)
            {
                // Calcul du sinus pour l'effet de respiration
                float wave = Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
                float currentScale = selectedScale + wave;
                options[i].textElement.transform.localScale = Vector3.one * currentScale;
                options[i].textElement.color = activeColor;
            }
            else
            {
                // Reset propre pour les autres
                options[i].textElement.transform.localScale = Vector3.one * baseScale;
                options[i].textElement.color = inactiveColor;
            }
        }
    }

    private void OnNavigate(InputAction.CallbackContext context)
    {
        if (!_canInput || _isClicking || options.Count == 0) return;
    
        Rumble(0.1f, 0f, 0.01f);
        
        Vector2 move = context.ReadValue<Vector2>();

        if (Mathf.Abs(move.y) > 0.5f)
        {
            AudioManager.Instance.PlaySFX(navigateSound, uiVolume);
            
            int dir = move.y > 0 ? -1 : 1; 
            _currentIndex = (_currentIndex + dir + options.Count) % options.Count;
            
            StartCoroutine(InputTick());
        }
    }

    private void OnSubmit(InputAction.CallbackContext context)
    {
        if (_activationCooldown > 0 || !_canInput || _isClicking || options.Count == 0) return;

        StartCoroutine(ClickCoroutine());
    }

    
    IEnumerator ClickCoroutine()
    {
        _isClicking = true;
        
        AudioManager.Instance.PlaySFX(submitSound, 0.4f, false);
        Rumble(1f, 0f, 0.05f);
        
        RectTransform targetTxt = options[_currentIndex].textElement.rectTransform;
        
        targetTxt.localScale = Vector3.one * clickPunchScale;
        
        yield return new WaitForSecondsRealtime(clickDuration);
        
        options[_currentIndex].onSelectAction?.Invoke();
        
        _isClicking = false;
        
        if (targetTxt != null) targetTxt.localScale = Vector3.one * selectedScale;
    }

    private void ResetAllScales()
    {
        foreach (var opt in options)
        {
            if (opt.textElement != null)
                opt.textElement.transform.localScale = Vector3.one * baseScale;
        }
    }

    IEnumerator InputTick()
    {
        _canInput = false;
        yield return new WaitForSecondsRealtime(InputCooldown);
        _canInput = true;
    }
    
    public void Rumble(float low, float high, float duration)
    {
        if (Gamepad.current != null)
        {
            Gamepad.current.SetMotorSpeeds(low, high);
            StartCoroutine(StopRumble(duration));
        }
    }
    private IEnumerator StopRumble(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        if (Gamepad.current != null)
        {
            Gamepad.current.SetMotorSpeeds(0f, 0f);
        }
    }
}