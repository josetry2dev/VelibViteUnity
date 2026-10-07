using UnityEngine;

public class TrafficNode : MonoBehaviour
{
    [Header("Path")]
    public TrafficNode nextNode;

    [Header("Debug")]
    public Color nodeColor = Color.green;
    public Color linkColor = Color.yellow;
    public float sphereRadius = 0.2f;

    // Get direction to next node
    public Vector3 GetDirectionToNext()
    {
        if (nextNode == null)
            return Vector3.zero;

        return (nextNode.transform.position - transform.position).normalized;
    }

    // Distance to next node
    public float GetDistanceToNext()
    {
        if (nextNode == null)
            return 0f;

        return Vector3.Distance(transform.position, nextNode.transform.position);
    }

    private void OnDrawGizmos()
    {
        // Draw node
        Gizmos.color = nodeColor;
        Gizmos.DrawSphere(transform.position, sphereRadius);

        // Draw direction
        if (nextNode != null)
        {
            Gizmos.color = linkColor;
            Gizmos.DrawLine(transform.position, nextNode.transform.position);

            // Arrow (simple)
            Vector3 dir = GetDirectionToNext();
            Vector3 right = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 180 + 20, 0) * Vector3.forward;
            Vector3 left = Quaternion.LookRotation(dir) * Quaternion.Euler(0, 180 - 20, 0) * Vector3.forward;

            Gizmos.DrawLine(nextNode.transform.position, nextNode.transform.position + right * 0.5f);
            Gizmos.DrawLine(nextNode.transform.position, nextNode.transform.position + left * 0.5f);
        }
    }
}