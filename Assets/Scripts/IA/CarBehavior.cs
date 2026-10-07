using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class CarBehavior : MonoBehaviour
{
    [Header("Physique")]
    public float maxSpeed = 15f;
    public float accelerationMin = 3f;
    public float accelerationMax = 6f;
    private float _acceleration = 5f;
    public float deceleration = 10f;
    
    [Header("Détection")]
    public float detectionDistance = 5f;
    public LayerMask trafficLogicLayer;
    
    public GameObject[] visuals;
    private CarVisualRefs _visualsRefs;
    
    private float _currentSpeed = 0f;
    private BoxCollider _boxCol;
    private bool _obstacleDetected = false;

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        
        // Random Visual
        var randomNumber = Random.Range(0, visuals.Length);
        visuals[0].SetActive(false); // Default visual
        visuals[randomNumber].SetActive(true);
        _visualsRefs = visuals[randomNumber].GetComponent<CarVisualRefs>();
        
        // Random Acceleration
        _acceleration = Random.Range(accelerationMin, accelerationMax);
    }
    
    void Start()
    {
        _boxCol = GetComponent<BoxCollider>();
        // On démarre à fond ou à 0 selon ton choix, ici on dit qu'elle arrive lancée
        _currentSpeed = 0; 
    }

    void Update()
    {
        CheckForObstacles();
        HandleMovement();
    }

    void CheckForObstacles()
    {
        // 1. Calculer le point exact à l'avant de la voiture
        // On prend le centre, on ajoute la moitié de la taille vers l'avant (Z local)
        // TransformPoint convertit la position locale en position Monde (gère la rotation/scale)
        Vector3 frontPos = transform.TransformPoint(_boxCol.center + new Vector3(0, 0, _boxCol.size.z / 2));

        // 2. Lancer le rayon depuis ce point
        RaycastHit hit;
        _obstacleDetected = false;

        // Note: On utilise QueryTriggerInteraction.Collide pour détecter les Triggers (Zone d'arrêt)
        if (Physics.Raycast(frontPos, transform.forward, out hit, detectionDistance, trafficLogicLayer, QueryTriggerInteraction.Collide))
        {
            // Vérifions ce qu'on a touché
            TrafficLightController light = hit.collider.GetComponentInParent<TrafficLightController>();

            if (light != null)
            {
                // Si c'est un feu : on s'arrête si c'est ROUGE ou ORANGE
                if (light.currentState == TrafficLightController.LightState.Red || 
                    light.currentState == TrafficLightController.LightState.Yellow)
                {
                    _obstacleDetected = true;
                }
            }
            else
            {
                // Si c'est autre chose (une autre voiture), on s'arrête
                _obstacleDetected = true;
            }
        }
    }

    void HandleMovement()
    {
        // 1. Calcul de la vitesse (Logique inchangée)
        // On utilise fixedDeltaTime car on est dans FixedUpdate
        float dt = Time.fixedDeltaTime;

        if (_obstacleDetected)
        {
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, deceleration * dt);
        }
        else
        {
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, maxSpeed, _acceleration * dt);
        }
        
        Vector3 nextPosition = _rb.position + transform.forward * _currentSpeed * dt;

        _rb.MovePosition(nextPosition);
        

        _visualsRefs.wheelArD.Rotate(Vector3.forward * 10*_currentSpeed * Time.deltaTime);
        _visualsRefs.wheelAvD.Rotate(Vector3.forward * 10*_currentSpeed * Time.deltaTime);
        _visualsRefs.wheelArG.Rotate(Vector3.forward * 10*_currentSpeed * Time.deltaTime);
        _visualsRefs.wheelAvG.Rotate(Vector3.forward * 10*_currentSpeed * Time.deltaTime);
    }

    void OnDrawGizmos()
    {
        if (_boxCol == null) _boxCol = GetComponent<BoxCollider>();
        
        Gizmos.color = _obstacleDetected ? Color.red : Color.green;
        Vector3 frontPos = transform.TransformPoint(_boxCol.center + new Vector3(0, 0, _boxCol.size.z / 2));
        Gizmos.DrawLine(frontPos, frontPos + transform.forward * detectionDistance);
    }
    
    public float GetSpeed()
    {
        if (_currentSpeed > 0.1f)
        {
            return (_currentSpeed);
        }
        return 0;
    }
}