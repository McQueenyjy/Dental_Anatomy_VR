using UnityEngine;
using UnityEngine.SceneManagement;

// Actions for the settings menu buttons. Hides buttons that don't apply in this scene.
public class SettingsMenuActions : MonoBehaviour
{
    public SummonedMenu menu;
    public GameObject recenterButton;
    public GameObject backButton;
    public string mainMenuScene = "MainMenu";

    private PlaceInFrontOfUser placement;

    [Tooltip("Shown only while a tooth is spawned in study mode (always shown in scenes without a StudyModeManager).")]
    public GameObject structuresSection;

    private StudyModeManager studyManager;

    private void OnEnable()
    {
        if (placement == null) placement = FindObjectOfType<PlaceInFrontOfUser>();
        if (recenterButton != null) recenterButton.SetActive(placement != null);
        if (backButton != null) backButton.SetActive(SceneManager.GetActiveScene().name != mainMenuScene);
        if (studyManager == null) studyManager = FindObjectOfType<StudyModeManager>();
        if (structuresSection != null)
            structuresSection.SetActive(studyManager == null || studyManager.CurrentTooth != null);
    }

    public void Close()
    {
        if (menu != null) menu.Close();
    }

    public void Recenter()
    {
        if (placement != null) placement.SnapToHead();
        Close();
    }

    public void BackToMainMenu() => SceneManager.LoadScene(mainMenuScene);

    public void QuitApp()
    {
#if UNITY_EDITOR
        // Application.Quit is ignored in the Editor, so stop Play mode instead.
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}