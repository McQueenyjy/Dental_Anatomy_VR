using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

public class InspectionModeManager : MonoBehaviour
{
    [Header("Root")]
    public Transform skullRig;

    [Header("Skull Grab")]
    public XRGrabInteractable skullGrabInteractable;
    public Rigidbody skullRigidbody;

    [Header("Input")]
    public InputActionReference toggleAction;   // A button
    public InputActionReference scaleAction;    // left thumbstick Vector2

    [Header("Scale")]
    public float minScaleMultiplier = 0.6f;
    public float maxScaleMultiplier = 1.5f;
    public float scaleSensitivity = 0.4f;
    public float scaleDeadzone = 0.2f;

    [Header("Puzzle References")]
    public List<PuzzlePiece> puzzlePieces = new List<PuzzlePiece>();
    public List<PuzzleSlot> puzzleSlots = new List<PuzzleSlot>();

    [Header("Locomotion Disable")]
    public Behaviour moveProvider;
    public Behaviour turnProvider;

    private bool isInspectionMode = false;
    private bool isHoldingSkull = false;

    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private Vector3 savedScale;

    private float baseInspectionScale = 1f;
    private float currentInspectionScale = 1f;

    private Dictionary<XRGrabInteractable, bool> grabStateMap = new Dictionary<XRGrabInteractable, bool>();
    private Dictionary<PuzzleSlot, bool> slotStateMap = new Dictionary<PuzzleSlot, bool>();
    private Dictionary<Collider, bool> pieceColliderStateMap = new Dictionary<Collider, bool>();

    private bool cachedMoveProviderEnabled;
    private bool cachedTurnProviderEnabled;

    private void Awake()
    {
        if (skullGrabInteractable != null)
        {
            skullGrabInteractable.selectEntered.AddListener(OnSkullGrabbed);
            skullGrabInteractable.selectExited.AddListener(OnSkullReleased);
        }
    }

    private void OnDestroy()
    {
        if (skullGrabInteractable != null)
        {
            skullGrabInteractable.selectEntered.RemoveListener(OnSkullGrabbed);
            skullGrabInteractable.selectExited.RemoveListener(OnSkullReleased);
        }
    }

    private void OnEnable()
    {
        if (toggleAction != null) toggleAction.action.Enable();
        if (scaleAction != null) scaleAction.action.Enable();
    }

    private void OnDisable()
    {
        if (toggleAction != null) toggleAction.action.Disable();
        if (scaleAction != null) scaleAction.action.Disable();
    }

    private void Start()
    {
        EnableSkullGrab(false);

        if (skullRigidbody != null)
        {
            skullRigidbody.isKinematic = true;
            skullRigidbody.useGravity = false;
            skullRigidbody.velocity = Vector3.zero;
            skullRigidbody.angularVelocity = Vector3.zero;
        }
    }

    private void Update()
    {
        if (toggleAction != null && toggleAction.action.WasPressedThisFrame())
        {
            ToggleInspectionMode();
        }
    }

    private void LateUpdate()
    {
        if (!isInspectionMode) return;
        HandleScale();
    }

    public bool IsInspectionMode()
    {
        return isInspectionMode;
    }

    public void ToggleInspectionMode()
    {
        if (isInspectionMode)
            ExitInspectionMode();
        else
            EnterInspectionMode();
    }

    public void EnterInspectionMode()
    {
        if (IsAnyPieceBeingHeld()) return;
        if (IsSkullBeingHeld()) return;

        isInspectionMode = true;
        isHoldingSkull = false;

        savedPosition = skullRig.position;
        savedRotation = skullRig.rotation;
        savedScale = skullRig.localScale;

        baseInspectionScale = savedScale.x;
        currentInspectionScale = savedScale.x;

        DisablePuzzleInteraction();
        DisableLooseTeethColliders();
        EnableSkullGrab(true);
    }

