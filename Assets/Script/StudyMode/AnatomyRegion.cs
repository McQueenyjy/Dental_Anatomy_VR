using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
    private enum Presentation { Normal, Focused, Transparent }

    private sealed class HighlightVisual
    {
        public Renderer renderer;
        public Material[] original, focused, transparent;
        public Presentation presentation;
    }

    private readonly List<HighlightVisual> highlightRenderers = new List<HighlightVisual>();
    private readonly List<Material> runtimeMaterials = new List<Material>();
    private MaterialPropertyBlock properties;
    private bool initialized;
    private Presentation presentation;
    private float backgroundOpacity = 1f;

    protected abstract IEnumerable<GameObject> GetHighlights();
    protected abstract IEnumerable<Collider> GetHitColliders();

    protected virtual void Awake() => Initialize();

    protected virtual void OnEnable()
    {
        Initialize();
        AnatomyHotspotManager manager = GetManager();
        SetInteractableByFilter(manager == null || manager.IsTypeEnabled(structureType));
        manager?.ApplyRegionPresentation(this);
    }

    protected virtual void OnDisable()
    {
        ClearHover();
        SetFocusPresentation(null, 1f);
        UpdateVisuals(false);
    }

    protected virtual void OnDestroy()
    {
        foreach (HighlightVisual visual in highlightRenderers)
            if (visual.renderer != null) visual.renderer.sharedMaterials = visual.original;
        foreach (Material material in runtimeMaterials)
        {
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }
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
                if (!highlightRenderers.Exists(item => item.renderer == visual))
                    highlightRenderers.Add(new HighlightVisual
                        { renderer = visual, original = visual.sharedMaterials });
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

    internal void SetFocusPresentation(AnatomyRegion focused, float unselectedOpacity)
    {
        Presentation next = focused == null ? Presentation.Normal :
            focused == this ? Presentation.Focused : Presentation.Transparent;
        float opacity = Mathf.Clamp01(unselectedOpacity);
        if (presentation == next && Mathf.Approximately(backgroundOpacity, opacity)) return;
        Initialize();
        presentation = next;
        backgroundOpacity = opacity;
        UpdateVisuals(FilterEnabled && isActiveAndEnabled);
    }

    private Material[] CreatePresentationMaterials(Material[] originals, bool transparent)
    {
        var materials = new Material[originals.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            if (originals[i] == null) continue;
            var material = new Material(originals[i])
            {
                name = originals[i].name + (transparent ? " (Context)" : " (Focused)"),
                hideFlags = HideFlags.DontSave,
                // Draw the focused patch after context, so overlapping patches cannot tint it.
                renderQueue = (int)RenderQueue.Transparent + (transparent ? 0 : 100)
            };
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_SrcBlend", (float)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
            material.SetFloat("_DstBlend", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            material.SetFloat("_ZWrite", 0f);
            materials[i] = material;
            runtimeMaterials.Add(material);
        }
        return materials;
    }

    private void ApplyMaterials(HighlightVisual visual)
    {
        if (visual.presentation == presentation) return;
        switch (presentation)
        {
            case Presentation.Focused:
                if (visual.focused == null) visual.focused = CreatePresentationMaterials(visual.original, false);
                visual.renderer.sharedMaterials = visual.focused;
                break;
            case Presentation.Transparent:
                if (visual.transparent == null) visual.transparent = CreatePresentationMaterials(visual.original, true);
                visual.renderer.sharedMaterials = visual.transparent;
                break;
            default:
                visual.renderer.sharedMaterials = visual.original;
                break;
        }
        visual.presentation = presentation;
    }

    private void UpdateVisuals(bool visible)
    {
        Color color = CategoryColor(structureType);
        if (presentation == Presentation.Focused || (presentation == Presentation.Normal && IsHovered))
            color = Color.Lerp(color, Color.white, 1f);
        foreach (HighlightVisual highlight in highlightRenderers)
        {
            Renderer visual = highlight.renderer;
            if (visual == null) continue;
            ApplyMaterials(highlight);
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
