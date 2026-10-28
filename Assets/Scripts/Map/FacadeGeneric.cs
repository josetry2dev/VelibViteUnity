using UnityEngine;

// Façade générique posée par l'import OSM. Pivot en bas au centre du mur de façade, +Z vers la rue.
// Les dimensions sont celles du mesh à l'échelle 1 : le générateur étire en X pour remplir un pâté.
public class FacadeGeneric : MonoBehaviour
{
    public float width = 12f;
    public float height = 20f;
    [Tooltip("Profondeur derrière le mur de façade")]
    public float depth = 8f;
}
