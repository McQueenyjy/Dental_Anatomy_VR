using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

// Opens and closes a menu with a controller button, placing it in front of the user.
public class SummonedMenu : MonoBehaviour
{
    [Tooltip("Button that opens and closes the menu, e.g. XRI LeftHand Interaction/ToggleMenu.")]
    public InputActionReference toggleAction;
    [Tooltip("Optional second button, e.g. right B, so the menu can be opened one-handed.")]
    public InputActionReference secondaryToggleAction;
    [Tooltip("The part that is shown and hidden. Keep this object itself active.")]
    public GameObject content;
    [Tooltip("Distance in front of the head, in metres.")]
    public float distance = 0.8f;
    [Tooltip("Height relative to the head, in metres. Negative is lower.")]
    public float heightOffset = -0.15f;
    [SerializeField] private bool allowDragging = false;

    private int? dragPointer;
    private Plane dragPlane;
    private Vector3 grabOffset;
    private Vector3 menuOffset;
    private XRUIInputModule inputModule;
    private readonly Dictionary<XRRayInteractor, LayerMask> suspendedRaycasts =
        new Dictionary<XRRayInteractor, LayerMask>();

    private Vector3 DefaultOffset => new Vector3(0f, heightOffset, distance);

    public bool IsOpen => content != null && content.activeSelf;

    private void Awake()
    {
        if (content == null) return;
        if (allowDragging) EnablePanelDragging();
        content.SetActive(false);
    }

    private void OnEnable()
    {
        Listen(toggleAction, true);
        Listen(secondaryToggleAction, true);
    }

    private void OnDisable()
    {
        dragPointer = null;
        StopMenuRayPriority();
        Listen(toggleAction, false);
        Listen(secondaryToggleAction, false);
    }

    private void Listen(InputActionReference reference, bool listen)
    {
        if (reference == null || reference.action == null) return;
        if (listen)
        {
            reference.action.performed += OnTogglePressed;
            reference.action.Enable();
        }
        else
        {
            reference.action.performed -= OnTogglePressed;
        }
    }

    private void OnTogglePressed(InputAction.CallbackContext context) => Toggle();

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (content == null) return;
        PlaceInFront();
        content.SetActive(true);
        if (allowDragging && inputModule == null && EventSystem.current != null)
        {
            inputModule = EventSystem.current.GetComponent<XRUIInputModule>();
            if (inputModule != null) inputModule.finalizeRaycastResults += PrioritizeMenuHit;
        }
    }

    public void Close()
    {
        dragPointer = null;
        StopMenuRayPriority();
        if (content != null) content.SetActive(false);
    }

    private void PlaceInFront()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Quaternion heading = GetHeading(cam.transform);
        content.transform.SetPositionAndRotation(
            cam.transform.position + heading * (DefaultOffset + menuOffset), heading);
    }

    private static Quaternion GetHeading(Transform head)
    {
        Vector3 forward = head.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
        return Quaternion.LookRotation(forward, Vector3.up);
    }

    private void EnablePanelDragging()
    {
        Transform panel = content.transform.Find("Panel");
        if (panel == null) return;
        var trigger = panel.gameObject.AddComponent<EventTrigger>();
        AddDragEvent(trigger, EventTriggerType.BeginDrag, StartDrag);
        AddDragEvent(trigger, EventTriggerType.Drag, MoveDrag);
        AddDragEvent(trigger, EventTriggerType.EndDrag, EndDrag);
    }

    private void PrioritizeMenuHit(PointerEventData data, List<RaycastResult> results)
    {
        if (!(data is TrackedDeviceEventData tracked) ||
            !(tracked.interactor is XRRayInteractor ray)) return;

        int menuHit = results.FindIndex(result => result.gameObject != null &&
            result.gameObject.transform.IsChildOf(content.transform));
        bool dragging = dragPointer == data.pointerId;
        if (menuHit >= 0 || dragging)
        {
            // XRI processes UI before physics. Suppress 3D hits for this ray while the menu owns it.
            if (!suspendedRaycasts.ContainsKey(ray))
            {
                suspendedRaycasts.Add(ray, ray.raycastMask);
                ray.raycastMask = 0;
            }

            if (menuHit > 0)
            {
                RaycastResult hit = results[menuHit];
                results.RemoveAt(menuHit);
                results.Insert(0, hit);
            }
            else if (dragging && menuHit < 0)
            {
                results.Clear();
            }
        }
        else if (suspendedRaycasts.TryGetValue(ray, out LayerMask mask))
        {
            ray.raycastMask = mask;
            suspendedRaycasts.Remove(ray);
        }
    }

    private void StopMenuRayPriority()
    {
        if (inputModule != null) inputModule.finalizeRaycastResults -= PrioritizeMenuHit;
        inputModule = null;
        RestoreRaycastMasks();
    }

    private void LateUpdate()
    {
        // The next UI pass needs the original masks to raycast canvases.
        RestoreRaycastMasks();
    }

    private void RestoreRaycastMasks()
    {
        foreach (var pair in suspendedRaycasts)
        {
            if (pair.Key != null) pair.Key.raycastMask = pair.Value;
        }
        suspendedRaycasts.Clear();
    }

    private static void AddDragEvent(EventTrigger trigger, EventTriggerType type,
        System.Action<PointerEventData> handler)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(data => handler((PointerEventData)data));
        trigger.triggers.Add(entry);
    }

    private void StartDrag(PointerEventData data)
    {
        if (dragPointer.HasValue || data.button != PointerEventData.InputButton.Left) return;

        var panelTransform = content.transform;
        dragPlane = new Plane(panelTransform.forward, panelTransform.position);
        dragPointer = data.pointerId;
        grabOffset = panelTransform.position - dragPlane.ClosestPointOnPlane(data.pointerPressRaycast.worldPosition);
    }

    private void MoveDrag(PointerEventData data)
    {
        if (dragPointer != data.pointerId || !TryGetDragPoint(data, out Vector3 point)) return;
        content.transform.position = point + grabOffset;

        var cam = Camera.main;
        if (cam != null)
        {
            menuOffset = Quaternion.Inverse(GetHeading(cam.transform)) *
                (content.transform.position - cam.transform.position) - DefaultOffset;
        }
    }

    private void EndDrag(PointerEventData data)
    {
        if (dragPointer == data.pointerId) dragPointer = null;
    }

    private bool TryGetDragPoint(PointerEventData data, out Vector3 point)
    {
        if (data is TrackedDeviceEventData tracked)
        {
            var points = tracked.rayPoints;
            if (points != null)
            {
                for (int i = 1; i < points.Count; i++)
                {
                    Vector3 segment = points[i] - points[i - 1];
                    float length = segment.magnitude;
                    if (length < 1e-5f) continue;
                    var ray = new Ray(points[i - 1], segment / length);
                    if (dragPlane.Raycast(ray, out float hit) && hit <= length)
                    {
                        point = ray.GetPoint(hit);
                        return true;
                    }
                }
            }
        }
        else
        {
            var camera = data.pressEventCamera != null ? data.pressEventCamera : Camera.main;
            if (camera != null)
            {
                Ray ray = camera.ScreenPointToRay(data.position);
                if (dragPlane.Raycast(ray, out float hit))
                {
                    point = ray.GetPoint(hit);
                    return true;
                }
            }
        }

        point = default;
        return false;
    }
}
