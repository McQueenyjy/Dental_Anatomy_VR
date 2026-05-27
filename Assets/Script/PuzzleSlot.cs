using UnityEngine;

public class PuzzleSlot : MonoBehaviour
{
    [Header("Accept ID")]
    public string acceptID;

    public enum SlotRow
    {
        Upper,
        Lower
    }

    [Header("Slot Row")]
    public SlotRow slotRow = SlotRow.Lower;

    [Header("Eject Direction")]
    public Transform ejectDirectionRef;


    [Header("Status")]
    public bool isFilled = false;

    [Header("Inspection")]
    public InspectionModeManager inspectionManager;



    private void OnTriggerEnter(Collider other)
    {
        if (inspectionManager != null && inspectionManager.IsInspectionMode())
            return;

        var piece = other.GetComponentInParent<PuzzlePiece>();
        if (piece == null) return;
        if (piece.isPlaced) return;
        if (isFilled) return;

        piece.NotifyTouchedAnySlot(this);

        piece.RegisterCandidateSlot(this);
    }

    private void OnTriggerStay(Collider other)
    {
        if (inspectionManager != null && inspectionManager.IsInspectionMode())
            return;

        // Stay ×ö¶µµ×£¬±ÜÃâ Enter Â©µô
        var piece = other.GetComponentInParent<PuzzlePiece>();
        if (piece == null) return;
        if (piece.isPlaced) return;
        if (isFilled) return;

        piece.NotifyTouchedAnySlot(this);

        piece.RegisterCandidateSlot(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (inspectionManager != null && inspectionManager.IsInspectionMode())
            return;

        var piece = other.GetComponentInParent<PuzzlePiece>();
        if (piece == null) return;

        piece.UnregisterCandidateSlot(this);
    }

    public void ResetSlot()
    {
        isFilled = false;
    }
}