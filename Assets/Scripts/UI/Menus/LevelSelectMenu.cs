using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;

public class LevelSelectMenu : MonoBehaviour
{
    [Header(" --- Levels --- ")]
    public List<LevelInfos> allLevels;

    [Header(" --- Panels --- ")]
    public GameObject startPanel;
    public GameObject levelSelectPanel;
    public GameObject lockedLevelText;
    
    void LoadLevel(int index)
    {
        LevelInfos selectedLvl = allLevels[index];
        bool isUnlocked = PlayerPrefs.GetInt("Unlocked_" + selectedLvl.levelID, 0) == 1;
        
        if (isUnlocked)
        {
            LevelManager.RequestSkipIntro(); // Contre la montre : pas d'intro
            SceneManager.LoadScene(selectedLvl.sceneName);
        }
        else
        {
            StartCoroutine(DisplayLockedLevel(0.5f));
        }
    }
    
    IEnumerator DisplayLockedLevel(float duration)
    {
        lockedLevelText.SetActive(true);
        yield return new WaitForSecondsRealtime(duration);
        lockedLevelText.SetActive(false);
    }
    
    public void GoBack(int index)
    {
        startPanel.SetActive(true);
        levelSelectPanel.SetActive(false);
    }

    public void LoadLevel1()
    {
        LoadLevel(0);
    }
    public void LoadLevel2()
    {
        LoadLevel(1);
    }
    public void LoadLevel3()
    {
        LoadLevel(2);
    }
}
