using UnityEngine;
using UnityEngine.SceneManagement; // Important pour savoir quelle scène est chargée

public class MusicManager : MonoBehaviour
{
    // Créons un singleton pour pouvoir appeler ce manager depuis n'importe où
    public static MusicManager instance;

    private AudioSource audioSource;
    // Un dictionnaire ou une liste pour associer des noms de scènes à tes musiques
    [System.Serializable]
    public struct MusicTrack
    {
        public string sceneName;
        public AudioClip musicClip;
    }

    public MusicTrack[] musicTracks;

    private string currentScenePlaying = ""; // Pour savoir quelle musique est actuellement en cours

    private void Awake()
    {
        // Pattern Singleton standard pour s'assurer qu'il n'y a qu'un seul MusicManager
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Récupère la référence à l'AudioSource
        audioSource = GetComponent<AudioSource>();
    }

    // Méthode principale que tu appelleras depuis ton GameManager ou tes niveaux
    public void PlayMusicForScene(string sceneName)
    {
        // Cherche la musique correspondant au nom de la scène
        AudioClip clipToPlay = null;
        foreach (MusicTrack track in musicTracks)
        {
            if (track.sceneName == sceneName)
            {
                clipToPlay = track.musicClip;
                break;
            }
        }

        // Si on ne trouve pas de clip pour cette scène, on ne fait rien
        if (clipToPlay == null)
        {
            Debug.LogWarning("Aucune musique définie pour la scène : " + sceneName);
            audioSource.Stop();
            return;
        }

        // VÉRIFICATION CRUCIALE : Est-ce que cette musique est déjà en train de jouer ?
        // Si oui, on ne fait rien pour ne pas la redémarrer.
        if (currentScenePlaying == sceneName && audioSource.isPlaying)
        {
            return; 
        }

        // Sinon, on change la musique et on la lance
        currentScenePlaying = sceneName;
        audioSource.clip = clipToPlay;
        audioSource.loop = true; // Assure-toi que la musique tourne en boucle
        audioSource.Play();
    }
    
    // Petite fonction pratique pour arrêter la musique si nécessaire (par exemple, dans un menu principal silencieux)
    public void StopMusic()
    {
        audioSource.Stop();
        currentScenePlaying = "";
    }
}