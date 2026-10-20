using UnityEngine;

// PNJ qui se déplacent (voitures, piétons) : utilisé pour juger les esquives du joueur
public interface IMovingAgent
{
    Vector3 Velocity { get; }
}
