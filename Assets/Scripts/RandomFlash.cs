using System.Collections;
using UnityEngine;
public class RandomFlash : MonoBehaviour
{
    private Light myLight;
    private AudioSource flashSound;

    [Header("Timing Settings")]
    public float minWaitTime = 2.0f;
    public float maxWaitTime = 7.0f;
    
    [Header("Flash Duration")]
    public float flashDuration = 0.1f;
    public float flashIntensity = 10.0f;

    // ON GARDE UNE RÉFÉRENCE POUR POUVOIR L'ARRÊTER
    private Coroutine flashRoutine;

    private void Awake()
    {
        myLight = GetComponent<Light>();
        flashSound = GetComponent<AudioSource>();
        myLight.intensity = 0f;
    }

    // On utilise OnEnable pour lancer la coroutine quand l'objet est activé
    private void OnEnable()
    {
        // SÉCURITÉ : On arrête toute coroutine précédente potentiellement en cours
        // (utile surtout si le script ou l'objet est désactivé/réactivé)
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }
        
        // On lance la coroutine et on stocke sa référence
        flashRoutine = StartCoroutine(FlashCoroutine());
    }

    // On utilise OnDisable pour arrêter la coroutine quand l'objet est désactivé
    private void OnDisable()
    {
        // Propre : on arrête la coroutine pour que la boucle s'arrête
        // et on éteint la lumière par sécurité.
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }
        
        if (myLight != null)
        {
            myLight.intensity = 0f;
        }
    }

    private IEnumerator FlashCoroutine()
    {
        // S'assurer que la lumière est éteinte au début du cycle
        myLight.intensity = 0f;

        while (true)
        {
            float waitTime = Random.Range(minWaitTime, maxWaitTime);
            yield return new WaitForSeconds(waitTime);

            PerformFlash();

            yield return new WaitForSeconds(flashDuration);
            
            myLight.intensity = 0f;
        }
    }

    private void PerformFlash()
    {
        // Vérification de sécurité au cas où myLight soit null
        if(myLight == null) return;

        myLight.intensity = flashIntensity;

        if (flashSound != null && flashSound.clip != null)
        {
            flashSound.PlayOneShot(flashSound.clip);
        }
    }
}