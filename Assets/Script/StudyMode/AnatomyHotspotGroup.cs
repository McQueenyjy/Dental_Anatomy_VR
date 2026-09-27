using System.Collections.Generic;
using UnityEngine;

public class AnatomyHotspotGroup : AnatomyRegion
{
    [Header("Highlights")]
    public GameObject[] highlightObjects;
    private AnatomyHotspotPoint[] points;

    protected override IEnumerable<GameObject> GetHighlights()
    {
        return highlightObjects ?? new GameObject[0];
    }

    protected override IEnumerable<Collider> GetHitColliders()
    {
        points = GetComponentsInChildren<AnatomyHotspotPoint>(true);
        foreach (AnatomyHotspotPoint point in points)
        {
            if (point.group != null && point.group != this) continue;
            foreach (Collider hitCollider in point.GetHitColliders()) yield return hitCollider;
        }
    }

    public override void SetInteractableByFilter(bool active)
    {
        base.SetInteractableByFilter(active);
        if (!active && points != null)
        {
            foreach (AnatomyHotspotPoint point in points)
                if (point != null && (point.group == null || point.group == this)) point.ClearHover();
        }
    }

    public void PointHoverEntered(AnatomyHotspotPoint point) => EnterHover(point);
    public void PointHoverExited(AnatomyHotspotPoint point) => ExitHover(point);
}
