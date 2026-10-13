using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMenu : MonoBehaviour
{
    [Header(" --- References --- ")]
    public SmsManager smsManager;
    public StationLauncher stationLauncher;

    [Header(" --- Dialogues --- ")]
    public SmsDialogue startDialogue;

    [Header(" --- Panels --- ")]
    public GameObject startPanel;
    public GameObject levelSelectPanel;

    public void StartFirstDialogue()
    {
        smsManager.StartDialogue(startDialogue, stationLauncher.LaunchStation);
        startPanel.SetActive(false);
    }

    public void GoToLevelSelect(int index)
    {
        startPanel.SetActive(false);
        levelSelectPanel.SetActive(true);
    }

    public void GoToHelpScreen()
    {
        SceneManager.LoadScene("Scenes/00_Help");
    }
    
    public void GoToExpo()
    {
        SceneManager.LoadScene("Scenes/01_Expo");
    }
    
    public void GoToStudio()
    {
        SceneManager.LoadScene("Scenes/02_Studio");
    }
    
    public void QuitGame()
    {
        Application.Quit();
    }
}