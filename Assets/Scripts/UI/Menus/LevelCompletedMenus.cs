using System;
using UnityEngine;
using TMPro;
using Random = UnityEngine.Random;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Collections;

public class LevelCompletedMenus : MonoBehaviour
{
    [Header(" --- References --- ")]
    public ScoreManager scoreManager;
    public GameObject nameSelectorPanel;

    [Header(" --- Input Settings --- ")]
    public float submitDelay;

    [Header(" --- Level Completed 1 : Panel --- ")]
    public GameObject levelCompleted1Panel;
    public TMP_Text velibNumberTxt;
    public TMP_Text dateTxt;
    public TMP_Text levelDepartureTxt;
    public TMP_Text levelArrivalTxt;
    public TMP_Text timeTxt;
    public TMP_Text kilometersTxt;
    public TMP_Text averageSpeedTxt;

    [Header(" --- Level Completed 1 : Medals --- ")]
    public GameObject goldMedal;
    public GameObject silverMedal;
    public GameObject bronzeMedal;

    [Header(" --- Level Completed 2 : Top 10 --- ")]
    public GameObject levelCompleted2Panel;
    public TextMeshProUGUI levelNameTxt;
    public TextMeshProUGUI[] top10Entries;
    public Color highlightColor = Color.red;
    public Color normalColor = Color.black;

    [Header(" --- Player --- ")]
    public string playerName;

    private float _elapsedTime;
    private LevelInfos _levelInfos;

    private void Update()
    {
        if (submitDelay > 0.001f)
        {
            submitDelay -= Time.unscaledDeltaTime;
        }
    }

    public void LevelCompleted(LevelInfos levelInfos, float elapsedTime)
    {
        _levelInfos = levelInfos;
        _elapsedTime = elapsedTime;
        
        MarkLevelAsUnlocked(levelInfos.levelID);
        
        submitDelay = 1f;
        StartCoroutine(SlowMoToPause(1f));
        SetLevelCompletedPanel1();
    }
    public void Panel1ToPanel2()
    {
        levelCompleted1Panel.SetActive(false);
        if (scoreManager.IsTop10(_elapsedTime))
        {
            nameSelectorPanel.SetActive(true);
        }    
        else
        {
            submitDelay = 1f;
            SetLevelCompletedPanel2();
        }
    }

    public void NextLevel()
    {
        if (SceneManager.GetActiveScene().buildIndex != SceneManager.sceneCountInBuildSettings-1)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
            Time.timeScale = 1f;
        }
    }

    public void Retry()
    {
        LevelManager.Instance.Restart();
    }
    
    public void SetLevelCompletedPanel1()
    {
        switch (scoreManager.GetMedal(_elapsedTime))
        {
            case Medal.Bronze:
                bronzeMedal.SetActive(true);
                silverMedal.SetActive(false);
                goldMedal.SetActive(false);
                break;
            case Medal.Silver:
                bronzeMedal.SetActive(false);
                silverMedal.SetActive(true);
                goldMedal.SetActive(false);
                break;
            case Medal.Gold:
                bronzeMedal.SetActive(false);
                silverMedal.SetActive(false);
                goldMedal.SetActive(true);
                break;
        }
        timeTxt.text = _elapsedTime.ToString("0.00");
        dateTxt.text = DateTime.Now.Month + "." + DateTime.Now.Day;
        // averageSpeedTxt.text = flowManager.getAverageSpeed().ToString("0.00");
        averageSpeedTxt.text = "TODO";
        levelArrivalTxt.text = _levelInfos.arrival;
        levelDepartureTxt.text = _levelInfos.departure;
        kilometersTxt.text = _levelInfos.kilometers;
        velibNumberTxt.text = Random.Range(1, 9999).ToString();
        levelCompleted1Panel.SetActive(true);
    }
    
    public void SetLevelCompletedPanel2()
    {
        levelNameTxt.text = _levelInfos.levelName;
    
        List<ScoreEntry> scores = scoreManager.GetTop10();
        int highlightIndex = scoreManager.GetLastAddedIndex();

        for (int i = 0; i < top10Entries.Length; i++)
        {
            if (i < scores.Count)
            {
                top10Entries[i].text = $"{i + 1}. {scores[i].playerName} - {scores[i].time:0.00}s";
            
                // COULEUR : Si c'est le score qu'on vient de faire, on surligne
                top10Entries[i].color = (i == highlightIndex) ? highlightColor : normalColor;
                top10Entries[i].fontStyle = (i == highlightIndex) ? FontStyles.Bold : FontStyles.Normal;
            }
            else
            {
                top10Entries[i].text = $"{i + 1}. ---";
                top10Entries[i].color = normalColor;
            }
        }

        levelCompleted2Panel.SetActive(true);
    }
    
    public void NameSelected(string nameSelected)
    {
        playerName = nameSelected;
        scoreManager.SetScore(playerName, _elapsedTime);
        nameSelectorPanel.SetActive(false);
        SetLevelCompletedPanel2();
    }
    
    IEnumerator SlowMoToPause(float duration)
    {
        float startTimeScale = 1f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;

            Time.timeScale = Mathf.Lerp(startTimeScale, 0f, t / duration);

            yield return null;
        }

        Time.timeScale = 0f;
    }
    
    private static void MarkLevelAsUnlocked(string levelID)
    {
        PlayerPrefs.SetInt("Unlocked_" + levelID, 1);
        PlayerPrefs.Save();
    }
}
