using UnityEngine;
using System.Collections.Generic;
using System;

[Serializable] 
public class ScoreEntry
{
    public string playerName;
    public float time;
}
[CreateAssetMenu(fileName = "NewLevelInfo", menuName = "GameManager/Level Info")]
public class LevelInfos : ScriptableObject
{
    public string levelID;
    public string sceneName;
    public string levelName;
    public string departure;
    public string arrival;
    public string kilometers;
    
    [Header("Medal Thresholds (Seconds)")]
    public float goldTime;
    public float silverTime;
    public float bronzeTime;
    
    [Header("Default Top 10")]
    public List<ScoreEntry> defaultTop10 = new List<ScoreEntry>();
}

public enum Medal { None, Bronze, Silver, Gold }