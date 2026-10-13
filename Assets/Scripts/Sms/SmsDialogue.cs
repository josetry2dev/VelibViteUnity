using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NouveauDialogue", menuName = "SMS/Dialogue")]
public class SmsDialogue : ScriptableObject
{
    public List<SmsMessage> messages;
}

[System.Serializable]
public class SmsMessage
{
    public string senderName;
    [TextArea] public string content;
    public bool isPlayer; // Si vrai, le joueur doit "taper" ce message
    public float tempo = 2.0f;
    public float timeOut = 0f; // 0 = pas de limite, sinon temps imparti
    public UnityEngine.Events.UnityEvent onTimeout; // Action si le temps est écoulé
}
