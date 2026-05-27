using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FixedLabel : MonoBehaviour
{
    public Vector3 worldForward = new Vector3(0, 0, 1);

    private void LateUpdate()
    {
        // ¹Ì¶¨Ðý×ª£º³¯Ïò worldForward
        transform.rotation = Quaternion.LookRotation(worldForward, Vector3.up);
    }
}
