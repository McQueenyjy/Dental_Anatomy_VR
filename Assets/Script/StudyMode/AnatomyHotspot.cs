using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class AnatomyHotspot : AnatomyRegion
{
    [Header("Highlight")]
    public GameObject highlightObject;
    [Header("References")]
    public AnatomyHotspotManager hotspotManager;
    private XRBaseInteractable interactable;

    protected override IEnumerable<GameObject> GetHighlights()
    {
        yield return highlightObject;
    }

    protected override IEnumerable<Collider> GetHitColliders()
    {
        interactable = GetComponent<XRBaseInteractable>();
        if (interactable != null && interactable.colliders.Count > 0)
            return interactable.colliders;
        return GetComponents<Collider>();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (interactable == null) interactable = GetComponent<XRBaseInteractable>();
        if (interactable == null) return;
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
    }

    protected override void OnDisable()
    {
        if (interactable != null)
        {
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }
        base.OnDisable();
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (args.interactorObject is XRRayInteractor ray) EnterHover(ray);
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        if (args.interactorObject is XRRayInteractor ray) ExitHover(ray);
    }
}
