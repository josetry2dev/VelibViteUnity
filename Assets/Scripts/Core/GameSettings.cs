using System;
using UnityEngine;

// Réglages joueur sauvegardés dans les PlayerPrefs (sensibilité de direction du vélo, FOV caméra)
public static class GameSettings
{
    // --- Sensibilité de direction (multiplicateur du turnSpeed du vélo) ---
    public const float DefaultSteerSensitivity = 1f;
    public const float MinSteerSensitivity = 0.5f;
    public const float MaxSteerSensitivity = 2f;
    public const float SteerSensitivityStep = 0.1f;

    // --- FOV caméra vélo (valeur de normalFOV sur PlayerOW) ---
    public const float DefaultFov = 65f;
    public const float MinFov = 50f;
    public const float MaxFov = 90f;
    public const float FovStep = 5f;

    private const string SteerKey = "Settings_SteerSensitivity";
    private const string FovKey = "Settings_Fov";

    public static event Action Changed;

    // Menu Réglages désactivé (sensi/FOV touchent trop au gameplay) : on force les valeurs par défaut
    // et on ignore ce qui a pu être sauvegardé avant. Repasser à true pour le réactiver.
    public const bool PlayerSettingsEnabled = false;

    private static float? _steerSensitivity;
    private static float? _fov;

    public static float SteerSensitivity
    {
        get
        {
            if (!PlayerSettingsEnabled) return DefaultSteerSensitivity;
            _steerSensitivity ??= PlayerPrefs.GetFloat(SteerKey, DefaultSteerSensitivity);
            return _steerSensitivity.Value;
        }
        set
        {
            // Arrondi au dixième pour éviter les 0.70000001
            float v = Mathf.Round(Mathf.Clamp(value, MinSteerSensitivity, MaxSteerSensitivity) * 10f) / 10f;
            _steerSensitivity = v;
            PlayerPrefs.SetFloat(SteerKey, v);
            Changed?.Invoke();
        }
    }

    public static float Fov
    {
        get
        {
            if (!PlayerSettingsEnabled) return DefaultFov;
            _fov ??= PlayerPrefs.GetFloat(FovKey, DefaultFov);
            return _fov.Value;
        }
        set
        {
            float v = Mathf.Round(Mathf.Clamp(value, MinFov, MaxFov));
            _fov = v;
            PlayerPrefs.SetFloat(FovKey, v);
            Changed?.Invoke();
        }
    }

    public static void Save() => PlayerPrefs.Save();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _steerSensitivity = null;
        _fov = null;
        Changed = null;
    }
}
