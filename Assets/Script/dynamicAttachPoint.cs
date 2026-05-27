using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class DynamicAttachPoint : MonoBehaviour
{
    private XRGrabInteractable grab;
    private Transform dynamicAttach;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();

        
        dynamicAttach = new GameObject("DynamicAttachPoint").transform;
        dynamicAttach.SetParent(transform);
    }

    private void OnEnable()
    {
        grab.selectEntered.AddListener(OnGrab);
    }

    private void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnGrab);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        
        dynamicAttach.position = transform.position;
        dynamicAttach.rotation = transform.rotation;

        
        grab.attachTransform = dynamicAttach;
    }
}
