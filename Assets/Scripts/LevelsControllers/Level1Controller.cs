using UnityEngine;

public class Level1Controller : MonoBehaviour
{
    [Header("References")]
    public SmsManager smsManager;
    
    [Header("Dialogues du Niveau")]
    public SmsDialogue tutoAccelerer;

    void Start()
    {
        Invoke("PlayIntro", 3.0f);
    }

    void PlayIntro()
    {
        smsManager.StartDialogue(tutoAccelerer);
    }
}