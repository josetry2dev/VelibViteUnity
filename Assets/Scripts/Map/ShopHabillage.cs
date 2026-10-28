using UnityEngine;

// Shop qui peut cacher un rideau de fer (menu FOUVELIB/Habillage des rideaux).
// cover = zone de la devanture qui doit recouvrir le rideau, dans l'espace du shop (x centré, y depuis le sol).
// Les enseignes ou frigos qui dépassent restent en dehors. Largeur 0 = toute la boîte du shop.
public class ShopHabillage : MonoBehaviour
{
    public Rect cover;

    void OnDrawGizmosSelected()
    {
        if (cover.width <= 0f)
            return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(new Vector3(cover.center.x, cover.center.y, 0f), new Vector3(cover.width, cover.height, 0.02f));
    }
}
