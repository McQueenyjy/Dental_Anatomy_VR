using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Connects the structure toggles to the scene's AnatomyHotspotManager.
// Hides this section if the scene has no manager.
public class StructureToggles : MonoBehaviour
{
    public Toggle cusps, ridges, pits, fossae, grooves;
    [Tooltip("Hide toggles for structure types this scene has no regions of.")]
    public bool onlyShowPresentTypes = false;

    private AnatomyHotspotManager manager;
    private bool bound;

    private void OnEnable()
    {
        if (manager == null)
        {
            manager = AnatomyHotspotManager.Active;
            if (manager == null) manager = FindObjectOfType<AnatomyHotspotManager>();
        }
        if (manager == null)
        {
            gameObject.SetActive(false);
            return;
        }

        if (!bound)
        {
            Bind(cusps, manager.SetCuspFilter, AnatomyStructureType.Cusp);
            Bind(ridges, manager.SetRidgeFilter, AnatomyStructureType.Ridge);
            Bind(pits, manager.SetPitFilter, AnatomyStructureType.Pit);
            Bind(fossae, manager.SetFossaFilter, AnatomyStructureType.Fossa);
            Bind(grooves, manager.SetGrooveFilter, AnatomyStructureType.Groove);
            bound = true;
        }

        // Match the current filter state each time the menu opens.
        Sync(cusps, manager.showCusps);
        Sync(ridges, manager.showRidges);
        Sync(pits, manager.showPits);
        Sync(fossae, manager.showFossae);
        Sync(grooves, manager.showGrooves);

        if (onlyShowPresentTypes)
        {
            ShowIfPresent(cusps, AnatomyStructureType.Cusp);
            ShowIfPresent(ridges, AnatomyStructureType.Ridge);
            ShowIfPresent(pits, AnatomyStructureType.Pit);
            ShowIfPresent(fossae, AnatomyStructureType.Fossa);
            ShowIfPresent(grooves, AnatomyStructureType.Groove);
        }
    }

    private static void Bind(Toggle toggle, UnityAction<bool> setter, AnatomyStructureType type)
    {
        if (toggle == null) return;
        toggle.onValueChanged.AddListener(setter);
        ToggleButtonStyle style = toggle.GetComponent<ToggleButtonStyle>();
        if (style != null) style.onColor = AnatomyRegion.CategoryColor(type);
    }

    private static void Sync(Toggle toggle, bool isOn)
    {
        if (toggle == null) return;
        toggle.SetIsOnWithoutNotify(isOn);
        ToggleButtonStyle style = toggle.GetComponent<ToggleButtonStyle>();
        if (style != null) style.Refresh();
    }

    private void ShowIfPresent(Toggle toggle, AnatomyStructureType type)
    {
        if (toggle == null) return;
        bool present = false;
        foreach (AnatomyRegion region in FindObjectsOfType<AnatomyRegion>(true))
        {
            if (region.gameObject.scene == manager.gameObject.scene && region.structureType == type)
            {
                present = true;
                break;
            }
        }
        toggle.gameObject.SetActive(present);
    }
}