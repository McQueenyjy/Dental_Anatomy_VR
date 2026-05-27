using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Billboard : MonoBehaviour
{
    public Transform targetCamera;

    private void Start()
    {
        if (targetCamera == null && Camera.main != null)
        {
            targetCamera = Camera.main.transform;
        }
    }

    /*private void LateUpdate()
    {
        if (targetCamera == null) return;

        // 看向相机方向
        Vector3 dir = transform.position - targetCamera.position;

        // 只在水平面上转动，避免前仰后仰导致文字倾斜
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion lookRot = Quaternion.LookRotation(dir);
        transform.rotation = lookRot;
    }
    */
    //added using ChatGPT
    [Header("Anti-occlusion")]
    public float pushTowardCamera = 0.25f; // 0.02 ~ 0.05?

    private Vector3 baseLocalPos;

    private void LateUpdate()
    {
        if (targetCamera == null) return;

        // === reset position ===
        transform.localPosition = baseLocalPos;

        // === billboard ===
        Vector3 dir = transform.position - targetCamera.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion lookRot = Quaternion.LookRotation(dir);
        transform.rotation = lookRot;

        // === push toward camera along z-axis to avoid blocking ===
        Vector3 toCam = (targetCamera.position - transform.position).normalized;
        transform.position += toCam * pushTowardCamera;
    }
}
