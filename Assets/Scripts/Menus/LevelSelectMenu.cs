using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;
using TMPro;

public class LevelSelectMenu : MonoBehaviour
{
    public List<LevelInfos> allLevels;
    public GameObject startPanel;
    public GameObject levelSelectPanel;
    public GameObject lockedLevelText;
    
    void LoadLevel(int  index)
    {
        LevelInfos selectedLvl = allLevels[index];
        bool isUnlocked = GameManager.Instance.IsLevelUnlocked(selectedLvl.levelID);
        
        if (isUnlocked)
        {
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
