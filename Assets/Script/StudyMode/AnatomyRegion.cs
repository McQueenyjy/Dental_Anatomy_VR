using System.Collections.Generic;
using UnityEngine;

// Shared presentation for a single hotspot and a region with multiple hit areas.
public abstract class AnatomyRegion : MonoBehaviour
{
    [Header("Info")]
    public string structureName;
    [TextArea] public string description;
    [Header("Filter")]
    public AnatomyStructureType structureType;

    public bool FilterEnabled { get; private set; } = true;
    public bool IsHovered => hoverSources.Count > 0;

    private readonly HashSet<Object> hoverSources = new HashSet<Object>();
    private readonly Dictionary<Collider, bool> originalColliders = new Dictionary<Collider, bool>();
    private readonly List<Renderer> highlightRenderers = new List<Renderer>();
    private MaterialPropertyBlock properties;
    private bool initialized;

    protected abstract IEnumerable<GameObject> GetHighlights();
    protected abstract IEnumerable<Collider> GetHitColliders();

    protected virtual void Awake() => Initialize();

    protected virtual void OnEnable()
    {
        Initialize();
        AnatomyHotspotManager manager = GetManager();
        SetInteractableByFilter(manager == null || manager.IsTypeEnabled(structureType));
    }

    protected virtual void OnDisable()
    {
        ClearHover();
        UpdateVisuals(false);
    }

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        properties = new MaterialPropertyBlock();
        foreach (Collider hitCollider in GetHitColliders())
        {
            if (hitCollider != null && !originalColliders.ContainsKey(hitCollider))
                originalColliders.Add(hitCollider, hitCollider.enabled);
        }
        foreach (GameObject highlight in GetHighlights())
        {
            if (highlight == null) continue;
            // Filtering renderers keeps the visual from disabling its own hit area.
            if (highlight != gameObject && !transform.IsChildOf(highlight.transform))
                highlight.SetActive(true);
            foreach (Renderer visual in highlight.GetComponentsInChildren<Renderer>(true))
                if (!highlightRenderers.Contains(visual)) highlightRenderers.Add(visual);
        }
    }

    public virtual void SetInteractableByFilter(bool active)
    {
        Initialize();
        FilterEnabled = active;
        if (!active) ClearHover();
        foreach (KeyValuePair<Collider, bool> pair in originalColliders)
            if (pair.Key != null) pair.Key.enabled = active && pair.Value;
        UpdateVisuals(active && isActiveAndEnabled);
    }

    protected void EnterHover(Object source)
    {
        if (!FilterEnabled || !isActiveAndEnabled || source == null) return;
        if (!hoverSources.Add(source)) return;
        UpdateVisuals(true);
        GetManager()?.SetHoveredRegion(this);
    }

    protected void ExitHover(Object source)
    {
        if (!hoverSources.Remove(source) || IsHovered) return;
        GetManager()?.ClearHoveredRegion(this);
        UpdateVisuals(FilterEnabled && isActiveAndEnabled);
    }

    private void ClearHover()
    {
        hoverSources.Clear();
        GetManager()?.ClearHoveredRegion(this);
    }

    protected AnatomyHotspotManager GetManager()
    {
        AnatomyHotspotManager manager = AnatomyHotspotManager.Active;
        if (manager != null && manager.gameObject.scene == gameObject.scene) return manager;
        foreach (AnatomyHotspotManager candidate in FindObjectsOfType<AnatomyHotspotManager>())
            if (candidate.gameObject.scene == gameObject.scene) return candidate;
        return null;
    }

    private void UpdateVisuals(bool visible)
    {
        Color color = CategoryColor(structureType);
        if (IsHovered) color = Color.Lerp(color, Color.white, 0.6f);
        foreach (Renderer visual in highlightRenderers)
        {
            if (visual == null) continue;
            visual.enabled = visible;
            if (!visible) continue;
            visual.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            properties.SetColor("_EmissionColor", color * 0.08f);
            visual.SetPropertyBlock(properties);
        }
    }

    public static Color CategoryColor(AnatomyStructureType type)
    {
        switch (type)
        {
            case AnatomyStructureType.Pit: return new Color(0.04f, 0.45f, 0.88f);
            case AnatomyStructureType.Cusp: return new Color(0.02f, 0.65f, 0.65f);
            case AnatomyStructureType.Ridge: return new Color(0.94f, 0.8f, 0.08f);
            case AnatomyStructureType.Fossa: return new Color(0.64f, 0.16f, 0.74f);
            case AnatomyStructureType.Groove: return new Color(0.1f, 0.72f, 0.28f);
            default: return Color.white;
        }
    }
}
