using UnityEngine;

[CreateAssetMenu(fileName = "NewProfile", menuName = "TinderGame")]
public class TinderProfile : ScriptableObject
{
    [Header(" --- Profile --- ")]
    public string profileName;
    public Sprite photo;

    [Header(" --- Gameplay --- ")]
    public bool isTrap;
}
