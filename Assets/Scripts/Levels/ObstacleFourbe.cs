using System;
using UnityEngine;

public class ObstacleFourbe : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            transform.position -= new Vector3(0, 0.1f, 0);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
