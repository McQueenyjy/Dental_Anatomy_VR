using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

public class InGameMenuController : MonoBehaviour
{
    [Header("Refs")]
    public Canvas menuCanvas;                 // 你的World Space Canvas
    public PuzzleGameManager puzzleManager;   // 场景里的PuzzleGameManager
    public Transform xrCamera;                // XR Origin/Main Camera

    [Header("Toggle Input")]
    public InputActionReference toggleMenuAction;
    public InputActionReference secondaryToggleMenuAction;
    [SerializeField] private InputActionManager inputManager = null;

    [Header("Placement")]
    public float distance = 1.0f;
    public float heightOffset = -0.05f;

    private bool isOpen;

    private void OnEnable()
    {
        SetMenuListener(toggleMenuAction, true);
        SetMenuListener(secondaryToggleMenuAction, true);
    }

    private void OnDisable()
    {
        SetMenuListener(toggleMenuAction, false);
        SetMenuListener(secondaryToggleMenuAction, false);
    }

    private void SetMenuListener(InputActionReference reference, bool subscribe)
    {
        InputAction action = reference != null ? reference.action : null;
        if (action == null) return;

        if (subscribe) action.performed += OnTogglePerformed;
        else action.performed -= OnTogglePerformed;
    }

    private void Start()
    {
        // Re-enable the shared XR actions after the outgoing scene has shut down.
        if (inputManager != null) inputManager.EnableInput();
        toggleMenuAction?.action?.Enable();
        secondaryToggleMenuAction?.action?.Enable();
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

    public void OnBackToMainMenuClicked()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
