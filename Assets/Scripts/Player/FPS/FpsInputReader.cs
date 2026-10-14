using UnityEngine;
using UnityEngine.InputSystem;

public class FpsInputReader : MonoBehaviour
{
    private PlayerInput _playerInput;

    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _jumpAction;
    private InputAction _sprintAction;
    private InputAction _postureAction;

    public Vector2 Move =>
        _moveAction != null
            ? _moveAction.ReadValue<Vector2>()
            : Vector2.zero;

    public Vector2 Look =>
        _lookAction != null
            ? _lookAction.ReadValue<Vector2>()
            : Vector2.zero;

    public bool JumpPressed =>
        _jumpAction != null && _jumpAction.WasPressedThisFrame();

    public bool SprintPressed =>
        _sprintAction != null && _sprintAction.WasPressedThisFrame();

    public bool PosturePressed =>
        _postureAction != null && _postureAction.WasPressedThisFrame();

    public bool PostureHeld =>
        _postureAction != null && _postureAction.IsPressed();

    private void Awake()
    {
        _playerInput = FindAnyObjectByType<PlayerInput>();

        if (_playerInput == null)
        {
            Debug.LogError(
                "FpsInputReader: No central PlayerInput found.",
                this
            );
            enabled = false;
            return;
        }

        _moveAction = _playerInput.actions["Move"];
        _lookAction = _playerInput.actions["Look"];
        _jumpAction = _playerInput.actions["Jump"];
        _sprintAction = _playerInput.actions["Sprint"];
        _postureAction = _playerInput.actions["Crouch"];
    }
}