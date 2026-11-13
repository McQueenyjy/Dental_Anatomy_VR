using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class XRUIHapticFeedback : MonoBehaviour, IPointerClickHandler
{
    [Range(0f, 1f)]
    [SerializeField] private float amplitude = 0.35f;

    [SerializeField] private float duration = 0.08f;

    public void OnPointerClick(PointerEventData eventData)
    {
        TrackedDeviceEventData trackedEvent =
            eventData as TrackedDeviceEventData;

        if (trackedEvent == null)
            return;

        XRBaseControllerInteractor controllerInteractor =
            trackedEvent.interactor as XRBaseControllerInteractor;

        if (controllerInteractor != null)
        {
            controllerInteractor.SendHapticImpulse(
                amplitude,
                duration);
        }
    }
}