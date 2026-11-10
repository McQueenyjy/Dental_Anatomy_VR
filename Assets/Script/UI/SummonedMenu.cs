using UnityEngine;
using UnityEngine.InputSystem;

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

    public bool IsOpen => content != null && content.activeSelf;

    private void Awake()
    {
        if (content != null) content.SetActive(false);
    }

    private void OnEnable()
    {
        Listen(toggleAction, true);
        Listen(secondaryToggleAction, true);
    }

    private void OnDisable()
    {
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
    }

    public void Close()
    {
        if (content != null) content.SetActive(false);
    }

    private void PlaceInFront()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
        forward.Normalize();

        content.transform.position = cam.transform.position + forward * distance + Vector3.up * heightOffset;
        content.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }
}