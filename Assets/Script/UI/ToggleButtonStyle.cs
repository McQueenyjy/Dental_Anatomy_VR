using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shows a Toggle as a button: filled with onColor when on, grey when off.
[RequireComponent(typeof(Toggle))]
public class ToggleButtonStyle : MonoBehaviour
{
    public Image background;
    public TMP_Text label;
    public Color onColor = new Color(0.2f, 0.55f, 0.85f);
    public Color offColor = new Color(0.85f, 0.85f, 0.85f);
    public Color onTextColor = Color.white;
    public Color offTextColor = new Color(0.25f, 0.25f, 0.25f);

    private Toggle toggle;

    private void Awake()
    {
        toggle = GetComponent<Toggle>();
        if (background == null) background = toggle.targetGraphic as Image;
        if (label == null) label = GetComponentInChildren<TMP_Text>();
        toggle.onValueChanged.AddListener(_ => Refresh());
    }

    private void OnEnable() => Refresh();

    public void Refresh()
    {
        bool on = toggle != null && toggle.isOn;
        if (background != null) background.color = on ? onColor : offColor;
        if (label != null) label.color = on ? onTextColor : offTextColor;
    }
}