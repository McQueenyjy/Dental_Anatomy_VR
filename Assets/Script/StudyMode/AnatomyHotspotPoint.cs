using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class AnatomyHotspotPoint : MonoBehaviour
{
    public AnatomyHotspotGroup group;
    private XRBaseInteractable interactable;
    private readonly HashSet<XRRayInteractor> rays = new HashSet<XRRayInteractor>();

    public IEnumerable<Collider> GetHitColliders()
    {
        if (interactable == null) interactable = GetComponent<XRBaseInteractable>();
        if (interactable != null && interactable.colliders.Count > 0) return interactable.colliders;
        return GetComponents<Collider>();
    }

    private void OnEnable()
    {
        if (group == null) group = GetComponentInParent<AnatomyHotspotGroup>();
        if (interactable == null) interactable = GetComponent<XRBaseInteractable>();
        if (interactable == null) return;
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }
        ClearHover();
    }

    public void ClearHover()
    {
        rays.Clear();
        if (group != null) group.PointHoverExited(this);
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (group == null || !group.FilterEnabled || !(args.interactorObject is XRRayInteractor ray)) return;
        if (rays.Add(ray) && rays.Count == 1) group.PointHoverEntered(this);
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        if (!(args.interactorObject is XRRayInteractor ray)) return;
        if (rays.Remove(ray) && rays.Count == 0 && group != null) group.PointHoverExited(this);
    }
}
