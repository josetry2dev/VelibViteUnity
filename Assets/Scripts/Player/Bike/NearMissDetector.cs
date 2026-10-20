using System;
using System.Collections.Generic;
using UnityEngine;

// Détecte les esquives : on frôle une voiture ou un piéton qui bouge dans une autre direction que nous, sans le toucher.
// Chaque PNJ qui entre dans la zone ouvre un "passage" ; quand il en sort, le passage est jugé sur l'instant où il était le plus proche.
public class NearMissDetector : MonoBehaviour
{
    [Header(" --- References --- ")]
    public BikeControlsOW bikeControls;

    [Header(" --- Zone --- ")]
    [Tooltip("Rayon de suivi des PNJ autour du vélo")]
    public float detectionRadius = 4f;
    [Tooltip("Distance max (centre du vélo -> bord du PNJ, à plat) pour que ça compte comme un frôlement")]
    public float nearMissDistance = 1.6f;
    public LayerMask agentLayers = ~0;
    [Tooltip("Hauteur du centre de détection au-dessus du pivot du vélo")]
    public float centerHeight = 0.8f;

    [Header(" --- Conditions --- ")]
    [Tooltip("Vitesse min du vélo pour qu'une esquive compte")]
    public float minBikeSpeed = 5f;
    public float minCarSpeed = 3f;
    public float minPietonSpeed = 0.5f;
    [Tooltip("Écart d'angle min entre notre direction et celle du PNJ (on ne valide pas un dépassement tranquille)")]
    public float minAngle = 30f;

    // closeness : 0 = au bord de la zone de frôlement, 1 = au contact
    public event Action<float> Dodged;

    private class Pass
    {
        public float minDistance = float.MaxValue;
        public bool validAtClosest;
        public bool touched;
        public bool seen;
    }

    private readonly Dictionary<Component, Pass> _passes = new Dictionary<Component, Pass>();
    private readonly List<Component> _ended = new List<Component>();
    private readonly Collider[] _hits = new Collider[32];

    void Awake()
    {
        if (bikeControls == null) bikeControls = GetComponent<BikeControlsOW>();
    }

    void OnDisable()
    {
        _passes.Clear();
    }

    void FixedUpdate()
    {
        // Crash/respawn en cours : on oublie tout ce qui était en train de passer
        if (!bikeControls.enabled)
        {
            _passes.Clear();
            return;
        }

        Vector3 center = transform.position + Vector3.up * centerHeight;
        foreach (var pass in _passes.Values) pass.seen = false;

        int count = Physics.OverlapSphereNonAlloc(center, detectionRadius, _hits, agentLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var agent = _hits[i].GetComponentInParent<IMovingAgent>();
            if (agent == null) continue;

            var key = (Component)agent;
            if (!_passes.TryGetValue(key, out var pass))
            {
                pass = new Pass();
                _passes.Add(key, pass);
            }
            pass.seen = true;

            Vector3 offset = _hits[i].ClosestPoint(center) - center;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance < pass.minDistance)
            {
                pass.minDistance = distance;
                pass.validAtClosest = IsDodgeContext(agent);
            }
        }

        // Les PNJ sortis de la zone (ou détruits) : on juge le passage
        _ended.Clear();
        foreach (var entry in _passes)
        {
            if (entry.Key == null || !entry.Value.seen) _ended.Add(entry.Key);
        }
        foreach (var key in _ended)
        {
            var pass = _passes[key];
            _passes.Remove(key);
            if (key != null && !pass.touched && pass.validAtClosest && pass.minDistance <= nearMissDistance)
                Dodged?.Invoke(1f - pass.minDistance / nearMissDistance);
        }
    }

    // Un contact pendant le passage l'annule (le bonk/crash s'occupe de la sanction)
    void OnCollisionEnter(Collision collision)
    {
        var agent = collision.collider.GetComponentInParent<IMovingAgent>();
        if (agent == null) return;

        var key = (Component)agent;
        if (!_passes.TryGetValue(key, out var pass))
        {
            pass = new Pass { seen = true };
            _passes.Add(key, pass);
        }
        pass.touched = true;
    }

    private bool IsDodgeContext(IMovingAgent agent)
    {
        if (bikeControls.GetCurrentSpeed() < minBikeSpeed) return false;

        Vector3 velocity = agent.Velocity;
        velocity.y = 0f;
        float minSpeed = agent is CarBehavior ? minCarSpeed : minPietonSpeed;
        if (velocity.magnitude < minSpeed) return false;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        return Vector3.Angle(forward, velocity) >= minAngle;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position + Vector3.up * centerHeight;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, detectionRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, nearMissDistance);
    }
}
