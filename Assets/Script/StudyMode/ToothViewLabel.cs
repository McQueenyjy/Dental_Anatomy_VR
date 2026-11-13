using TMPro;
using UnityEngine;

// Shows the current tooth's view name, such as "Viewing: Occlusal",
// and optionally that view's note from the tooth's ToothViewNotes.
public class ToothViewLabel : MonoBehaviour
{
    public StudyModeManager manager;
    public TMP_Text label;
    [Tooltip("Optional: text for the current view's note. Hidden when there is no note.")]
    public TMP_Text body;
    private const string format = "Viewing: {0}";

    private GameObject shownTooth;
    private ToothViewTracker tracker;
    private ToothViewNotes notes;

    private void Awake()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        if (manager == null) manager = FindObjectOfType<StudyModeManager>();
        SetText(label, "");
        SetBody("");
    }

    private void LateUpdate()
    {
        GameObject tooth = manager != null ? manager.CurrentTooth : null;
        if (tooth != shownTooth)
        {
            shownTooth = tooth;
            tracker = tooth != null ? tooth.GetComponent<ToothViewTracker>() : null;
            notes = tooth != null ? tooth.GetComponent<ToothViewNotes>() : null;
        }

        ToothView view = tracker != null ? tracker.CurrentView : ToothView.None;
        string viewName = tracker != null ? tracker.CurrentViewName : "";
        SetText(label, viewName.Length > 0 ? string.Format(format, viewName) : "");
        SetBody(tooth != null && notes != null ? notes.Get(view) : "");
    }

    private void SetBody(string text)
    {
        if (body == null) return;
        bool show = !string.IsNullOrWhiteSpace(text);
        if (body.gameObject.activeSelf != show) body.gameObject.SetActive(show);
        if (show) SetText(body, text);
    }

    private static void SetText(TMP_Text target, string text)
    {
        if (target != null && target.text != text) target.text = text;
    }
}