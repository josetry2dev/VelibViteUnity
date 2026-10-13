using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header(" --- References --- ")]
    [SerializeField] private AudioSource sfxSource;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void PlaySFX(AudioClip clip, float volume = 1f, bool randomizePitch = true)
    {
        if (clip == null) return;

        sfxSource.pitch = randomizePitch ? Random.Range(0.98f, 1.02f) : 1f;
        sfxSource.PlayOneShot(clip, volume);
    }
}
