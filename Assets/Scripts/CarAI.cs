using System;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public class CarAI : MonoBehaviour
{
    [Header("Navigation")]
    public Lane currentLane;
    public Lane nextLane;
    [Range(0, 1)] public float progress = 0f;
    public float lookAheadDistance = 2.0f;
    private float lateralOffset;

    [Header("Sensors (All-in-One)")]
    public LayerMask trafficLayer; // Toutes les voitures, piétons ET zones de feux ici
    public float detectionRange = 6;
    public float stopDistance = 3.5f;
    public float sphereCastRadius = 1.1f;
    private Vector3 currentSensorDir;

    [Header("Personality & Curve")]
    [Range(0, 1)] public float aggressiveness = 0.5f;
    public float curveSlowdownFactor = 0.5f;

    [Header("Settings")]
    public float maxSpeed = 10f;
    public float acceleration = 5f;
    public float brakingForce = 12f;
    public float steeringSpeed = 10f;
    
    [Header("Visuals")]
    public GameObject[] visuals;
    private CarVisualRefs _visualsRefs;
    private float wheelRadius = 0.8f;
    private float steerAngle;
    private float visualSteerAngle;

    private Rigidbody rb;
    private SplineContainer splineContainer;
    private float currentTargetSpeed;
    private bool isBraking = false;

    // Debug Variables
    private float lastDynamicRange;
    private bool sphereHit;
    private float sphereHitDist;
    private bool[] rayHitResults = new bool[3];
    private float[] rayHitDistances = new float[3];
    private Vector3[] sensorOffsets = { Vector3.zero, Vector3.right * 0.7f, -Vector3.right * 0.7f };

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        lateralOffset = UnityEngine.Random.Range(-0.5f, 0.5f);
        if (currentLane != null) splineContainer = currentLane.GetSplineContainer();
        
        // Random Visual
        var randomNumber = UnityEngine.Random.Range(0, visuals.Length);
        visuals[0].SetActive(false); // Default visual
        visuals[randomNumber].SetActive(true);
        _visualsRefs = visuals[randomNumber].GetComponent<CarVisualRefs>();
    }

    private void Update()
    {
        UpdateWheelAnimations();
    }

    void FixedUpdate()
    {
        if (currentLane == null) return;

        currentTargetSpeed = maxSpeed;
        isBraking = false;

        // 1. On calcule d'abord la destination pour savoir vers où regarder
        Vector3 navDirection = CalculateNavigationDirection();

        // 2. On passe cette direction aux capteurs
        CheckSensors(navDirection); 
    
        // 3. On applique le mouvement
        MoveVehicle(navDirection);
    }

    void CheckSensors(Vector3 sensorDir)
    {
        currentSensorDir = sensorDir; // Stockage pour le debug
        float speedFactor = rb.linearVelocity.magnitude / maxSpeed;
        lastDynamicRange = detectionRange * (1f + speedFactor);

        RaycastHit hit;
        Vector3 origin = transform.position + transform.up * 0.5f;
    
        // On calcule le vecteur "perpendiculaire" à la direction du regard pour décaler les rayons
        // (L'équivalent de transform.right mais orienté vers la cible)
        Vector3 sensorRight = Vector3.Cross(Vector3.up, sensorDir).normalized;

        // 1. SPHERE CAST (Orienté vers la cible)
        sphereHit = Physics.SphereCast(origin, sphereCastRadius, sensorDir, out hit, lastDynamicRange * 0.5f, trafficLayer, QueryTriggerInteraction.Collide);
        if (sphereHit)
        {
            if (ShouldStopForObject(hit))
            {
                sphereHitDist = hit.distance;
                ApplyObstacleSlowdown(hit.distance, lastDynamicRange * 0.5f);
            }
            else { sphereHit = false; }
        }

        // 2. RAYCAST ARRAY (Orienté vers la cible)
        for (int i = 0; i < sensorOffsets.Length; i++)
        {
            // On décale l'origine du rayon en utilisant notre nouveau sensorRight
            Vector3 rayOffset = sensorRight * sensorOffsets[i].x; 
            Vector3 rayOrigin = origin + rayOffset;

            rayHitResults[i] = Physics.Raycast(rayOrigin, sensorDir, out hit, lastDynamicRange, trafficLayer, QueryTriggerInteraction.Collide);
        
            if (rayHitResults[i])
            {
                if (ShouldStopForObject(hit))
                {
                    rayHitDistances[i] = hit.distance;
                    ApplyObstacleSlowdown(hit.distance, lastDynamicRange);
                }
                else { rayHitResults[i] = false; }
            }
            else { rayHitDistances[i] = lastDynamicRange; }
        }
    }
    
    Vector3 CalculateNavigationDirection()
    {
        // WORLD -> LOCAL
        float3 localPos = splineContainer.transform.InverseTransformPoint(transform.position);
        SplineUtility.GetNearestPoint(splineContainer.Spline, localPos, out _, out float t);
        progress = t;

        // LOOK AHEAD
        float lookAheadT = Mathf.Clamp01(progress + (lookAheadDistance / splineContainer.CalculateLength()));
        float3 targetLocalPos = splineContainer.Spline.EvaluatePosition(lookAheadT);
        Vector3 targetWorldPos = splineContainer.transform.TransformPoint(targetLocalPos);
    
        // Ajout du décalage latéral
        targetWorldPos += transform.right * lateralOffset;

        Vector3 dir = (targetWorldPos - transform.position);
        dir.y = 0;
    
        if (dir.sqrMagnitude < 0.001f) return transform.forward;
        return dir.normalized;
    }
    
    // --- LE CERVEAU DE DÉCISION ---
    bool ShouldStopForObject(RaycastHit hit)
    {
        // On check si l'objet touché est un feu
        var light = hit.collider.GetComponentInParent<TrafficLightController>();
        
        if (light != null)
        {
            // Si c'est un feu, on ne s'arrête QUE si c'est Rouge ou Orange
            bool stopForLight = (light.currentState == TrafficLightController.LightState.Red || 
                                 light.currentState == TrafficLightController.LightState.Yellow);
            
            // Un conducteur très agressif peut ignorer le feu rouge (0.98+)
            if (aggressiveness > 0.98f) return false;

            return stopForLight;
        }

        // Si ce n'est pas un feu (donc une voiture, un mur, un piéton), on s'arrête d'office
        return true;
    }

    void ApplyObstacleSlowdown(float distance, float range)
    {
        // On ajoute un tampon de sécurité basé sur la vitesse actuelle (ex: 2m de plus tous les 10km/h)
        float dynamicStopDistance = stopDistance + (rb.linearVelocity.magnitude * 1.5f); 

        float ratio = (distance - dynamicStopDistance) / (range - dynamicStopDistance);
        float speed = Mathf.Lerp(0, maxSpeed, Mathf.Clamp01(ratio));
    
        if (speed < currentTargetSpeed) currentTargetSpeed = speed;
        if (distance < dynamicStopDistance + 1f) isBraking = true;
    }

    void MoveVehicle(Vector3 navDir)
    {
        if (progress >= 0.80f && nextLane == null) 
        {
            GetNextLane(); 
        }
        if (progress >= 0.98f) 
        {
            SwitchToNextLane();
            return; 
        }

        // --- 2. RALENTISSEMENT EN VIRAGE ---
        float3 currentTangent = splineContainer.EvaluateTangent(progress);
    
        float lookAheadT = Mathf.Clamp01(progress + (lookAheadDistance / splineContainer.CalculateLength()));
        float3 futureTangent = splineContainer.EvaluateTangent(lookAheadT);
    
        // On calcule l'angle entre la direction actuelle et le futur
        float curveAngle = Vector3.Angle((Vector3)currentTangent, (Vector3)futureTangent);
    
        // Calcul du multiplicateur de virage (ex: si angle = 90°, on tombe à ~30% de la vitesse)
        // Plus curveSlowdownFactor est haut, plus la voiture pile dans les virages
        float curveMultiplier = Mathf.Clamp(1f - (curveAngle * curveSlowdownFactor / 90f), 0.3f, 1f);
    
        // On applique ce multiplicateur à la vitesse déjà calculée par les capteurs
        float finalTargetSpeed = currentTargetSpeed * curveMultiplier;

        // --- 3. ROTATION PHYSIQUE ---
        // On oriente la voiture vers la direction de navigation (navDir) calculée plus tôt
        if (navDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(navDir);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, steeringSpeed * Time.fixedDeltaTime));
        }
        steerAngle = Vector3.SignedAngle(transform.forward, navDir, transform.up);
        visualSteerAngle = Mathf.Clamp(steerAngle, -30f, 30f);

        // --- 4. APPLICATION DE LA VITESSE ---
        ApplyVelocity(finalTargetSpeed);
    }

    void ApplyVelocity(float speedLimit)
    {
        // Passage en vitesse locale pour manipuler les axes facilement
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
    
        // On supprime toute dérive latérale (la voiture ne glisse pas sur les côtés)
        localVel.x = 0f;

        // Calcul de l'accélération (plus forte si on freine ou si on est agressif)
        float accel = isBraking ? brakingForce : acceleration;
        float finalAccel = accel * (0.5f + aggressiveness);

        // On tend vers la vitesse cible (speedLimit prend en compte les obstacles ET les virages)
        localVel.z = Mathf.MoveTowards(localVel.z, speedLimit, finalAccel * Time.fixedDeltaTime);
        
        // On réapplique la vitesse en coordonnées Monde
        rb.linearVelocity = transform.TransformDirection(localVel);
    }
    
    void UpdateWheelAnimations()
    {
        float speed = rb.linearVelocity.magnitude;
        float rotationDegrees = (speed / wheelRadius) * Mathf.Rad2Deg * Time.deltaTime;

        // On fait tourner chaque roue
        foreach (Transform wheel in _visualsRefs.wheels)
        {
            wheel.Rotate(Vector3.forward, rotationDegrees);
        }
    }

    void GetNextLane() { if (currentLane.nextIntersection != null) nextLane = currentLane.nextIntersection.GetNextLane(currentLane); }
    void SwitchToNextLane() { if (nextLane != null) { currentLane = nextLane; splineContainer = currentLane.GetSplineContainer(); progress = 0; nextLane = null; } }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || rb == null) return;
        Vector3 origin = transform.position + transform.up * 0.5f;
    
        // On recalcule sensorRight pour le dessin des offsets
        Vector3 sensorRight = Vector3.Cross(Vector3.up, currentSensorDir).normalized;

        // Gizmo Sphere
        Gizmos.color = sphereHit ? Color.red : Color.green;
        Vector3 sphereEnd = origin + currentSensorDir * (sphereHit ? sphereHitDist : lastDynamicRange * 0.5f);
        Gizmos.DrawWireSphere(sphereEnd, sphereCastRadius);

        // Gizmos Raycasts
        for (int i = 0; i < sensorOffsets.Length; i++)
        {
            Gizmos.color = rayHitResults[i] ? Color.red : Color.cyan;
            Vector3 rOrigin = origin + (sensorRight * sensorOffsets[i].x);
            Gizmos.DrawLine(rOrigin, rOrigin + currentSensorDir * rayHitDistances[i]);
        }
    }
}