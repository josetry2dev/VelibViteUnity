using System;
using UnityEngine;

// Note de style de conduite façon Devil May Cry : une jauge d'aura continue (0 à 5 rangs)
// qui monte quand on roule vite et qu'on drifte proprement, et qui baisse quand on traîne ou qu'on freine.
public class AuraManager : MonoBehaviour
{
    [Header(" --- References --- ")]
    public BikeControlsOW bikeControls;
    [Tooltip("Trouvé automatiquement dans la scène si vide (il vit dans le GameManager)")]
    public FlowRankDisplay rankDisplay;
    [Tooltip("Messages +/- au-dessus du rang. Trouvé automatiquement dans la scène si vide")]
    public AuraFeed auraFeed;

    [Header(" --- Jauge --- ")]
    [Tooltip("Points d'aura par rang : D = [0, 100[, C = [100, 200[ ... S = [400, 500]")]
    public float pointsPerRank = 100f;
    [Tooltip("Aura de départ (150 = milieu du C : on peut monter ou descendre direct)")]
    public float startAura = 150f;
    [Tooltip("Marge sous le seuil avant de perdre un rang (évite que la lettre clignote)")]
    public float rankDownMargin = 10f;
    [Tooltip("Perte d'aura par seconde selon le rang (D, C, B, A, S) : plus on est haut, plus il faut envoyer")]
    public float[] decayPerRank = { 0f, 2f, 4f, 7f, 10f };

    [Header(" --- Vitesse --- ")]
    [Tooltip("Temps de lissage de la vitesse (s) : un petit ralentissement ponctuel ne punit pas direct")]
    public float speedSmoothTime = 2f;
    [Tooltip("X : vitesse lissée / maxSpeed (1 = palier max, 2.5 = boost max). Y : aura gagnée par seconde")]
    public AnimationCurve speedAuraCurve = new AnimationCurve(
        new Keyframe(0f, -15f),
        new Keyframe(0.4f, -5f),
        new Keyframe(0.6f, 0f),
        new Keyframe(0.8f, 6f),
        new Keyframe(1f, 12f),
        new Keyframe(1.5f, 20f),
        new Keyframe(2.5f, 30f));

    [Header(" --- Drift --- ")]
    [Tooltip("Vitesse minimale pour qu'un drift compte")]
    public float minDriftSpeed = 4f;
    [Tooltip("Rotation minimale (degrés) pour valider un drift, sinon c'est juste un freinage")]
    public float minDriftAngle = 30f;
    public float minDriftDuration = 0.3f;
    public float driftBaseBonus = 10f;
    public float driftBonusPerSecond = 15f;
    public float driftBonusPerDegree = 0.15f;
    public float maxDriftBonus = 100f;

    [Header(" --- Freinage --- ")]
    [Tooltip("Aura perdue par seconde de frein (frein normal ou drift en ligne droite)")]
    public float brakePenaltyPerSecond = 20f;
    [Tooltip("En dessous de cette vitesse, freiner ne coûte rien (arrêt au feu, manœuvre)")]
    public float minBrakePenaltySpeed = 2f;
    [Tooltip("Perte minimale cumulée sur un coup de frein pour afficher un message")]
    public float minBrakeMessage = 3f;

    [Header(" --- Chocs --- ")]
    [Tooltip("Aura perdue sur un bonk (obstacle à faible vitesse, voiture, piéton...)")]
    public float bonkPenalty = 45f;
    [Tooltip("Crash : on redescend d'un rang, et on garde ce nombre de points dans le nouveau rang (D reste D)")]
    public float crashKeepInRank = 20f;

    [Header(" --- Esquive --- ")]
    public float dodgeBaseBonus = 15f;
    [Tooltip("Bonus en plus quand on passe au ras du PNJ (proportionnel à la proximité)")]
    public float dodgeClosenessBonus = 20f;

    [Header(" --- Debug Info (Read Only) --- ")]
    [SerializeField] private float aura;
    [SerializeField] private FlowRank rank;
    [SerializeField] private float smoothedSpeedRatio;
    [SerializeField] private float driftDuration;
    [SerializeField] private float driftAngle;

    public float Aura => aura;
    public FlowRank Rank => rank;
    public float MaxAura => pointsPerRank * 5f;
    // Remplissage du rang en cours (0 à 1), pratique pour une future jauge
    public float RankProgress => rank == FlowRank.S ? 1f : Mathf.Clamp01((aura - (int)rank * pointsPerRank) / pointsPerRank);

    public event Action<FlowRank> OnRankChanged;
    // Gains/pertes ponctuels (label, montant réellement appliqué)
    public event Action<string, float> OnAuraEvent;

    private bool _wasDrifting;
    private float _lastYaw;
    private float _brakeLoss;

    private BikeHealthSystem _healthSystem;
    private NearMissDetector _nearMiss;

    void Awake()
    {
        if (bikeControls == null) bikeControls = GetComponent<BikeControlsOW>();
        _healthSystem = GetComponent<BikeHealthSystem>();
        _nearMiss = GetComponent<NearMissDetector>();
    }

    void OnEnable()
    {
        bikeControls.Bonked += OnBonk;
        if (_healthSystem != null) _healthSystem.Crashed += OnCrash;
        if (_nearMiss != null) _nearMiss.Dodged += OnDodge;
    }

    void OnDisable()
    {
        bikeControls.Bonked -= OnBonk;
        if (_healthSystem != null) _healthSystem.Crashed -= OnCrash;
        if (_nearMiss != null) _nearMiss.Dodged -= OnDodge;
    }

