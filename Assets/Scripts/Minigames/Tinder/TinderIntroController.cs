using UnityEngine;
using UnityEngine.InputSystem;

public class TinderIntroController : MonoBehaviour
{
    [Header(" --- References --- ")]
    public PlayerInput playerInput;

    [Header(" --- Intro Objects --- ")]
    public GameObject explicationCanvas;
    public GameObject tinderSmartphone;

    private InputAction _accelerateAction; // X

    void Awake()
    {
        if (playerInput == null) playerInput = FindAnyObjectByType<PlayerInput>();
        _accelerateAction = playerInput.actions["Accelerate"];

        // Au lancement : on affiche l'explication, le smartphone attend
        explicationCanvas.SetActive(true);
        tinderSmartphone.SetActive(false);
    }

    void OnEnable()
    {
        _accelerateAction.performed += OnAcceleratePerformed;
    }

    void OnDisable()
    {
        _accelerateAction.performed -= OnAcceleratePerformed;
    }

    private void OnAcceleratePerformed(InputAction.CallbackContext context)
    {
        explicationCanvas.SetActive(false);
        tinderSmartphone.SetActive(true);
        enabled = false; // L'intro ne sert plus
    }
}