    public void ExitInspectionMode()
    {
        // 为了避免抓着 skull 时强退导致状态错乱
        if (IsSkullBeingHeld()) return;

        isInspectionMode = false;
        isHoldingSkull = false;

        EnableSkullGrab(false);
        RestoreLooseTeethColliders();

        if (moveProvider != null)
            moveProvider.enabled = cachedMoveProviderEnabled;

        if (turnProvider != null)
            turnProvider.enabled = cachedTurnProviderEnabled;

        skullRig.position = savedPosition;
        skullRig.rotation = savedRotation;
        skullRig.localScale = savedScale;

        RestorePuzzleInteraction();
    }

    private bool IsAnyPieceBeingHeld()
    {
        foreach (var piece in puzzlePieces)
        {
            if (piece == null) continue;

            XRGrabInteractable grab = piece.GetComponent<XRGrabInteractable>();
            if (grab != null && grab.isSelected)
                return true;
        }
        return false;
    }

    private bool IsSkullBeingHeld()
    {
        return skullGrabInteractable != null && skullGrabInteractable.isSelected;
    }

    private void EnableSkullGrab(bool enabled)
    {
        if (skullGrabInteractable != null)
            skullGrabInteractable.enabled = enabled;

        if (skullRigidbody != null)
        {
            skullRigidbody.isKinematic = true;
            skullRigidbody.useGravity = false;
            skullRigidbody.velocity = Vector3.zero;
            skullRigidbody.angularVelocity = Vector3.zero;
        }
    }

    private void DisablePuzzleInteraction()
    {
        grabStateMap.Clear();
        slotStateMap.Clear();

        foreach (var piece in puzzlePieces)
        {
            if (piece == null) continue;

            XRGrabInteractable grab = piece.GetComponent<XRGrabInteractable>();
            if (grab != null)
            {
                grabStateMap[grab] = grab.enabled;
                grab.enabled = false;
            }
        }

        foreach (var slot in puzzleSlots)
        {
            if (slot == null) continue;

            slotStateMap[slot] = slot.enabled;
            slot.enabled = false;
        }
    }

    private void RestorePuzzleInteraction()
    {
        foreach (var kv in grabStateMap)
        {
            if (kv.Key != null)
                kv.Key.enabled = kv.Value;
        }

        foreach (var kv in slotStateMap)
        {
            if (kv.Key != null)
                kv.Key.enabled = kv.Value;
        }
    }

    private void DisableLooseTeethColliders()
    {
        pieceColliderStateMap.Clear();

        foreach (var piece in puzzlePieces)
        {
            if (piece == null) continue;
            if (piece.isPlaced) continue; // 已拼好的牙不处理

            Collider[] colliders = piece.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                if (col == null) continue;

                pieceColliderStateMap[col] = col.enabled;
                col.enabled = false;
            }
        }
    }

    private void RestoreLooseTeethColliders()
    {
        foreach (var kv in pieceColliderStateMap)
        {
            if (kv.Key != null)
                kv.Key.enabled = kv.Value;
        }
    }

    private void OnSkullGrabbed(SelectEnterEventArgs args)
    {
        isHoldingSkull = true;

        if (moveProvider != null)
        {
            cachedMoveProviderEnabled = moveProvider.enabled;
            moveProvider.enabled = false;
        }

        if (turnProvider != null)
        {
            cachedTurnProviderEnabled = turnProvider.enabled;
            turnProvider.enabled = false;
        }
    }

    private void OnSkullReleased(SelectExitEventArgs args)
    {
        isHoldingSkull = false;

        if (moveProvider != null)
            moveProvider.enabled = cachedMoveProviderEnabled;

        if (turnProvider != null)
            turnProvider.enabled = cachedTurnProviderEnabled;
    }

    private void HandleScale()
    {
        if (scaleAction == null) return;
        if (!isInspectionMode) return;

        float minAllowed = baseInspectionScale * minScaleMultiplier;
        float maxAllowed = baseInspectionScale * maxScaleMultiplier;

        
        if (isHoldingSkull)
        {
            Vector2 input = scaleAction.action.ReadValue<Vector2>();
            float y = input.y;

            if (Mathf.Abs(y) >= scaleDeadzone)
            {
                currentInspectionScale += y * scaleSensitivity * Time.deltaTime;
                currentInspectionScale = Mathf.Clamp(currentInspectionScale, minAllowed, maxAllowed);
            }
        }

        
        skullRig.localScale = Vector3.one * currentInspectionScale;
    }
}