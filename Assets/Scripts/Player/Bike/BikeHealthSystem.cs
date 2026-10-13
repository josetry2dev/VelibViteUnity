using UnityEngine;
using System.Collections;

public class BikeHealthSystem : MonoBehaviour
{
    [Header(" --- References --- ")]
    public BikeControlsBase bikeControls;
    public GameObject bikeMeshRoot;
    public BikeCameraEffect bikeCamera; 
    public CameraController cameraController; 
    public BikeVisualController visualController;
    public BikeCameraEffect bikeCameraEffect;
    
    [Header(" --- Life Settings --- ")]
    public int startLives = 3;
    public int maxLives = 5;
    
    [Header(" --- Invincibility & Respawn --- ")]
    public float invincibilityDuration = 3f;
    public float respawnDelay = 1.5f;

    [Header(" --- Checkpoint Logic --- ")]
    public float safePositionCheckInterval = 0.5f;
    public float minSpeedForSafePos = 2f;

    // Etat
    private int _currentLives;
    private bool _isInvincible;
    private bool _isRespawning;
    private Vector3 _lastSafePosition;
    private Quaternion _lastSafeRotation;

    void Awake()
    {
        _currentLives = startLives;
        if (bikeControls == null) bikeControls = GetComponent<BikeControlsBase>();
        if (cameraController == null) cameraController = GetComponent<CameraController>();
        
        _lastSafePosition = transform.position;
        _lastSafeRotation = transform.rotation;
        StartCoroutine(TrackSafePositionRoutine());
    }

    private void OnDestroy()
    {
        LevelManager.Instance.ResetBrokenPanel();
    }

    public void TakeDamage()
    {
        if (_isInvincible || _isRespawning) return;
        _currentLives--;
        if (bikeCameraEffect != null) bikeCameraEffect.TriggerCrashEffect();
        if (bikeControls != null) bikeControls.enabled = false;
        if (cameraController != null) cameraController.enabled = false;
        if (visualController != null) visualController.enabled = false;

        if (bikeControls is BikeControlsOW owScript)
        {
            if (_currentLives > 0) // Crash level 0
            {
                StartCoroutine(RespawnSequence());
                LevelManager.Instance.OnCrash(0, respawnDelay);
            }

            if (_currentLives == 0) // Crash level 1 
            {
                StartCoroutine(RespawnSequence());
                LevelManager.Instance.OnCrash(1, respawnDelay * 2);
                owScript.SetBrokenMode();
                return;
            }

            if (_currentLives <= 0) // Crash level 2 
            {
                LevelManager.Instance.OnCrash(2, respawnDelay * 2);
            }
        }
        else LevelManager.Instance.OnCrash(2, respawnDelay);

    }

    public void AddLife(int amount)
    {
        _currentLives = Mathf.Min(_currentLives + amount, maxLives);
    }

    private IEnumerator TrackSafePositionRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(safePositionCheckInterval);

            // On ne sauvegarde que si on est au sol, pas en train de respawn, et qu'on roule
            if (bikeControls.IsGrounded() && !_isRespawning && bikeControls.GetSpeedForUI() > minSpeedForSafePos)
            {
                // On lève un peu le point pour éviter de respawn DANS le sol
                _lastSafePosition = transform.position + Vector3.up * 1f;
                _lastSafeRotation = transform.rotation;
            }
        }
    }

    private IEnumerator RespawnSequence()
    {
        _isRespawning = true;
        _isInvincible = true;

        yield return new WaitForSeconds(respawnDelay);
        
        if (bikeControls != null) bikeControls.ResetBikePhysics(_lastSafePosition, _lastSafeRotation);
        if (bikeControls != null) bikeControls.enabled = true;
        if (bikeCamera != null) bikeCamera.ResetCamera();
        if (cameraController != null) cameraController.enabled = true;
        if (visualController != null) visualController.enabled = true;
        
        float timer = 0f;
        bool isVisible = true;
        while (timer < invincibilityDuration)
        {
            timer += 0.1f;
            isVisible = !isVisible;
            if (bikeMeshRoot != null) bikeMeshRoot.SetActive(isVisible);
            yield return new WaitForSeconds(0.1f);
        }

        if (bikeMeshRoot != null) bikeMeshRoot.SetActive(true);
        _isInvincible = false;
        _isRespawning = false;
    }
    
    public int GetCurrentLives() => _currentLives;
    public bool IsInvincible() => _isInvincible;
}