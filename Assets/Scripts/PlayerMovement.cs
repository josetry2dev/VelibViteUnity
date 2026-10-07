    using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    
    public Rigidbody rb;
    public float speed = 500f;
    public float turn_speed = 100f;
    
    PlayerInput  playerInput;
    InputAction moveAction;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions["Move"];
        Debug.Log("Macron explosion");
    }

    void FixedUpdate()
    {
        rb.AddForce(0,0,speed * Time.deltaTime, ForceMode.VelocityChange);
        Move();
    }

    void Move()
    {
        Vector2 direction = moveAction.ReadValue<Vector2>();
        transform.position += new Vector3(direction.x, 0, 0) * turn_speed * Time.deltaTime;
    }
}
