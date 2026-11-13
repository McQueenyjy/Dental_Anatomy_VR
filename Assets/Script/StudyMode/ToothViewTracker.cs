using System;
using UnityEngine;

// Works out which surface of this tooth faces the user, using its
// ViewMarkers children, and tells the hotspot manager when it changes.
public class ToothViewTracker : MonoBehaviour
{
    [Tooltip("Parent of the view markers. Defaults to the 'ViewMarkers' child.")]
    public Transform markers;
    [Tooltip("No view unless a marker faces the user at least this directly (1 = straight on).")]
    [Range(0f, 1f)] public float minFacing = 0.5f;
    [Tooltip("How much more directly another surface must face the user before the view switches.")]
    [Range(0f, 0.3f)] public float switchMargin = 0.05f;

    public ToothView CurrentView { get; private set; } = ToothView.None;
    public string CurrentViewName => CurrentView == ToothView.None ? "" : CurrentView.ToString();
    public event Action<ToothView> ViewChanged;

    private string currentMarker = "";

    private void Awake()
    {
        if (markers == null) markers = transform.Find("ViewMarkers");
    }

    private void OnDisable()
    {
        currentMarker = "";
        SetView(ToothView.None);
    }

    private void LateUpdate()
    {
        currentMarker = FacingMarker(currentMarker);
        ToothView view = ToothView.None;
        if (currentMarker.Length > 0 && Enum.TryParse(currentMarker, out ToothView parsed))
            view = parsed;
        SetView(view);
    }

    private void SetView(ToothView view)
    {
        if (view == CurrentView) return;
        CurrentView = view;
        ViewChanged?.Invoke(view);
        if (AnatomyHotspotManager.Active != null)
            AnatomyHotspotManager.Active.SetCurrentView(view);
    }

    // The marker facing the user, keeping the current one unless
    // another faces the user more directly by at least switchMargin.
    private string FacingMarker(string current)
    {
        Camera cam = Camera.main;
        if (markers == null || cam == null) return "";

        Vector3 origin = markers.position;
        Vector3 toViewer = (cam.transform.position - origin).normalized;

        string best = "";
        float bestFacing = float.NegativeInfinity;
        float currentFacing = float.NegativeInfinity;
        foreach (Transform marker in markers)
        {
            Vector3 direction = marker.position - origin;
            if (direction.sqrMagnitude < 1e-6f) continue;
            float facing = Vector3.Dot(direction.normalized, toViewer);
            if (marker.name == current) currentFacing = facing;
            if (facing > bestFacing) { bestFacing = facing; best = marker.name; }
        }

        if (current.Length > 0 && currentFacing >= minFacing && bestFacing < currentFacing + switchMargin)
            return current;
        return bestFacing >= minFacing ? best : "";
    }
}