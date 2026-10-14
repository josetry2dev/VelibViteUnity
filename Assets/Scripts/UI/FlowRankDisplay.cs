using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

public enum FlowRank
{
    D,
    C,
    B,
    A,
    S
}

// Affiche la note de flow du joueur : les lettres 3D sont rendues par une caméra dédiée
// dans une RenderTexture, elle-même affichée dans le HUD via une RawImage.
public class FlowRankDisplay : MonoBehaviour
{
    [Header(" --- References --- ")]
    public RawImage targetImage;
    public Camera stageCamera;
    public Transform letterPivot;
    [Tooltip("Une lettre par rang, dans l'ordre D, C, B, A, S")]
    public Transform[] letters = new Transform[5];

    [Header(" --- Render --- ")]
    public int textureSize = 512;

    [Header(" --- Idle Float --- ")]
    public float bobAmplitude = 0.03f;
    public float bobSpeed = 1.6f;
    public float swayAngle = 20f;
    public float swaySpeed = 0.8f;
    public float tiltAngle = 6f;
    public float tiltSpeed = 1.3f;

    [Header(" --- Transition --- ")]
    public float swapDuration = 0.35f;
    public float rankUpPunch = 1.3f;

    [Header(" --- Visibility --- ")]
    [Tooltip("Affiché seulement en niveau (LevelManager actif) et quand le joueur est sur le vélo")]
    public bool showInLevels = true;
    [Tooltip("Intervalle de recherche du vélo quand le joueur n'est pas dessus (à pied, en station...)")]
    public float bikeCheckInterval = 0.25f;
    public FlowRank startRank = FlowRank.D;

    public FlowRank CurrentRank { get; private set; }
    public bool IsShown { get; private set; }

    private CanvasGroup _canvasGroup;
    private RenderTexture _renderTexture;
    private Vector3 _pivotBasePos;
    private Vector3[] _letterBaseScales;
    private Coroutine _swapRoutine;
    private float _idleTime;

    // Visibilité
    private bool _visibleRequested;
    private BikeControlsBase _bike;
    private float _bikeCheckTimer;

    void Awake()
    {
        // Masque tout le contenu du panel d'un coup (lettre + textes ajoutés dedans)
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        _renderTexture = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32);
        _renderTexture.antiAliasing = 4;
        _renderTexture.name = "FlowRankRT";
        stageCamera.targetTexture = _renderTexture;
        stageCamera.clearFlags = CameraClearFlags.SolidColor;
        stageCamera.backgroundColor = Color.clear;
        var camData = stageCamera.GetUniversalAdditionalCameraData();
        camData.renderPostProcessing = false;
        camData.renderShadows = false;
        targetImage.texture = _renderTexture;

        _pivotBasePos = letterPivot.localPosition;
        _letterBaseScales = new Vector3[letters.Length];
        for (int i = 0; i < letters.Length; i++)
        {
            _letterBaseScales[i] = letters[i].localScale;
            letters[i].gameObject.SetActive(false);
        }

        CurrentRank = startRank;
        letters[(int)CurrentRank].gameObject.SetActive(true);
    }

    void Start()
    {
        _visibleRequested = showInLevels;
        ApplyVisibility(false, true);
    }

    void OnDestroy()
    {
        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
        }
    }

    void Update()
    {
        ApplyVisibility(_visibleRequested && LevelManager.Instance != null && IsPlayerOnBike());
        if (!IsShown) return;

        // Flottement léger : bob vertical + balancement + petite inclinaison
        _idleTime += Time.deltaTime;
        letterPivot.localPosition = _pivotBasePos + Vector3.up * (Mathf.Sin(_idleTime * bobSpeed) * bobAmplitude);
        float yaw = Mathf.Sin(_idleTime * swaySpeed) * swayAngle;
        float roll = Mathf.Sin(_idleTime * tiltSpeed + 1.3f) * tiltAngle;
        letterPivot.localRotation = Quaternion.Euler(0f, yaw, roll);
    }

    // Demande d'affichage (ex : le flow manager) ; reste masqué tant que le joueur n'est pas sur le vélo
    public void SetVisible(bool visible)
    {
        _visibleRequested = visible;
    }

    private void ApplyVisibility(bool visible, bool force = false)
    {
        if (visible == IsShown && !force) return;
        IsShown = visible;
        _canvasGroup.alpha = visible ? 1f : 0f;
        stageCamera.enabled = visible;
    }

    // Le vélo (PlayerOW) est détruit quand on descend ou qu'on entre en station, puis remplacé par le FPSPlayer
    private bool IsPlayerOnBike()
    {
        if (_bike != null && _bike.isActiveAndEnabled) return true;

        _bikeCheckTimer -= Time.unscaledDeltaTime;
        if (_bikeCheckTimer > 0f) return false;
        _bikeCheckTimer = bikeCheckInterval;
        _bike = FindAnyObjectByType<BikeControlsBase>();
        return _bike != null && _bike.isActiveAndEnabled;
    }

    public void SetRank(FlowRank rank)
    {
        if (rank == CurrentRank) return;
        bool rankUp = rank > CurrentRank;
        FlowRank previous = CurrentRank;
        CurrentRank = rank;

        if (_swapRoutine != null)
        {
            StopCoroutine(_swapRoutine);
            for (int i = 0; i < letters.Length; i++)
            {
                letters[i].localScale = _letterBaseScales[i];
                letters[i].gameObject.SetActive(i == (int)previous);
            }
        }
        _swapRoutine = StartCoroutine(SwapRoutine(previous, rank, rankUp));
    }

    public void RankUp()
    {
        if (CurrentRank < FlowRank.S) SetRank(CurrentRank + 1);
    }

    public void RankDown()
    {
        if (CurrentRank > FlowRank.D) SetRank(CurrentRank - 1);
    }

    private IEnumerator SwapRoutine(FlowRank from, FlowRank to, bool rankUp)
    {
        Transform outLetter = letters[(int)from];
        Transform inLetter = letters[(int)to];
        Vector3 outScale = _letterBaseScales[(int)from];
        Vector3 inScale = _letterBaseScales[(int)to];
        float half = swapDuration * 0.5f;

        // Sortie : la lettre se rétracte
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            outLetter.localScale = outScale * (1f - EaseInBack(t / half));
            yield return null;
        }
        outLetter.localScale = outScale;
        outLetter.gameObject.SetActive(false);

        // Entrée : pop avec dépassement (plus marqué quand on monte de rang)
        inLetter.gameObject.SetActive(true);
        float overshoot = rankUp ? rankUpPunch : 1.1f;
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            float k = t / half;
            float s = k < 0.6f ? Mathf.Lerp(0f, overshoot, k / 0.6f) : Mathf.Lerp(overshoot, 1f, (k - 0.6f) / 0.4f);
            inLetter.localScale = inScale * s;
            yield return null;
        }
        inLetter.localScale = inScale;
        _swapRoutine = null;
    }

    private static float EaseInBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return Mathf.Clamp01(c3 * x * x * x - c1 * x * x);
    }

    [ContextMenu("Debug/Rank Up")]
    private void DebugRankUp() => RankUp();

    [ContextMenu("Debug/Rank Down")]
    private void DebugRankDown() => RankDown();
}
