using UnityEngine;
using TMPro;
public class NameEntry : MonoBehaviour
{
    public TMP_InputField inputField; // assigné dans inspector ou trouvé via tag
    public GameObject quitButton;
    public GameObject encoreButton;
    
    void Start()
    {
        if (inputField == null)
            inputField = GetComponent<TMP_InputField>();
    }

    // Appelé quand le joueur appuie sur Enter / Submit
    public void OnSubmitName()
    {
        string playerName = inputField.text.Trim();

        if (string.IsNullOrEmpty(playerName))
        {
            Debug.LogWarning("Nom vide !");
            return;
        }

        // Envoie le nom au ScoreManager
        // ScoreManager.Instance.SetCurrentPlayerName(playerName);

        // Eventuellement masquer l'input
        gameObject.SetActive(false);
        quitButton.SetActive(true);
        encoreButton.SetActive(true);

        // Affiche directement le top10 maintenant qu'on a le nom
        // ScoreManager.Instance.DisplayTop10();
    }

}
