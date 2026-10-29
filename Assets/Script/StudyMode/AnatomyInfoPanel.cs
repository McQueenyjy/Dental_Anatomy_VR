using TMPro;
using UnityEngine;

public class AnatomyInfoPanel : MonoBehaviour
{
    [Header("UI Text")]
    public TMP_Text titleText;
    public TMP_Text descriptionText;

    [Header("Default Text")]
    [Tooltip("Use the initial non-empty TextMeshPro text as the default. Empty text fields use the defaults below.")]
    public bool useInitialTextAsDefault = true;
    public string defaultTitle = "Anatomy";
    [TextArea(3, 8)]
    public string defaultDescription = "Select structure categories to show their colors. Point at a highlighted structure to view its name and description.";

    private bool defaultsInitialized;

    private void InitializeDefaultText()
    {
        if (defaultsInitialized) return;
        defaultsInitialized = true;
        if (!useInitialTextAsDefault) return;
        if (titleText != null && !string.IsNullOrWhiteSpace(titleText.text))
            defaultTitle = titleText.text;
        if (descriptionText != null && !string.IsNullOrWhiteSpace(descriptionText.text))
            defaultDescription = descriptionText.text;
    }

    private void Awake()
    {
        ClearInfo();
    }

    public void ShowInfo(string title, string description)
    {
        // Cache authored text before any runtime update, even if called before Awake.
        InitializeDefaultText();
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
        InitializeDefaultText();
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
