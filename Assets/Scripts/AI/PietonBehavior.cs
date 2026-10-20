using UnityEngine;
using System.Collections; // Obligatoire pour les Coroutines

[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Rigidbody))]
public class PietonBehavior : MonoBehaviour, IMovingAgent
{
    [Header(" --- Speed Settings --- ")]
    public float maxSpeed = 2f;
    public float acceleration = 5f;
    public float deceleration = 10f;
    public float turnSpeed = 300f; // Vitesse de rotation du demi-tour

    [Header(" --- Detection --- ")]
    public float detectionDistance = 2f;
    public LayerMask obstacleLayer; // Ex: TrafficLogic, Car

    [Header(" --- References --- ")]
    public Animator anim;

    private float currentSpeed;
    private CapsuleCollider _capsuleCol;
    private Rigidbody _rb;
    
    private bool obstacleDetected;
    private bool isTurningAround; // Pour savoir si on est en train de pivoter

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _capsuleCol = GetComponent<CapsuleCollider>();
        if (anim == null) anim = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        // Si on est en train de faire demi-tour, on ne check rien d'autre
        if (isTurningAround) return;

        CheckForObstacles();
        HandleMovement();
        UpdateAnimation();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacle"))
        {
            Vector3 pushDir = collision.GetContact(0).normal;
            pushDir.y = Mathf.Max(pushDir.y, 0.5f);
            pushDir = pushDir.normalized;

            if (collision.gameObject.layer == LayerMask.NameToLayer("Floor"))
            {
                if (Mathf.Approximately(pushDir.y, 1f))
                {
                    return;
                }

                StepUp();
            }
            else StartCoroutine(TurnAroundRoutine());
        }
    }

    void CheckForObstacles()
    {
        Vector3 frontPos = transform.TransformPoint(_capsuleCol.center + new Vector3(0, 0, _capsuleCol.radius));
        
        obstacleDetected = false;

        // On lance un Raycast qui touche TOUT (LayerMask ~0)
        if (Physics.Raycast(frontPos, transform.forward, out RaycastHit hit, detectionDistance))
        {
            // 1. On vérifie si l'objet touché appartient au obstacleLayer
            // (La syntaxe (1 << layer) & mask est la façon pro de vérifier un layer)
            if (((1 << hit.collider.gameObject.layer) & obstacleLayer) != 0)
            {
                // C'est un obstacle normal (Feu rouge, etc.) -> On s'arrête
                TrafficLightController trafficLight = hit.collider.GetComponentInParent<TrafficLightController>();
                if (trafficLight != null)
                {
                    if (trafficLight.currentState == TrafficLightController.LightState.Green)
                        obstacleDetected = true;
                }
                else
                {
                    obstacleDetected = true;
                }
            }
            else
            {
                // 2. C'est un objet "inconnu" (mur, décor, limite de map) -> Demi-tour !
                StartCoroutine(TurnAroundRoutine());
            }
        }
    }

    // La Coroutine pour faire le demi-tour proprement
    IEnumerator TurnAroundRoutine()
    {
        isTurningAround = true;
        obstacleDetected = false;

        // On ralentit d'abord un peu avant de tourner
        currentSpeed = 0;
        anim.SetFloat("Speed", 0);

        // Rotation de 180 degrés
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = transform.rotation * Quaternion.Euler(0, 180, 0);
        float t = 0;

        while (t < 1f)
        {
            t += Time.deltaTime * 2f; // Ajuste le "2f" pour tourner plus ou moins vite
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null; // Attend la frame suivante
        }

        isTurningAround = false;
    }

    void HandleMovement()
    {
        if (obstacleDetected)
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * Time.deltaTime);
        else
            currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.deltaTime);

        _rb.MovePosition(_rb.position + transform.forward * (currentSpeed * Time.deltaTime));
    }

    void UpdateAnimation()
    {
        if (anim != null)
        {
            anim.SetFloat("Speed", currentSpeed / maxSpeed);
        }
    }
    
    public void StepUp()
    {
        transform.position += Vector3.up * 0.45f;
    }

    public Vector3 Velocity => isTurningAround ? Vector3.zero : transform.forward * currentSpeed;
}
