using UnityEngine;

public class StudyModeFollowMenu : MonoBehaviour
{
    [Header("Target")]
    public Transform head;

    [Header("Placement")]
    public float distanceFromHead = 1.6f;
    public float heightOffset = -0.05f;

    [Header("Follow")]
    public float positionSmooth = 8f;
    public float rotationSmooth = 8f;

    private void Start()
    {
        if (head == null && Camera.main != null)
        {
            head = Camera.main.transform;
        }

        SnapToHead();
    }

    private void LateUpdate()
    {
        if (head == null) return;

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
}