using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class AnatomyHotspotManager : MonoBehaviour
{
    public static AnatomyHotspotManager Active { get; private set; }
    [Header("Input (optional: repeat current information)")]
    public InputActionReference showInfoAction;
    [Header("Info Panel")]
    public AnatomyInfoPanel infoPanel;
    [Header("Structure Filters")]
    public bool showPits = true;
    public bool showCusps = true;
    public bool showRidges = true;
    public bool showFossae = true;
    public bool showGrooves = true;

    [Header("Hover Transparency")]
    [Tooltip("Opacity of other visible anatomy regions while one region is highlighted. 0 is invisible; 1 is opaque.")]
    [Range(0.05f, 1f)] public float unselectedOpacity = 0.2f;

    [Header("Views")]
    [Tooltip("Opacity of pointable regions while nothing is being pointed at.")]
    [Range(0.05f, 1f)] public float hintOpacity = 0.35f;

    [Header("Info Card")]
    [Tooltip("Buttons that close a pinned info card when pressed anywhere, e.g. both hands' UI Press.")]
    public InputActionReference[] closeCardActions;

    private bool closedByPress;
    private int ignoreClicksUntilFrame = -1;

    private ToothView currentView = ToothView.None;
    public ToothView CurrentView => currentView;

    private readonly List<AnatomyRegion> hoveredRegions = new List<AnatomyRegion>();
    private AnatomyRegion currentRegion;
    public AnatomyRegion CurrentRegion => currentRegion;

    public AnatomyRegion SelectedRegion { get; private set; }
    private Vector3 selectedLocalPoint;

    // Where the selected region was clicked, following the tooth as it rotates.
    public Vector3 SelectedWorldPoint =>
        SelectedRegion != null ? SelectedRegion.transform.TransformPoint(selectedLocalPoint) : Vector3.zero;

    public void ToggleSelectedRegion(AnatomyRegion region, Vector3 worldPoint)
    {
        if (closedByPress && Time.frameCount <= ignoreClicksUntilFrame) return;
        if (region == null || region == SelectedRegion)
        {
            SelectedRegion = null;
        }
        else
        {
            SelectedRegion = region;
            selectedLocalPoint = region.transform.InverseTransformPoint(worldPoint);
        }
        RefreshInfo();
    }

    // Any trigger press closes a pinned card...
    private void OnCloseCardPressed(InputAction.CallbackContext context)
    {
        if (SelectedRegion == null) return;
        SelectedRegion = null;
        closedByPress = true;
        ignoreClicksUntilFrame = int.MaxValue;   // until the trigger is released
        RefreshInfo();
    }

    // ...and the click that finishes on release must not reopen one.
    private void OnCloseCardReleased(InputAction.CallbackContext context)
    {
        if (closedByPress) ignoreClicksUntilFrame = Time.frameCount + 1;
    }

    private void Update()
    {
        if (closedByPress && Time.frameCount > ignoreClicksUntilFrame) closedByPress = false;
    }

    private void Awake() => FindInfoPanel();

    private void OnEnable()
    {
        Active = this;
        if (showInfoAction != null && showInfoAction.action != null)
        {
            showInfoAction.action.Enable();
            showInfoAction.action.performed += OnShowInfoPressed;
        }
        foreach (InputActionReference reference in closeCardActions ?? new InputActionReference[0])
        {
            if (reference == null || reference.action == null) continue;
            reference.action.performed += OnCloseCardPressed;
            reference.action.canceled += OnCloseCardReleased;
            reference.action.Enable();
        }
    }

    private void Start()
    {
        FindInfoPanel();
        ApplyStructureFilters();
        RefreshInfo();
    }

    private void FindInfoPanel()
    {
        if (infoPanel != null) return;
        foreach (AnatomyInfoPanel panel in FindObjectsOfType<AnatomyInfoPanel>(true))
        {
            if (panel.gameObject.scene != gameObject.scene) continue;
            infoPanel = panel;
            break;
        }
    }

    private void OnDisable()
    {
        foreach (InputActionReference reference in closeCardActions ?? new InputActionReference[0])
        {
            if (reference == null || reference.action == null) continue;
            reference.action.performed -= OnCloseCardPressed;
            reference.action.canceled -= OnCloseCardReleased;
        }
        if (showInfoAction != null && showInfoAction.action != null)
            showInfoAction.action.performed -= OnShowInfoPressed;
        ClearSelection();
        if (Active == this) Active = null;
    }

    public void SetPitFilter(bool active) { showPits = active; ApplyStructureFilters(); }
    public void SetCuspFilter(bool active) { showCusps = active; ApplyStructureFilters(); }
    public void SetRidgeFilter(bool active) { showRidges = active; ApplyStructureFilters(); }
    public void SetFossaFilter(bool active) { showFossae = active; ApplyStructureFilters(); }
    public void SetGrooveFilter(bool active) { showGrooves = active; ApplyStructureFilters(); }

    public bool IsTypeEnabled(AnatomyStructureType type)
    {
        switch (type)
        {
            case AnatomyStructureType.Pit: return showPits;
            case AnatomyStructureType.Cusp: return showCusps;
            case AnatomyStructureType.Ridge: return showRidges;
            case AnatomyStructureType.Fossa: return showFossae;
            case AnatomyStructureType.Groove: return showGrooves;
            default: return false;
        }
    }

    public bool IsRegionEnabled(AnatomyRegion region)
    {
        return IsTypeEnabled(region.structureType) && (region.visibleInViews & currentView) != 0;
    }

    public void SetCurrentView(ToothView view)
    {
        if (view == currentView) return;
        currentView = view;
        ApplyStructureFilters();
    }

    public void ApplyStructureFilters()
    {
        foreach (AnatomyRegion region in FindObjectsOfType<AnatomyRegion>(true))
        {
            if (region.gameObject.scene == gameObject.scene)
                region.SetInteractableByFilter(IsRegionEnabled(region));
        }
        if (SelectedRegion != null && !IsRegionEnabled(SelectedRegion)) SelectedRegion = null;
        RefreshInfo();
    }

    public void SetHoveredHotspot(AnatomyHotspot hotspot) => SetHoveredRegion(hotspot);
    public void SetHoveredGroup(AnatomyHotspotGroup group) => SetHoveredRegion(group);

    public void SetHoveredRegion(AnatomyRegion region)
    {
        if (region == null || !region.FilterEnabled || !region.isActiveAndEnabled) return;
        hoveredRegions.Remove(region);
        hoveredRegions.Add(region);
        RefreshInfo();
    }

    public void ClearHoveredRegion(AnatomyRegion region)
    {
        if (hoveredRegions.Remove(region)) RefreshInfo();
    }

    public void ClearSelection()
    {
        SelectedRegion = null;
        hoveredRegions.Clear();
        RefreshInfo();
    }

    private void RefreshInfo()
    {
        hoveredRegions.RemoveAll(region => region == null || !region.isActiveAndEnabled ||
            !region.FilterEnabled || !region.IsHovered);
        currentRegion = hoveredRegions.Count == 0 ? null : hoveredRegions[hoveredRegions.Count - 1];
        foreach (AnatomyRegion region in FindObjectsOfType<AnatomyRegion>(true))
            if (region.gameObject.scene == gameObject.scene) ApplyRegionPresentation(region);
        if (infoPanel == null) return;
        if (hoveredRegions.Count == 0)
        {
            infoPanel.ClearInfo();
            return;
        }
        AnatomyRegion current = hoveredRegions[hoveredRegions.Count - 1];
        infoPanel.ShowInfo(current.structureName, current.description);
    }

    internal void ApplyRegionPresentation(AnatomyRegion region)
    {
        AnatomyRegion target = SelectedRegion != null ? SelectedRegion : currentRegion;
        AnatomyRegion focused = isActiveAndEnabled ? target : null;
        float opacity = focused == null ? hintOpacity : unselectedOpacity;
        region.SetFocusPresentation(focused, opacity, true);
    }

    private void OnShowInfoPressed(InputAction.CallbackContext context) => RefreshInfo();
}
