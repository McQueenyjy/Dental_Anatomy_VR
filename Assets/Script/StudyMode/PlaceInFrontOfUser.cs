using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

public class PlaceInFrontOfUser : MonoBehaviour
{
    [Header("Target")]
    public Transform head;

    [Header("Placement")]
    public float distanceFromHead = 1.2f;
    public float heightOffset = -0.05f;

    [Header("Follow")]
    [Tooltip("Off: place the menu in front of the user once, then keep it fixed in the world.")]
    public bool followHead = false;
    public float positionSmooth = 8f;
    public float rotationSmooth = 8f;

    [Header("Recenter Input")]
    [Tooltip("Optional button that brings the menu back in front of the user.")]
    public InputActionReference recenterAction;

    private static readonly List<XRInputSubsystem> inputSubsystems = new List<XRInputSubsystem>();

    private void OnEnable()
    {
        if (recenterAction != null && recenterAction.action != null)
        {
            recenterAction.action.performed += OnRecenter;
            recenterAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (recenterAction != null && recenterAction.action != null)
            recenterAction.action.performed -= OnRecenter;
        ListenForSystemRecenter(false);
    }

    private void OnRecenter(InputAction.CallbackContext context) => SnapToHead();

    private IEnumerator Start()
    {
        if (head == null && Camera.main != null)
        {
            head = Camera.main.transform;
        }

        // Wait until the headset reports a real head position (not the floor-level
        // origin it reports while XR starts up), or give up after 2 seconds.
        float giveUpTime = Time.time + 2f;
        while (head != null && head.localPosition.sqrMagnitude < 0.01f && Time.time < giveUpTime)
            yield return null;

        SnapToHead();
        ListenForSystemRecenter(true);
    }

    private void LateUpdate()
    {
        if (!followHead || head == null) return;

        Vector3 forward = head.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 targetPosition =
            head.position +
            forward * distanceFromHead +
            Vector3.up * heightOffset;

        Quaternion targetRotation =
            Quaternion.LookRotation(forward, Vector3.up);

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            Time.deltaTime * positionSmooth
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * rotationSmooth
        );
    }

    public void SnapToHead()
    {
        if (head == null) return;

        Vector3 forward = head.forward;
        forward.y = 0f;
        forward.Normalize();

        transform.position =
            head.position +
            forward * distanceFromHead +
            Vector3.up * heightOffset;

        transform.rotation =
            Quaternion.LookRotation(forward, Vector3.up);
    }

    // Holding the Meta button resets the view; move the panels back in front of the user.
    private void ListenForSystemRecenter(bool listen)
    {
        SubsystemManager.GetSubsystems(inputSubsystems);
        foreach (XRInputSubsystem subsystem in inputSubsystems)
        {
            subsystem.trackingOriginUpdated -= OnTrackingOriginUpdated;
            if (listen) subsystem.trackingOriginUpdated += OnTrackingOriginUpdated;
        }
    }

    private void OnTrackingOriginUpdated(XRInputSubsystem subsystem)
    {
        if (isActiveAndEnabled) StartCoroutine(SnapNextFrame());
    }

    // The head position updates a frame after the recenter, so wait before placing.
    private IEnumerator SnapNextFrame()
    {
        yield return null;
        SnapToHead();
    }
}