using UnityEngine;
using UnityEngine.SceneManagement;

public class StartMenu : MonoBehaviour
{
    [Header(" --- Panels --- ")]
    public GameObject startPanel;
    public GameObject levelSelectPanel;

    // L'anim d'intro du panel ne se joue qu'au lancement du jeu
    private static bool _introPlayed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _introPlayed = false;

    private void Start()
    {
        Animator animator = startPanel.GetComponent<Animator>();
        if (animator == null) return;

        // Garde l'état de l'anim quand on désactive/réactive le panel (aller-retour level select)
        animator.keepAnimatorStateOnDisable = true;

        if (_introPlayed)
        {
            // Retour au menu : on saute directement à la fin de l'anim
            animator.Play(animator.GetCurrentAnimatorStateInfo(0).fullPathHash, 0, 1f);
            animator.Update(0f);
        }
        _introPlayed = true;
    }

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

    public void QuitGame()
    {
        Application.Quit();
    }
}
