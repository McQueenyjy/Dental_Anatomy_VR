using UnityEngine;
using UnityEngine.SceneManagement;

// Actions for the settings menu buttons. Hides buttons that don't apply in this scene.
public class SettingsMenuActions : MonoBehaviour
{
    public SummonedMenu menu;
    public GameObject recenterButton;
    public GameObject backButton;
    public string mainMenuScene = "MainMenu";

    private StudyModeFollowMenu followMenu;

    private void OnEnable()
    {
        if (followMenu == null) followMenu = FindObjectOfType<StudyModeFollowMenu>();
        if (recenterButton != null) recenterButton.SetActive(followMenu != null);
        if (backButton != null) backButton.SetActive(SceneManager.GetActiveScene().name != mainMenuScene);
    }

    public void Close()
    {
        if (menu != null) menu.Close();
    }

    public void Recenter()
    {
        if (followMenu != null) followMenu.SnapToHead();
        Close();
    }

    public void BackToMainMenu() => SceneManager.LoadScene(mainMenuScene);
}