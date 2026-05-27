using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class TwoHandScale : MonoBehaviour
{
    private XRGrabInteractable grab;
    private XRBaseInteractor firstHand;
    private XRBaseInteractor secondHand;

    private float initialDistance;
    private Vector3 initialScale;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();

        
        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);
    }

    void OnDestroy()
    {
        grab.selectEntered.RemoveListener(OnSelectEntered);
        grab.selectExited.RemoveListener(OnSelectExited);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (firstHand == null)
        {
            firstHand = args.interactorObject as XRBaseInteractor;
        }
        else if (secondHand == null)
        {
            secondHand = args.interactorObject as XRBaseInteractor;
           
            initialDistance = Vector3.Distance(firstHand.transform.position, secondHand.transform.position);
            initialScale = transform.localScale;
        }
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        XRBaseInteractor interactor = args.interactorObject as XRBaseInteractor;

        if (interactor == secondHand)
        {
            secondHand = null;
        }
        if (interactor == firstHand)
        {
            
            firstHand = secondHand;
            secondHand = null;
        }
    }

    void Update()
    {
        if (firstHand != null && secondHand != null)
        {
            float currentDistance = Vector3.Distance(firstHand.transform.position, secondHand.transform.position);
            if (initialDistance > 0.0001f)
            {
                float scaleFactor = currentDistance / initialDistance;
                transform.localScale = initialScale * scaleFactor;
            }
        }
    }
}
