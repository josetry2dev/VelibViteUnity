using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BikeDashboard : MonoBehaviour
{
    [Header("References")]
    public BikeControlsBase bikeControls;
    public BikeHealthSystem bikeHealthSystem;

    [Header("UI Elements")]
    public TextMeshProUGUI speedText;
    public Image boostBarImage;
    
    public Image[] speedTierBars;
    public Image[] batteryBars;

    void Update()
    {
        if (bikeControls == null) return;

        float speed = bikeControls.GetSpeedForUI();
        speedText.text = speed.ToString("F0");

        if (bikeControls is BikeControlsOW owScript)
        {
            // Speed
            boostBarImage.fillAmount = owScript.GetBoostRatio();
            int currentTier = owScript.GetTierLevel();

            for (int i = 0; i < speedTierBars.Length; i++)
            {
                if (i < currentTier) speedTierBars[i].enabled = true;
                else speedTierBars[i].enabled = false;
            }
            
            // Lives 
            int lives = bikeHealthSystem.GetCurrentLives();
            
            for (int i = 0; i < batteryBars.Length; i++)
            {
                if (i < lives) batteryBars[i].enabled = true;
                else batteryBars[i].enabled = false;
                
                if (lives == 1) batteryBars[0].color = Color.red;
                else batteryBars[0].color = Color.black;
            }
        }
    }
}