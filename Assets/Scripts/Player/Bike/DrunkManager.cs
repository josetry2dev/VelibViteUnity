using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class DrunkManager : MonoBehaviour
{
    [Header(" --- References --- ")]
    public Volume volume;
    [Tooltip("Vélo (optionnel)")]
    public CameraController cameraController;
    [Tooltip("Vélo (optionnel) : le boost consommé rend bourré")]
    public BikeControlsOW bikeControls;
    [Tooltip("À pied (optionnel)")]
    public FpsCameraController fpsCameraController;
    [Tooltip("À pied (optionnel)")]
    public FpsPlayerController fpsPlayerController;

    [Header(" --- Timing Settings --- ")]
    public float transitionSpeed = 0.25f; // Vitesse à laquelle l'effet s'installe
    public float resetDrunkTime = 90f;

    [Header(" --- Drunk AF Max Values --- ")]
    public float drunkLensDistortionAmplitude = 0.2f;
    public float drunkVignetteIntensity = 0.5f;
    public float drunkChromaticAberrationIntensity = 1f;
    public float drunkMotionBlurIntensity = 0.75f;

    [Header(" --- Current State (Read Only) --- ")]
    [SerializeField] private float drunkPercentage;

    // Post-process overrides
    private LensDistortion _lensDistortion;
    private ChromaticAberration _chromaticAberration;
    private MotionBlur _motionBlur;
    private Vignette _vignette;

    // Valeurs de travail pour le lissage
    private float _currentLensAmplitude;
    private float _currentVignette;
    private float _currentChromatic;
    private float _currentBlur;
    private float _currentLensOffset;

    // Ivresse partagée entre le joueur à pied et à vélo : survit quand on pose ou prend un vélo dans le niveau
    private static float s_sharedDrunk;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitSharedDrunk()
    {
        s_sharedDrunk = 0f;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Nouveau niveau, restart ou retour menu : on repart à jeun
        if (mode == LoadSceneMode.Single) s_sharedDrunk = 0f;
    }

    void Start()
    {
        if (volume.profile.TryGet(out _lensDistortion)) _lensDistortion.active = true;
        if (volume.profile.TryGet(out _chromaticAberration)) _chromaticAberration.active = true;
        if (volume.profile.TryGet(out _motionBlur)) _motionBlur.active = true;
        if (volume.profile.TryGet(out _vignette)) _vignette.active = true;

        // On reprend l'ivresse du joueur précédent, effets directement à fond (pas de fondu depuis zéro)
        drunkPercentage = s_sharedDrunk;
        _currentLensAmplitude = drunkPercentage * drunkLensDistortionAmplitude;
        _currentVignette = drunkPercentage * drunkVignetteIntensity;
        _currentChromatic = drunkPercentage * drunkChromaticAberrationIntensity;
        _currentBlur = drunkPercentage * drunkMotionBlurIntensity;
        _currentLensOffset = drunkPercentage > 0 ? 0.6f : 0f;
    }

    void Update()
    {
        drunkPercentage -= Time.deltaTime / resetDrunkTime;
        if (bikeControls != null) drunkPercentage += bikeControls.GetConsumedBoost() / 2;
        drunkPercentage = Mathf.Clamp(drunkPercentage, 0f, 1.5f);
        s_sharedDrunk = drunkPercentage;

        ApplyDrunkGlobalVolume();
        if (cameraController != null) cameraController.ApplyDrunk(drunkPercentage, transitionSpeed);
        if (bikeControls != null) bikeControls.ApplyDrunk(drunkPercentage, transitionSpeed);
        if (fpsCameraController != null) fpsCameraController.ApplyDrunk(drunkPercentage, transitionSpeed);
        if (fpsPlayerController != null) fpsPlayerController.ApplyDrunk(drunkPercentage, transitionSpeed);
    }

    public void AddDrunk(float amount)
    {
        drunkPercentage = Mathf.Clamp(drunkPercentage + amount, 0f, 1.5f);
        s_sharedDrunk = drunkPercentage;
    }

    public void ApplyDrunkGlobalVolume()
    {
        _currentLensAmplitude = Mathf.Lerp(_currentLensAmplitude, drunkPercentage * drunkLensDistortionAmplitude, Time.deltaTime * transitionSpeed);
        _currentVignette = Mathf.Lerp(_currentVignette, drunkPercentage * drunkVignetteIntensity, Time.deltaTime * transitionSpeed);
        _currentChromatic = Mathf.Lerp(_currentChromatic, drunkPercentage * drunkChromaticAberrationIntensity, Time.deltaTime * transitionSpeed);
        _currentBlur = Mathf.Lerp(_currentBlur, drunkPercentage * drunkMotionBlurIntensity, Time.deltaTime * transitionSpeed);
        float targetLensOffset = drunkPercentage > 0 ? 0.6f : 0f;
        _currentLensOffset = Mathf.Lerp(_currentLensOffset, targetLensOffset, Time.deltaTime * transitionSpeed);

        if (_vignette) _vignette.intensity.value = _currentVignette;
        if (_chromaticAberration) _chromaticAberration.intensity.value = _currentChromatic;
        if (_motionBlur) _motionBlur.intensity.value = _currentBlur;
        if (_lensDistortion) _lensDistortion.intensity.value = Mathf.Sin(Time.time * 1.5f) * _currentLensAmplitude - _currentLensOffset;
    }
}
