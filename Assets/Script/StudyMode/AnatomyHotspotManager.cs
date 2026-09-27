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

    private readonly List<AnatomyRegion> hoveredRegions = new List<AnatomyRegion>();

    private void Awake() => FindInfoPanel();

    private void OnEnable()
    {
        Active = this;
        if (showInfoAction != null && showInfoAction.action != null)
        {
            showInfoAction.action.Enable();
            showInfoAction.action.performed += OnShowInfoPressed;
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

    public void ApplyStructureFilters()
    {
        foreach (AnatomyRegion region in FindObjectsOfType<AnatomyRegion>(true))
        {
            if (region.gameObject.scene == gameObject.scene)
                region.SetInteractableByFilter(IsTypeEnabled(region.structureType));
        }
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
        hoveredRegions.Clear();
        RefreshInfo();
    }

    private void RefreshInfo()
    {
        hoveredRegions.RemoveAll(region => region == null || !region.isActiveAndEnabled ||
            !region.FilterEnabled || !region.IsHovered);
        if (infoPanel == null) return;
        if (hoveredRegions.Count == 0)
        {
            infoPanel.ClearInfo();
            return;
        }
        AnatomyRegion current = hoveredRegions[hoveredRegions.Count - 1];
        infoPanel.ShowInfo(current.structureName, current.description);
    }

    private void OnShowInfoPressed(InputAction.CallbackContext context) => RefreshInfo();
}
