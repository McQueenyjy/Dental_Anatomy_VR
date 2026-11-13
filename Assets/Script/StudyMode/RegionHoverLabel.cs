using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;

// Floating label that shows the name of the anatomy region being pointed at,
// just above where the ray hits it, always facing the user.
public class RegionHoverLabel : MonoBehaviour
{
    [Tooltip("The part that is shown and hidden (the label's Canvas).")]
    public GameObject content;
    public TMP_Text label;
    [Tooltip("Extra text shown when the region is clicked. Hidden otherwise.")]
    public TMP_Text body;
    [Tooltip("How far above the hit point, in metres.")]
    public float heightAbovePointer = 0.04f;
    [Tooltip("How far toward the user, so the label doesn't sink into the tooth.")]
    public float towardViewer = 0.03f;
    [Tooltip("Keep roughly the same apparent size at any distance.")]
    public bool constantScreenSize = true;
    [Tooltip("Content scale at 1 m when Constant Screen Size is on.")]
    public float scaleAtOneMetre = 0.0005f;
    [Tooltip("Extra size for the clicked card with its description (1 = same as the hover label).")]
    public float expandedScale = 1.6f;
    [Tooltip("On: keep the label parallel to the view, so it always looks rectangular. " +
             "Off: face the user but stay level with the world.")]
    public bool alignToView = true;

    public enum CardSide { Auto, Right, Left }

    [Header("Expanded Card Placement")]
    [Tooltip("Slide the clicked card beside the tooth so it doesn't cover it.")]
    public bool moveToSideWhenExpanded = true;
    [Tooltip("Auto: the side that was clicked. Right/Left: always that side.")]
    public CardSide cardSide = CardSide.Right;
    [Tooltip("The card's background RectTransform, used to measure its size.")]
    public RectTransform card;
    [Tooltip("Space between the tooth and the card at 1 m away; grows with distance so it looks the same.")]
    public float sideGap = 0.015f;
    [Tooltip("Move the card up (+) or down (-) from the tooth's middle, at 1 m away.")]
    public float sideHeightOffset = 0f;
    [Tooltip("How quickly the card slides. Higher is faster.")]
    public float slideSpeed = 8f;

    private float sideBlend;          // 0 = at the pointer, 1 = beside the tooth
    private Vector3 lastSidePosition;   // where the open card sat, used while it slides back
    private float sideSign = 1f;      // +1 right of the tooth, -1 left
    private AnatomyRegion sideRegion;

    private XRRayInteractor[] rays;
    private AnatomyRegion shownRegion;

    private void Awake()
    {
        if (label == null && content != null) label = content.GetComponentInChildren<TMP_Text>(true);
        if (content != null) content.SetActive(false);
    }

    private void LateUpdate()
    {
        AnatomyHotspotManager manager = AnatomyHotspotManager.Active;
        Camera cam = Camera.main;
        if (manager == null || cam == null || content == null) { Hide(); return; }

        AnatomyRegion hovered = manager.CurrentRegion;
        AnatomyRegion selected = manager.SelectedRegion;
        // A clicked region stays pinned until the next click; otherwise follow the hover.
        AnatomyRegion region = selected != null ? selected : hovered;
        if (region == null) { Hide(); return; }

        // Expanded when showing the clicked region and it has a description.
        string description = region.description != null ? region.description.Trim() : "";
        bool expanded = region == selected && description.Length > 0;

        if (region != shownRegion)
        {
            shownRegion = region;
            if (label != null) label.text = region.structureName;
        }
        if (body != null)
        {
            if (body.gameObject.activeSelf != expanded) body.gameObject.SetActive(expanded);
            if (expanded && body.text != description) body.text = description;
        }

        Vector3 point = selected != null ? manager.SelectedWorldPoint : FindHitPoint(region);
        Vector3 toViewer = (cam.transform.position - point).normalized;
        Vector3 position = point + Vector3.up * heightAbovePointer + toViewer * towardViewer;

        Transform t = content.transform;
        t.rotation = alignToView
            ? cam.transform.rotation
            : Quaternion.LookRotation(position - cam.transform.position, Vector3.up);

        if (constantScreenSize)
        {
            float distance = Vector3.Distance(cam.transform.position, point);
            t.localScale = Vector3.one * (scaleAtOneMetre * distance * (expanded ? expandedScale : 1f));
        }

        // Ease between the pointer position and the spot beside the tooth.
        float target = expanded && moveToSideWhenExpanded ? 1f : 0f;
        sideBlend = Mathf.Lerp(sideBlend, target, 1f - Mathf.Exp(-slideSpeed * Time.deltaTime));
        if (Mathf.Abs(sideBlend - target) < 0.001f) sideBlend = target;

        if (sideBlend > 0f)
        {
            // Only work out the side spot while the card is open; when closing,
            // slide back from where it was instead of from the new region's side.
            if (target > 0f) lastSidePosition = SidePosition(region, point, cam, t.localScale.x);
            float s = sideBlend * sideBlend * (3f - 2f * sideBlend);   // smoothstep
            position = Vector3.Lerp(position, lastSidePosition, s);
        }
        else
        {
            sideRegion = null;   // the next opened card picks its side afresh
        }
        t.position = position;

        if (!content.activeSelf) content.SetActive(true);
    }

