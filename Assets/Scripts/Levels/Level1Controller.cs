using UnityEngine;

public class Level1Controller : MonoBehaviour
{
    [Header(" --- References --- ")]
    public SmsManager smsManager;

    [Header(" --- Level Dialogues --- ")]
    public SmsDialogue tutoAccelerer;

    void Start()
    {
        Invoke(nameof(PlayIntro), 3.0f);
    }

    void PlayIntro()
    {
        smsManager.StartDialogue(tutoAccelerer);
    }
}
