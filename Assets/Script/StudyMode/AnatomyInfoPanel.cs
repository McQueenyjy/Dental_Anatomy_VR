using TMPro;
using UnityEngine;

public class AnatomyInfoPanel : MonoBehaviour
{
    [Header("UI Text")]
    public TMP_Text titleText;
    public TMP_Text descriptionText;

    [Header("Default Text")]
    public string defaultTitle = "Anatomy";
    public string defaultDescription = "Select structure categories to show their colors. Point at a highlighted structure to view its name and description.";

    private void Awake()
    {
        ClearInfo();
    }

    public void ShowInfo(string title, string description)
    {
        if (titleText != null)
        {
            titleText.text = title;
        }

        if (descriptionText != null)
        {
            descriptionText.text = description;
        }
    }

    public void ClearInfo()
    {
        if (titleText != null)
        {
            titleText.text = defaultTitle;
        }

        if (descriptionText != null)
        {
            descriptionText.text = defaultDescription;
        }
    }
}
