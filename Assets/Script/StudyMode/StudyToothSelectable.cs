using UnityEngine;
using UnityEngine.EventSystems;

// Hover highlight and trigger-click selection for a tooth on the arch.
// Uses UI pointer events, so the tooth's collider can also be used
// by the arch's XR Grab Interactable for grabbing.
public class StudyToothSelectable : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public StudyModeManager manager;
    public string toothId;
    public Behaviour outline;

    private void Awake()
    {
        if (manager == null)
            manager = FindObjectOfType<StudyModeManager>();
        SetOutline(false);
    }

    private void OnDisable()
    {
        // Hiding the arch can happen before the pointer exit event arrives.
        SetOutline(false);
    }

    public void OnPointerEnter(PointerEventData eventData) => SetOutline(true);
    public void OnPointerExit(PointerEventData eventData) => SetOutline(false);
    public void OnPointerClick(PointerEventData eventData) => Select();

    public void Select()
    {
        if (manager != null)
            manager.SelectTooth(toothId);
    }

    private void SetOutline(bool visible)
    {
        if (outline != null)
            outline.enabled = visible;
    }
}