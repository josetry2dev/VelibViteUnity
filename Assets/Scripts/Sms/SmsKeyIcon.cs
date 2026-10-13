using UnityEngine;
using UnityEngine.UI;

public class SmsKeyIcon : MonoBehaviour
{
    [Header(" --- References --- ")]
    public RawImage image;

    public void Setup(Vector2 dir)
    {
        float rotation = 0f;
        if (dir == Vector2.up) return;
        if (dir == Vector2.left) rotation = 90f;
        if (dir == Vector2.right) rotation = -90f;
        if (dir == Vector2.down) rotation = 180f;
        transform.rotation = Quaternion.Euler(0f, 0f, rotation);
    }

    public void SetValidated()
    {
        image.color = Color.chartreuse;
    }
}
