using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class StudyToothSelectable : MonoBehaviour
{
    public StudyModeManager manager;
    public string toothId;
    public Behaviour outline;

    private XRBaseInteractable interactable;

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();

        if (manager == null)
        {
            manager = FindObjectOfType<StudyModeManager>();
        }

        SetOutline(false);
    }

    private void OnEnable()
    {
        if (interactable == null)
        {
            interactable = GetComponent<XRBaseInteractable>();
        }

        if (interactable != null)
        {
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
            interactable.selectEntered.AddListener(OnSelected);
        }
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
            interactable.selectEntered.RemoveListener(OnSelected);
        }
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        SetOutline(true);
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        SetOutline(false);
    }

    private void OnSelected(SelectEnterEventArgs args)
    {
        Select();
    }

    public void Select()
    {
        if (manager != null)
        {
            manager.SelectTooth(toothId);
        }
    }

    private void SetOutline(bool visible)
    {
        if (outline != null)
        {
            outline.enabled = visible;
        }
    }
}