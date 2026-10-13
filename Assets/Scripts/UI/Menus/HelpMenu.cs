using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class HelpMenu : MonoBehaviour
{
    public GameObject ControlsDisplay;
    public GameObject DashboardDisplay;
    
    public void BackToMenu()
    {
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
