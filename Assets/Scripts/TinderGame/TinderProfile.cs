using UnityEngine;

[CreateAssetMenu(fileName = "NewProfile", menuName = "TinderGame")]
public class TinderProfile : ScriptableObject
{
    public string profileName;
    public Sprite photo;
    public bool isTrap;
}