using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Button that switches between AR and VR. Hidden if the scene has no XRModeManager.
[RequireComponent(typeof(Button))]
public class XRModeButton : MonoBehaviour
{
    public TMP_Text label;

    private XRModeManager modeManager;

    private void Awake()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>();
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    private void OnEnable()
    {
        if (modeManager == null) modeManager = FindObjectOfType<XRModeManager>();
        if (modeManager == null) { gameObject.SetActive(false); return; }
        Refresh();
    }

    private void OnClick()
    {
        modeManager.ToggleMode();
        Refresh();
    }

    private void Refresh()
    {
        if (label != null)
            // label.text = XRModeManager.CurrentMode == XRModeManager.Mode.AR ? "Switch to VR" : "Switch to AR";
            label.text = "AR / VR";
    }
}