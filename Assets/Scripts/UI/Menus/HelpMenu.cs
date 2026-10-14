using UnityEngine;
using UnityEngine.SceneManagement;

public class HelpMenu : MonoBehaviour
{
    [Header(" --- Displays --- ")]
    public GameObject ControlsDisplay;
    public GameObject DashboardDisplay;

    // Vrai quand on arrive ici depuis le Start du premier lancement : la sortie lance alors le tuto
    public static bool launchTutoOnExit;

    private const string HelpSeenKey = "HelpSeen";

    public static bool HasSeenHelp()
    {
        return PlayerPrefs.GetInt(HelpSeenKey, 0) == 1;
    }

    public void BackToMenu()
    {
        if (launchTutoOnExit)
        {
            launchTutoOnExit = false;
            PlayerPrefs.SetInt(HelpSeenKey, 1);
            PlayerPrefs.Save();
            SceneManager.LoadScene("Scenes/2_Tuto");
            return;
        }
        SceneManager.LoadScene("Scenes/1_Menu");
    }

    public void SetControlsDisplay()
    {
        DashboardDisplay.SetActive(false);
        ControlsDisplay.SetActive(true);
    }

    public void SetDashboardDisplay()
    {
        ControlsDisplay.SetActive(false);
        DashboardDisplay.SetActive(true);
    }
}
