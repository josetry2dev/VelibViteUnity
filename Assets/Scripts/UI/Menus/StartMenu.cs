using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMenu : MonoBehaviour
{
    [Header(" --- Panels --- ")]
    public GameObject startPanel;
    public GameObject levelSelectPanel;

    public void StartGame()
    {
        // Premier lancement : on force le passage par l'écran d'aide avant le tuto
        if (!HelpMenu.HasSeenHelp())
        {
            HelpMenu.launchTutoOnExit = true;
            SceneManager.LoadScene("Scenes/00_Help");
            return;
        }
        SceneManager.LoadScene("Scenes/2_Tuto");
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
