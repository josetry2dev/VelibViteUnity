using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class CarBehavior : MonoBehaviour
{
    [Header(" --- Speed Settings --- ")]
    public float maxSpeed = 15f;
    public float accelerationMin = 3f;
    public float accelerationMax = 6f;
    public float deceleration = 10f;

    [Header(" --- Detection --- ")]
    public float detectionDistance = 5f;
    public LayerMask trafficLogicLayer;

    [Header(" --- Visuals --- ")]
    public GameObject[] visuals;

    private float _acceleration = 5f;
    private float _currentSpeed;
    private bool _obstacleDetected;
    private BoxCollider _boxCol;
    private Rigidbody _rb;
    private CarVisualRefs _visualsRefs;

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
        _obstacleDetected = false;

        // Note: On utilise QueryTriggerInteraction.Collide pour détecter les Triggers (Zone d'arrêt)
        if (Physics.Raycast(frontPos, transform.forward, out RaycastHit hit, detectionDistance, trafficLogicLayer, QueryTriggerInteraction.Collide))
        {
            // Vérifions ce qu'on a touché
            TrafficLightController trafficLight = hit.collider.GetComponentInParent<TrafficLightController>();

            if (trafficLight != null)
            {
                // Si c'est un feu : on s'arrête si c'est ROUGE ou ORANGE
                if (trafficLight.currentState == TrafficLightController.LightState.Red ||
                    trafficLight.currentState == TrafficLightController.LightState.Yellow)
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

        Vector3 nextPosition = _rb.position + transform.forward * (_currentSpeed * dt);

        _rb.MovePosition(nextPosition);

        Vector3 wheelRotation = Vector3.forward * (10 * _currentSpeed * Time.deltaTime);
        _visualsRefs.wheelArD.Rotate(wheelRotation);
        _visualsRefs.wheelAvD.Rotate(wheelRotation);
        _visualsRefs.wheelArG.Rotate(wheelRotation);
        _visualsRefs.wheelAvG.Rotate(wheelRotation);
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
            return _currentSpeed;
        }
        return 0;
    }
}
