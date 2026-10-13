using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DrunkManager : MonoBehaviour
{
    [Header(" --- References --- ")]
    public Volume volume;
    public CameraController cameraController;
    public BikeControlsOW bikeControls;

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

    void Start()
    {
        if (volume.profile.TryGet(out _lensDistortion)) _lensDistortion.active = true;
        if (volume.profile.TryGet(out _chromaticAberration)) _chromaticAberration.active = true;
        if (volume.profile.TryGet(out _motionBlur)) _motionBlur.active = true;
        if (volume.profile.TryGet(out _vignette)) _vignette.active = true;
    }

    void Update()
    {
        drunkPercentage -= Time.deltaTime / resetDrunkTime;
        drunkPercentage += bikeControls.GetConsumedBoost() / 2;
        drunkPercentage = Mathf.Clamp(drunkPercentage, 0f, 1.5f);

        ApplyDrunkGlobalVolume();
        cameraController.ApplyDrunk(drunkPercentage, transitionSpeed);
        bikeControls.ApplyDrunk(drunkPercentage, transitionSpeed);
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
