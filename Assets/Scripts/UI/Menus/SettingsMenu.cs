using TMPro;
using UnityEngine;

// Panel "Reglages" ouvert depuis la pause. La navigation est gérée par le MenuGeneric du panel :
// haut/bas pour choisir la ligne, gauche/droite pour modifier la valeur.
public class SettingsMenu : MonoBehaviour
{
    [Header(" --- Panels --- ")]
    public GameObject pausePanel;

    [Header(" --- Texts --- ")]
    public TextMeshProUGUI steerSensitivityText;
    public TextMeshProUGUI fovText;

    private void OnEnable()
    {
        Refresh();
    }

    public void Open()
    {
        pausePanel.SetActive(false);
        gameObject.SetActive(true);
    }

    public void Back()
    {
        GameSettings.Save();
        gameObject.SetActive(false);
        pausePanel.SetActive(true);
    }

    public void ChangeSteerSensitivity(int direction)
    {
        GameSettings.SteerSensitivity += direction * GameSettings.SteerSensitivityStep;
        Refresh();
    }

    public void ChangeFov(int direction)
    {
        GameSettings.Fov += direction * GameSettings.FovStep;
        Refresh();
    }

    // La police Gothic War n'a pas de vraies flèches (< et > affichent son logo), on les prend dans Bebas Neue
    private const string LeftArrow = "<font=\"BEBASNEUE-REGULAR SDF\"><noparse><</noparse></font>";
    private const string RightArrow = "<font=\"BEBASNEUE-REGULAR SDF\"><noparse>></noparse></font>";

    private void Refresh()
    {
        if (steerSensitivityText != null)
            steerSensitivityText.text = $"Sensibilite  {LeftArrow} {Mathf.RoundToInt(GameSettings.SteerSensitivity * 100f)}% {RightArrow}";
        if (fovText != null)
            fovText.text = $"FOV  {LeftArrow} {Mathf.RoundToInt(GameSettings.Fov)} {RightArrow}";
    }
}
