using System.Collections;
using UnityEngine;

public class RandomFlash : MonoBehaviour
{
    // Référence vers la lumière
    private Light myLight;
    // Référence vers le son
    private AudioSource flashSound;

    // Paramètres pour le temps aléatoire (modifiables dans l'inspecteur)
    [Header("Timing Settings")]
    public float minWaitTime = 2.0f; // Temps minimum entre deux flashs (en secondes)
    public float maxWaitTime = 7.0f; // Temps maximum entre deux flashs (en secondes)
    
    // Paramètres pour la durée du flash lui-même
    [Header("Flash Duration")]
    public float flashDuration = 0.1f; // Temps que la lumière reste allumée
    public float flashIntensity = 10.0f; // Intensité du flash

    private void Start()
    {
        // On récupère les composants sur le même objet
        myLight = GetComponent<Light>();
        flashSound = GetComponent<AudioSource>();

        // On s'assure que la lumière est éteinte au démarrage
        myLight.intensity = 0f;

        // On lance la routine du premier flash
        StartCoroutine(FlashCoroutine());
    }

    // Une Coroutine permet de gérer des attentes dans le temps
    private IEnumerator FlashCoroutine()
    {
        // Boucle infinie (tant que l'objet est actif)
        while (true)
        {
            // 1. Attendre un temps aléatoire
            float waitTime = Random.Range(minWaitTime, maxWaitTime);
            yield return new WaitForSeconds(waitTime);

            // 2. Déclencher le flash
            PerformFlash();

            // 3. Attendre la durée du flash avant de reboucler
            yield return new WaitForSeconds(flashDuration);
            
            // 4. Éteindre la lumière
            myLight.intensity = 0f;
        }
    }

    // Fonction qui exécute l'action du flash
    private void PerformFlash()
    {
        // Allumer la lumière
        myLight.intensity = flashIntensity;

        // Jouer le son (si un son est assigné)
        if (flashSound != null && flashSound.clip != null)
        {
            // On joue le son une seule fois
            flashSound.PlayOneShot(flashSound.clip);
        }
    }
}