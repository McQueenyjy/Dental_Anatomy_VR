using UnityEngine;

public class FollowHMD : MonoBehaviour
{
    public Transform head;           // Main Camera (HMD)
    public float distance = 1.2f;    // 距离眼睛多远
    public float heightOffset = -0.1f;
    public float followSpeed = 12f;  // 越大越跟手

    void Reset()
    {
        if (Camera.main) head = Camera.main.transform;
    }

    void LateUpdate()
    {
        if (!head) return;

        
        Vector3 forward = head.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 targetPos = head.position + forward * distance + Vector3.up * heightOffset;
        Quaternion targetRot = Quaternion.LookRotation(forward, Vector3.up);

        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * followSpeed);
    }
}