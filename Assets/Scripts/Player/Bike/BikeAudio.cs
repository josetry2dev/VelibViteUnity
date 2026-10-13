using UnityEngine;

public class BikeAudio : MonoBehaviour
{
    [Header(" --- References --- ")]
    public BikeControlsBase bikeControls;
    public AudioSource motorSource;
    public AudioSource brakeSource;

    [Header(" --- Motor Settings --- ")]
    public float minPitch = 0.2f;
    public float maxPitch = 1.5f;
    public float maxSpeed = 25f; // Vitesse à laquelle le pitch est au max
    public float motorBaseVolume = 0.1f;

    [Header(" --- Brake Settings --- ")]
    public float brakeFadeSpeed = 5f; // Vitesse à laquelle le son de frein apparaît/disparaît

    private float _targetBrakeVolume;

    private void Update()
    {
        // UpdateMotorSound();
        UpdateBrakeSound();
    }

    private void UpdateMotorSound()
    {
        // 1. On récupère la vitesse actuelle (on utilise la magnitude du vecteur vitesse)
        float currentSpeed = bikeControls.GetSpeedForUI();

        // 2. On calcule le ratio de vitesse (0 à 1)
        float speedRatio = Mathf.Clamp01(currentSpeed / maxSpeed);

        // 3. Le PITCH : Plus on va vite, plus le moteur électrique siffle aigu
        motorSource.pitch = Mathf.Lerp(minPitch, maxPitch, speedRatio);

        // 4. LE VOLUME : Le moteur fait un peu plus de bruit quand il tourne vite
        // On peut aussi mixer ça avec l'input d'accélération pour plus de réalisme
        motorSource.volume = Mathf.Lerp(0.1f, motorBaseVolume, speedRatio);
    }

    private void UpdateBrakeSound()
    {
        // On détecte si on freine (à adapter selon ton système d'input)
        // Exemple : si on appuie sur S ou la gâchette gauche
        bool isBraking = bikeControls.IsBoosting();
        float currentSpeed = bikeControls.GetSpeedForUI();

        // On ne veut le son de frein que si on appuie ET qu'on bouge
        if (isBraking && currentSpeed > 0.5f)
        {
            _targetBrakeVolume = 0.6f;
            // Optionnel : le pitch du frein baisse légèrement quand on ralentit
            brakeSource.pitch = Mathf.Lerp(0.9f, 1.1f, currentSpeed / 10f);
        }
        else
        {
            _targetBrakeVolume = 0f;
        }

        // On smooth le volume pour éviter les coupures sèches (le fameux "Pop" audio)
        brakeSource.volume = Mathf.MoveTowards(brakeSource.volume, _targetBrakeVolume, brakeFadeSpeed * Time.deltaTime);
    }
}