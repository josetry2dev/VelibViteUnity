using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class StartMenu : MonoBehaviour
{
    public SmsManager smsManager;
    public SmsDialogue startDialogue;
    public GameObject startPanel;
    public GameObject levelSelectPanel;
    public StationLauncher stationLauncher;
    
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