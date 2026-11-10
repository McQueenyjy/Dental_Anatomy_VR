using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class XRModeManager : MonoBehaviour
{
    public enum Mode { AR, VR }

    // Shared across scenes, so the choice carries from the main menu into study mode.
    public static Mode CurrentMode = Mode.AR;

    [Header("References")]
    public Camera xrCamera;
    public ARCameraManager arCameraManager;
    [Tooltip("Objects only shown in VR, such as the ground plane or virtual room.")]
    public GameObject[] vrOnlyObjects;

    [Header("VR Background")]
    public Material vrSkybox;

    private void Start()
    {
        Apply();
    }

    public void ToggleMode()
    {
        CurrentMode = CurrentMode == Mode.AR ? Mode.VR : Mode.AR;
        Apply();
    }

    public void SetAR() { CurrentMode = Mode.AR; Apply(); }
    public void SetVR() { CurrentMode = Mode.VR; Apply(); }

    private void Apply()
    {
        bool ar = CurrentMode == Mode.AR;

        if (arCameraManager != null) arCameraManager.enabled = ar;

        if (xrCamera != null)
        {
            if (ar)
            {
                xrCamera.clearFlags = CameraClearFlags.SolidColor;
                xrCamera.backgroundColor = new Color(0f, 0f, 0f, 0f); // transparent so the room shows through
            }
            else
            {
                xrCamera.clearFlags = CameraClearFlags.Skybox;
                if (vrSkybox != null) RenderSettings.skybox = vrSkybox;
            }
        }

        foreach (GameObject go in vrOnlyObjects)
            if (go != null) go.SetActive(!ar);
    }
}