    void Start()
    {
        if (rankDisplay == null) rankDisplay = FindAnyObjectByType<FlowRankDisplay>();
        if (auraFeed == null) auraFeed = FindAnyObjectByType<AuraFeed>(FindObjectsInactive.Include);

        aura = Mathf.Clamp(startAura, 0f, MaxAura);
        rank = RankFromAura(aura);
        smoothedSpeedRatio = 0.6f; // neutre sur la courbe : pas de pénalité le temps de démarrer
        if (rankDisplay != null) rankDisplay.SetRank(rank);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        // Contrôles coupés pendant la séquence de crash/respawn : l'aura est figée
        if (!bikeControls.enabled) return;

        float speed = bikeControls.GetCurrentSpeed();
        float speedRatio = Mathf.Max(speed, 0f) / bikeControls.maxSpeed;
        smoothedSpeedRatio = Mathf.Lerp(smoothedSpeedRatio, speedRatio, 1f - Mathf.Exp(-dt / speedSmoothTime));

        bool drifting = bikeControls.IsDrifting();
        if (drifting) UpdateDrift(speed);
        else if (_wasDrifting) EndDrift();
        _wasDrifting = drifting;

        // Pendant un drift on gèle vitesse et decay : le drift ralentit forcément, il est jugé à la fin
        if (!drifting)
        {
            aura += speedAuraCurve.Evaluate(smoothedSpeedRatio) * dt;
            aura -= decayPerRank[(int)rank] * dt;

            if (bikeControls.IsBraking() && speed > minBrakePenaltySpeed)
            {
                float loss = Mathf.Min(brakePenaltyPerSecond * dt, aura);
                aura -= loss;
                _brakeLoss += loss;
            }
        }
        // Le frein retire en continu, mais n'affiche qu'un seul message au relâchement
        if (!bikeControls.IsBraking() || drifting) FlushBrakeMessage();

        aura = Mathf.Clamp(aura, 0f, MaxAura);
        RefreshRank();
    }

    #region Drift
    private void UpdateDrift(float speed)
    {
        float yaw = transform.eulerAngles.y;
        if (!_wasDrifting)
        {
            driftDuration = 0f;
            driftAngle = 0f;
        }
        else if (bikeControls.IsGrounded() && speed > minDriftSpeed)
        {
            driftDuration += Time.deltaTime;
            driftAngle += Mathf.Abs(Mathf.DeltaAngle(_lastYaw, yaw));
        }
        _lastYaw = yaw;
    }

    private void EndDrift()
    {
        if (driftDuration < minDriftDuration) return;

        if (driftAngle >= minDriftAngle)
        {
            float bonus = driftBaseBonus + driftDuration * driftBonusPerSecond + driftAngle * driftBonusPerDegree;
            ApplyAuraEvent("DRIFT", Mathf.Min(bonus, maxDriftBonus));
        }
        else
        {
            // Drift en ligne droite = coup de frein déguisé
            ApplyAuraEvent("FREIN", -driftDuration * brakePenaltyPerSecond);
        }
    }
    #endregion

    #region Chocs
    private void OnBonk()
    {
        CancelDrift();
        FlushBrakeMessage();
        ApplyAuraEvent("BONK", -bonkPenalty);
    }

    private void OnCrash()
    {
        CancelDrift();
        FlushBrakeMessage();
        // Un rang de moins, posé en bas du rang inférieur : ça fait mal sans renvoyer direct en D
        int lowerRank = Mathf.Max((int)rank - 1, 0);
        float target = Mathf.Min(aura, lowerRank * pointsPerRank + crashKeepInRank);
        ApplyAuraEvent("CRASH", target - aura);
        smoothedSpeedRatio = 0.6f; // repart neutre après le respawn
    }

    private void OnDodge(float closeness)
    {
        ApplyAuraEvent("ESQUIVE", dodgeBaseBonus + closeness * dodgeClosenessBonus);
    }

    // Le drift en cours ne rapporte rien ; s'il est maintenu, il repart de zéro
    private void CancelDrift()
    {
        _wasDrifting = false;
        driftDuration = 0f;
        driftAngle = 0f;
    }
    #endregion

    #region Events
    // Gain/perte ponctuel : appliqué tout de suite et affiché dans le feed
    private void ApplyAuraEvent(string label, float amount)
    {
        float before = aura;
        aura = Mathf.Clamp(aura + amount, 0f, MaxAura);
        float applied = aura - before;
        RefreshRank();

        // Le feed affiche le montant nominal : un drift en S plein ou un bonk à 0 restent visibles
        if (auraFeed != null) auraFeed.Push(label, amount);
        OnAuraEvent?.Invoke(label, applied);
    }

    private void FlushBrakeMessage()
    {
        if (_brakeLoss >= minBrakeMessage)
        {
            if (auraFeed != null) auraFeed.Push("FREIN", -_brakeLoss);
            OnAuraEvent?.Invoke("FREIN", -_brakeLoss);
        }
        _brakeLoss = 0f;
    }
    #endregion

    #region Rank
    private FlowRank RankFromAura(float value)
    {
        return (FlowRank)Mathf.Clamp(Mathf.FloorToInt(value / pointsPerRank), 0, (int)FlowRank.S);
    }

    private void RefreshRank()
    {
        FlowRank target = RankFromAura(aura);
        bool up = target > rank;
        bool down = target < rank && aura < (int)rank * pointsPerRank - rankDownMargin;
        if (!up && !down) return;

        rank = target;
        if (rankDisplay != null) rankDisplay.SetRank(rank);
        OnRankChanged?.Invoke(rank);
    }
    #endregion

    #region Debug
    [ContextMenu("Debug/Add 50 Aura")]
    private void DebugAddAura() => aura = Mathf.Min(aura + 50f, MaxAura);

    [ContextMenu("Debug/Remove 50 Aura")]
    private void DebugRemoveAura() => aura = Mathf.Max(aura - 50f, 0f);
    #endregion
}
