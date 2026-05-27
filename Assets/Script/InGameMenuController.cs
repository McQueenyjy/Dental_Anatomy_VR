using UnityEngine;
using UnityEngine.InputSystem;

public class InGameMenuController : MonoBehaviour
{
    [Header("Refs")]
    public Canvas menuCanvas;                 // 你的World Space Canvas
    public PuzzleGameManager puzzleManager;   // 场景里的PuzzleGameManager
    public Transform xrCamera;                // XR Origin/Main Camera

    [Header("Toggle Input (recommended: A or X)")]
    public InputActionReference toggleMenuAction;

    [Header("Placement")]
    public float distance = 1.0f;
    public float heightOffset = -0.05f;

    private bool isOpen;

    private void OnEnable()
    {
        if (toggleMenuAction != null)
        {
            toggleMenuAction.action.performed += OnTogglePerformed;
            toggleMenuAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (toggleMenuAction != null)
        {
            toggleMenuAction.action.performed -= OnTogglePerformed;
            toggleMenuAction.action.Disable();
        }
    }

    private void Start()
    {
        SetOpen(false);
    }

    private void OnTogglePerformed(InputAction.CallbackContext ctx)
    {
        SetOpen(!isOpen);
    }

    public void SetOpen(bool open)
    {
        isOpen = open;

        if (menuCanvas != null)
            menuCanvas.enabled = open;

        if (open)
            RepositionMenu();
    }

    private void LateUpdate()
    {
        if (isOpen)
            RepositionMenu();
    }

    private void RepositionMenu()
    {
        if (menuCanvas == null || xrCamera == null) return;

        Vector3 forward = xrCamera.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 pos = xrCamera.position + forward * distance;
        pos.y = xrCamera.position.y + heightOffset;

        menuCanvas.transform.position = pos;
        menuCanvas.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    // ===== UI Buttons call these =====
    public void OnResumeClicked() => SetOpen(false);

    public void OnEasyClicked()
    {
        puzzleManager.SwitchToEasy();
        SetOpen(false);
    }

    public void OnHardClicked()
    {
        puzzleManager.SwitchToHard();
        SetOpen(false);
    }

    public void OnQuitClicked()
    {
        Application.Quit();
    }
}
