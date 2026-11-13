using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// Rotates this object by "dragging" it while grabbed, so the side
// facing the user follows the ray, like spinning a globe.
// Optionally snaps to the nearest cube-aligned orientation on release.
[RequireComponent(typeof(XRGrabInteractable))]
public class DragToRotate : MonoBehaviour
{
    [Header("Drag")]
    [Tooltip("Degrees the object turns per degree the controller turns.")]
    public float sensitivity = 2f;
    [Tooltip("Allow tilting up and down as well as spinning left and right.")]
    public bool allowPitch = true;
    [Tooltip("Rotate around the middle of the visible model instead of its pivot.")]
    public bool rotateAroundVisualCenter = true;

    [Header("Snap on Release")]
    [Tooltip("After release, ease to the nearest of the 24 cube-aligned orientations " +
             "(6 faces x 4 roll positions), relative to the starting orientation.")]
    public bool snapToFacesOnRelease = false;
    [Tooltip("How quickly it eases into place. Higher is faster.")]
    public float snapSpeed = 10f;

    private XRGrabInteractable grab;
    private Transform controller;
    private Vector3 lastDirection;
    private Vector3 pivot;

    private Quaternion referenceLocalRotation;
    private bool snapping;
    private Quaternion snapTargetLocal;

    // True while the tooth is being dragged or is easing into a snap.
    public bool IsMoving => controller != null || snapping;

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        grab.trackPosition = false;
        grab.trackRotation = false;
        grab.throwOnDetach = false;
    }

    private void Start()
    {
        // The orientation the object starts in counts as the "front" view.
        referenceLocalRotation = transform.localRotation;
    }

    private void OnEnable()
    {
        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
    }

    private void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnGrab);
        grab.selectExited.RemoveListener(OnRelease);
        controller = null;
        snapping = false;
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        snapping = false;
        controller = args.interactorObject.transform;
        lastDirection = controller.forward;
        pivot = rotateAroundVisualCenter ? GetVisualCenter() : transform.position;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        controller = null;
        if (!snapToFacesOnRelease) return;

        pivot = rotateAroundVisualCenter ? GetVisualCenter() : transform.position;
        snapTargetLocal = NearestCubeOrientation(transform.localRotation);
        snapping = true;
    }

    private void Update()
    {
        if (controller != null) Drag();
        else if (snapping) Snap();
    }

    private void Drag()
    {
        Vector3 direction = controller.forward;
        Vector3 up = Vector3.up;
        Vector3 right = Camera.main != null ? Camera.main.transform.right : Vector3.right;

        float yaw = Vector3.SignedAngle(
            Vector3.ProjectOnPlane(lastDirection, up),
            Vector3.ProjectOnPlane(direction, up), up);
        float pitch = allowPitch
            ? Vector3.SignedAngle(
                Vector3.ProjectOnPlane(lastDirection, right),
                Vector3.ProjectOnPlane(direction, right), right)
            : 0f;

        // Rotate opposite to the hand, so the near surface follows the ray.
        transform.RotateAround(pivot, up, -yaw * sensitivity);
        transform.RotateAround(pivot, right, -pitch * sensitivity);

        lastDirection = direction;
    }

    private void Snap()
    {
        Quaternion parentRotation = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
        Quaternion target = parentRotation * snapTargetLocal;

        // Ease toward the target, frame-rate independent.
        float t = 1f - Mathf.Exp(-snapSpeed * Time.deltaTime);
        Quaternion next = Quaternion.Slerp(transform.rotation, target, t);
        if (Quaternion.Angle(next, target) < 0.1f)
        {
            next = target;
            snapping = false;
        }

        // Apply the change around the pivot, so the visible center stays put.
        RotateAroundPivot(next * Quaternion.Inverse(transform.rotation));
    }

    private void RotateAroundPivot(Quaternion delta)
    {
        delta.ToAngleAxis(out float angle, out Vector3 axis);
        if (float.IsNaN(axis.x) || float.IsInfinity(axis.x) || Mathf.Approximately(angle, 0f)) return;
        if (angle > 180f) angle -= 360f;
        transform.RotateAround(pivot, axis, angle);
    }

    // Nearest of the 24 orientations whose axes line up with the reference axes.
    private Quaternion NearestCubeOrientation(Quaternion currentLocal)
    {
        Quaternion relative = Quaternion.Inverse(referenceLocalRotation) * currentLocal;
        Vector3 forward = NearestAxis(relative * Vector3.forward, Vector3.zero);
        Vector3 up = NearestAxis(relative * Vector3.up, forward);
        return referenceLocalRotation * Quaternion.LookRotation(forward, up);
    }

    private static readonly Vector3[] Axes =
    {
        Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
    };

    // The axis closest to v, skipping any axis parallel to 'exclude'.
    private static Vector3 NearestAxis(Vector3 v, Vector3 exclude)
    {
        float best = float.NegativeInfinity;
        Vector3 result = Vector3.up;
        foreach (Vector3 axis in Axes)
        {
            if (exclude != Vector3.zero && Mathf.Abs(Vector3.Dot(axis, exclude)) > 0.5f) continue;
            float dot = Vector3.Dot(v, axis);
            if (dot > best) { best = dot; result = axis; }
        }
        return result;
    }

    private Vector3 GetVisualCenter()
    {
        bool found = false;
        Bounds bounds = default;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return found ? bounds.center : transform.position;
    }
}