    // Beside the tooth on the side that was clicked, vertically centred on the tooth.
    private Vector3 SidePosition(AnatomyRegion region, Vector3 clickPoint, Camera cam, float scale)
    {
        Bounds tooth = ToothBounds(region);
        Vector3 right = cam.transform.right;
        Vector3 up = cam.transform.up;

        // Pick the side once per clicked region, so the card doesn't flip while the tooth turns.
        if (region != sideRegion)
        {
            sideRegion = region;
            sideSign = cardSide == CardSide.Right ? 1f
                     : cardSide == CardSide.Left ? -1f
                     : Vector3.Dot(clickPoint - tooth.center, right) >= 0f ? 1f : -1f;
        }

        Vector2 cardSize = card != null ? card.rect.size * scale : new Vector2(0.2f, 0.1f);
        float toothHalf = HalfExtentAlong(tooth, right);
        Vector3 toViewer = (cam.transform.position - tooth.center).normalized;
        float distance = Vector3.Distance(cam.transform.position, tooth.center);
        float gap = sideGap * distance;
        float height = sideHeightOffset * distance;

        // The card's pivot is its bottom centre, so lower it by half its height to centre it.
        return tooth.center
            + right * sideSign * (toothHalf + gap + cardSize.x * 0.5f)
            + up * (height - cardSize.y * 0.5f)
            + toViewer * towardViewer;
    }

    // Bounds of the whole tooth the region belongs to (the grabbable root), not just the region.
    private static Bounds ToothBounds(AnatomyRegion region)
    {
        Component root = region.GetComponentInParent<XRGrabInteractable>();
        Transform rootTransform = root != null ? root.transform : region.transform;

        bool found = false;
        Bounds bounds = new Bounds(rootTransform.position, Vector3.zero);
        foreach (Renderer r in rootTransform.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return bounds;
    }

    // Half the size of the bounds measured along a direction.
    private static float HalfExtentAlong(Bounds b, Vector3 dir) =>
        Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.y) * b.extents.y + Mathf.Abs(dir.z) * b.extents.z;

    private void Hide()
    {
        shownRegion = null;
        sideRegion = null;
        sideBlend = 0f;
        if (content != null && content.activeSelf) content.SetActive(false);
    }

    // Where a controller ray touches this region; otherwise the region's middle.
    private Vector3 FindHitPoint(AnatomyRegion region)
    {
        if (rays == null || rays.Length == 0) rays = FindObjectsOfType<XRRayInteractor>();
        foreach (XRRayInteractor ray in rays)
        {
            if (ray == null || !ray.isActiveAndEnabled) continue;
            if (ray.TryGetCurrentUIRaycastResult(out RaycastResult hit) && hit.gameObject != null &&
                hit.gameObject.GetComponentInParent<AnatomyRegion>() == region)
                return hit.worldPosition;
        }

        Renderer[] renderers = region.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return region.transform.position;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
        return bounds.center;
    }
}