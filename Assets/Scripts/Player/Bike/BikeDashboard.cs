using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BikeDashboard : MonoBehaviour
{
    [Header(" --- References --- ")]
    public BikeControlsBase bikeControls;
    public BikeHealthSystem bikeHealthSystem;

    [Header(" --- Speed UI --- ")]
    public TextMeshProUGUI speedText;
    public Image[] speedTierBars;

    [Header(" --- Boost UI --- ")]
    public Image boostBarImage;

    [Header(" --- Battery UI --- ")]
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
                speedTierBars[i].enabled = i < currentTier;
            }

            // Lives
            int lives = bikeHealthSystem.GetCurrentLives();

            for (int i = 0; i < batteryBars.Length; i++)
            {
                batteryBars[i].enabled = i < lives;
            }
            if (batteryBars.Length > 0) batteryBars[0].color = lives == 1 ? Color.red : Color.black;
        }
    }
}
