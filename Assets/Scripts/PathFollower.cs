using UnityEngine;

public class PathFollower : MonoBehaviour
{
    public TrafficNode currentNode;
    public float speed = 3f;
    public float reachDistance = 0.2f;

    void Update()
    {
        if (currentNode == null)
            return;

        Vector3 target = currentNode.transform.position;
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        float dist = Vector3.Distance(transform.position, target);

        if (dist < reachDistance)
        {
            currentNode = currentNode.nextNode;
        }
    }
}