using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class ScoreManager : MonoBehaviour
{
    private LevelInfos _currentLevel;
    private List<ScoreEntry> _loadedScores = new List<ScoreEntry>();
    private int _lastAddedIndex = -1; // Pour retenir quelle ligne surligner

    // --- API ---

    public void InitCurrentLevel(LevelInfos levelInfo)
    {
        _currentLevel = levelInfo;
    }
    public Medal GetMedal(float time)
    {
        if (_currentLevel == null) return Medal.None;

        if (time <= _currentLevel.goldTime) return Medal.Gold;
        if (time <= _currentLevel.silverTime) return Medal.Silver;
        if (time <= _currentLevel.bronzeTime) return Medal.Bronze;
        return Medal.None;
    }

    public bool IsTop10(float time)
    {
        LoadLevelScores();
        if (_loadedScores.Count < 10)
            return true;
        float worstTopTime = _loadedScores.Last().time;
        return time < worstTopTime;
    }

    public void SetScore(string playerName, float time)
    {
        if (_currentLevel == null) return;
        LoadLevelScores();

        ScoreEntry newEntry = new ScoreEntry { playerName = playerName, time = time };
        _loadedScores.Add(newEntry);

        // Tri par temps
        _loadedScores = _loadedScores
            .OrderBy(s => s.time)
            .Take(10)
            .ToList();

        // ON CHERCHE L'INDEX DU SCORE QU'ON VIENT D'AJOUTER
        _lastAddedIndex = _loadedScores.IndexOf(newEntry);

        SaveLevelScores();
    }

    public List<ScoreEntry> GetTop10() => _loadedScores;
    public int GetLastAddedIndex() => _lastAddedIndex;
    
    // --- LOGIQUE INTERNE ---

    private void LoadLevelScores()
    {
        string key = "Scores_" + _currentLevel.levelID;

        if (PlayerPrefs.HasKey(key))
        {
            // Charger depuis la sauvegarde
            string json = PlayerPrefs.GetString(key);
            ScoreWrapper wrapper = JsonUtility.FromJson<ScoreWrapper>(json);
            _loadedScores = wrapper.entries;
        }
        else
        {
            // Premier lancement : On clone les scores par défaut du ScriptableObject
            // On fait un "new List" pour ne pas modifier le ScriptableObject en runtime
            _loadedScores = new List<ScoreEntry>();
            foreach (var entry in _currentLevel.defaultTop10)
            {
                _loadedScores.Add(new ScoreEntry { playerName = entry.playerName, time = entry.time });
            }
            
            // On sauvegarde direct pour créer le fichier
            SaveLevelScores();
        }
    }

    private void SaveLevelScores()
    {
        string key = "Scores_" + _currentLevel.levelID;
        ScoreWrapper wrapper = new ScoreWrapper { entries = _loadedScores };
        string json = JsonUtility.ToJson(wrapper);
        PlayerPrefs.SetString(key, json);
        PlayerPrefs.Save();
    }

    [Serializable]
    private class ScoreWrapper { public List<ScoreEntry> entries; }
}