using TMPro;
using UnityEngine;

// Shows the current tooth's view name, such as "Viewing: Occlusal",
// as worked out by the tooth's ToothViewTracker.
public class ToothViewLabel : MonoBehaviour
{
    public StudyModeManager manager;
    public TMP_Text label;
    [Tooltip("{0} is replaced with the view name.")]
    private const string format = "Viewing: {0}";

    private GameObject shownTooth;
    private ToothViewTracker tracker;

    private void Awake()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        if (manager == null) manager = FindObjectOfType<StudyModeManager>();
        SetText("");
    }

    private void LateUpdate()
    {
        GameObject tooth = manager != null ? manager.CurrentTooth : null;
        if (tooth != shownTooth)
        {
            shownTooth = tooth;
            tracker = tooth != null ? tooth.GetComponent<ToothViewTracker>() : null;
        }

        string view = tracker != null ? tracker.CurrentViewName : "";
        SetText(view.Length > 0 ? string.Format(format, view) : "");
    }

    private void SetText(string text)
    {
        if (label != null && label.text != text) label.text = text;
    }
}