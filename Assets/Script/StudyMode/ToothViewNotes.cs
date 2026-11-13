using UnityEngine;

// Text to show for each view of this tooth, e.g. what to look for from the occlusal side.
public class ToothViewNotes : MonoBehaviour
{
    // Same values as ToothView, but not flags, so the Inspector shows a single choice.
    public enum View
    {
        Buccal   = (int)ToothView.Buccal,
        Lingual  = (int)ToothView.Lingual,
        Mesial   = (int)ToothView.Mesial,
        Distal   = (int)ToothView.Distal,
        Occlusal = (int)ToothView.Occlusal
    }

    [System.Serializable]
    public class Note
    {
        public View view;
        [TextArea(2, 6)] public string text;
    }

    [Tooltip("Shown when no view faces the user, or a view has no note.")]
    [TextArea(2, 6)] public string defaultText;
    public Note[] notes;

    public string Get(ToothView view)
    {
        if (notes != null && view != ToothView.None)
            foreach (Note note in notes)
                if ((ToothView)note.view == view && !string.IsNullOrWhiteSpace(note.text)) return note.text;
        return defaultText;
    }
}