using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Petits messages de gain/perte d'aura au-dessus du rang ("DRIFT +50", "BONK -45"...).
// Chaque message apparaît avec un pop, monte puis disparaît ; un nouveau message pousse les précédents vers le haut (cascade).
public class AuraFeed : MonoBehaviour
{
    [Header(" --- Style --- ")]
    public TMP_FontAsset font;
    [Tooltip("Attention : le FlowPanel est scalé (~x3), les tailles ci-dessous sont dans son repère")]
    public float fontSize = 6f;
    public Color gainColor = new Color(1f, 0.85f, 0.25f);
    public Color lossColor = new Color(1f, 0.3f, 0.25f);
    [Tooltip("Bord du feed sur lequel les messages s'alignent (calé sur le texte AURA RANK)")]
    public HorizontalAlignmentOptions alignment = HorizontalAlignmentOptions.Right;

    [Header(" --- Animation --- ")]
    public float lifetime = 1.3f;
    [Tooltip("Distance parcourue vers le haut pendant la durée de vie")]
    public float riseDistance = 12f;
    [Tooltip("Décalage appliqué aux messages déjà affichés quand un nouveau arrive")]
    public float stackSpacing = 7.5f;
    public float stackSmoothSpeed = 14f;
    [Tooltip("Part de la durée de vie (à la fin) pendant laquelle le message disparaît")]
    [Range(0f, 1f)] public float fadePortion = 0.5f;
    public float popScale = 1.3f;
    public float popDuration = 0.15f;
    public int maxMessages = 8;

    private class Message
    {
        public TextMeshProUGUI text;
        public float age;
        public float stackTarget;
        public float stackOffset;
    }

    private readonly List<Message> _active = new List<Message>();
    private readonly Stack<TextMeshProUGUI> _pool = new Stack<TextMeshProUGUI>();

    public void Push(string label, float amount)
    {
        int value = Mathf.RoundToInt(amount);
        if (value == 0) return;

        foreach (var m in _active) m.stackTarget += stackSpacing;
        if (_active.Count >= maxMessages) Release(_active[0]);

        var text = _pool.Count > 0 ? _pool.Pop() : CreateText();
        text.gameObject.SetActive(true);
        text.text = $"{label} {(value > 0 ? "+" : "")}{value}";
        text.color = value > 0 ? gainColor : lossColor;
        text.rectTransform.anchoredPosition = Vector2.zero;
        text.rectTransform.localScale = Vector3.one * popScale;

        _active.Add(new Message { text = text });
    }

    void Update()
    {
        float dt = Time.deltaTime;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var m = _active[i];
            m.age += dt;
            if (m.age >= lifetime)
            {
                Release(m);
                continue;
            }

            float k = m.age / lifetime;
            m.stackOffset = Mathf.Lerp(m.stackOffset, m.stackTarget, 1f - Mathf.Exp(-stackSmoothSpeed * dt));
            float rise = (1f - (1f - k) * (1f - k)) * riseDistance; // ease out
            m.text.rectTransform.anchoredPosition = new Vector2(0f, rise + m.stackOffset);

            float pop = Mathf.Clamp01(m.age / popDuration);
            m.text.rectTransform.localScale = Vector3.one * Mathf.Lerp(popScale, 1f, pop);

            float fadeStart = 1f - fadePortion;
            Color c = m.text.color;
            c.a = k < fadeStart ? 1f : 1f - (k - fadeStart) / fadePortion;
            m.text.color = c;
        }
    }

    void OnDisable()
    {
        for (int i = _active.Count - 1; i >= 0; i--) Release(_active[i]);
    }

    private void Release(Message m)
    {
        _active.Remove(m);
        m.text.gameObject.SetActive(false);
        _pool.Push(m.text);
    }

    private TextMeshProUGUI CreateText()
    {
        var go = new GameObject("AuraMsg", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        float x = alignment == HorizontalAlignmentOptions.Left ? 0f : alignment == HorizontalAlignmentOptions.Right ? 1f : 0.5f;
        rt.anchorMin = rt.anchorMax = new Vector2(x, 0f);
        rt.pivot = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(80f, fontSize * 1.2f);

        var text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.horizontalAlignment = alignment;
        text.verticalAlignment = VerticalAlignmentOptions.Bottom;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    [ContextMenu("Debug/Push Gain")]
    private void DebugGain() => Push("DRIFT", Random.Range(20f, 80f));

    [ContextMenu("Debug/Push Loss")]
    private void DebugLoss() => Push("BONK", -45f);
}
