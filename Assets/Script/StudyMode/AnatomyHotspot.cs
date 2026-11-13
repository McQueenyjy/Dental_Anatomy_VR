using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// One anatomy region on a tooth. Hover comes from UI pointer events,
// so the region's collider can also be used to grab the whole tooth.
public class AnatomyHotspot : AnatomyRegion, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Highlight")]
    public GameObject highlightObject;
    [Header("References")]
    public AnatomyHotspotManager hotspotManager;

    private readonly HashSet<int> pointers = new HashSet<int>();

    protected override IEnumerable<GameObject> GetHighlights()
    {
        yield return highlightObject;
    }

    // Some regions keep their collider on the Highlight child, so include children.
    protected override IEnumerable<Collider> GetHitColliders()
    {
        return GetComponentsInChildren<Collider>(true);
    }

    protected override void OnDisable()
    {
        pointers.Clear();
        base.OnDisable();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (pointers.Add(eventData.pointerId) && pointers.Count == 1) EnterHover(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (pointers.Remove(eventData.pointerId) && pointers.Count == 0) ExitHover(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        AnatomyHotspotManager manager = GetManager();
        if (manager != null)
            manager.ToggleSelectedRegion(this, eventData.pointerCurrentRaycast.worldPosition);
    }
}