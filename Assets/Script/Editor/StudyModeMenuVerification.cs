using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Opt-in end-to-end verification. Play Mode restores the user's editing scene on exit.
[InitializeOnLoad]
public static class StudyModeMenuVerification
{
    private const string StartRequest = "Temp/StudyModeMenuStart.request";
    private const string RunRequest = "Temp/StudyModeMenuVerification.request";
    private const string MenuPath = "Assets/Scenes/MainMenu.unity";
    private const string Output = "StudyModeBackup/20261027-menu-study-mode";
    private const string PreviousStartScene = "Dental.MenuVerification.PreviousStartScene";
    private static int checks, initialDifficulty;
    private static double deadline;
    [Serializable] private class Result { public bool passed; public int checks; public string error; }

    static StudyModeMenuVerification()
    {
        EditorApplication.delayCall += HandleRequest;
        EditorApplication.playModeStateChanged += OnPlayState;
    }

    private static void HandleRequest()
    {
        if (!File.Exists(StartRequest)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += HandleRequest; return; }
        File.Delete(StartRequest);
        StartVerification();
    }

    [MenuItem("Tools/Dental/Verify Main Menu to Study Mode")]
    public static void StartVerification()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var script = AssetDatabase.LoadAssetAtPath<MonoScript>(
            AssetDatabase.GUIDToAssetPath("29fc2f4881ee52b41b932d9a20ef1ce0"));
        if (script == null || script.GetClass() != typeof(MainMenu))
        { Finish("MainMenu MonoScript does not resolve to the MainMenu class."); return; }
        SessionState.SetString(PreviousStartScene, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuPath);
        File.WriteAllText(RunRequest, "Run once");
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(RunRequest))
            EditorApplication.delayCall += CheckMenuAndClick;
        if (state == PlayModeStateChange.EnteredEditMode && File.Exists(RunRequest))
        {
            File.Delete(RunRequest);
            string previous = SessionState.GetString(PreviousStartScene, "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.EraseString(PreviousStartScene);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }

    private static void CheckMenuAndClick()
    {
        checks = 0;
        try
        {
            Check(SceneManager.GetActiveScene().path == MenuPath, "Test did not start from MainMenu.");
            Check(EditorBuildSettings.scenes.Where(s => s.enabled).First().path == MenuPath, "MainMenu must be the first enabled scene.");
            Check(Application.CanStreamedLevelBeLoaded("StudyMode_1") && Application.CanStreamedLevelBeLoaded("SampleScene"), "A destination is missing from the build.");
            Button[] buttons = UnityEngine.Object.FindObjectsOfType<Button>();
            foreach (string[] entry in new[] { new[] { "Study Mode", "PlayStudyMode" }, new[] { "Easy", "PlayEasy" }, new[] { "Hard", "PlayHard" }, new[] { "Quit", "QuitGame" } })
            {
                Button button = buttons.Single(b => b.name == entry[0]);
                Check(button.interactable && button.onClick.GetPersistentEventCount() == 1 &&
                    button.onClick.GetPersistentTarget(0) is MainMenu &&
                    button.onClick.GetPersistentMethodName(0) == entry[1], "Invalid button binding: " + entry[0]);
            }
            Button study = buttons.Single(b => b.name == "Study Mode");
            Check(study.GetComponentInChildren<TMPro.TMP_Text>().text == "Study Mode", "Incorrect Study button label.");
            RectTransform panel = study.transform.parent as RectTransform;
            var ordered = buttons.Where(b => b.transform.parent == panel)
                .OrderByDescending(b => ((RectTransform)b.transform).anchoredPosition.y).ToArray();
            Check(ordered.Length == 4, "Expected four main menu buttons.");
            for (int i = 0; i < ordered.Length; i++)
            {
                var rect = (RectTransform)ordered[i].transform;
                float half = rect.rect.height * rect.localScale.y * .5f;
                Check(rect.anchoredPosition.y + half < panel.rect.yMax && rect.anchoredPosition.y - half > panel.rect.yMin,
                    "Button is outside the panel: " + ordered[i].name);
                if (i > 0)
                {
                    var previous = (RectTransform)ordered[i - 1].transform;
                    Check(previous.anchoredPosition.y - previous.rect.height * previous.localScale.y * .5f > rect.anchoredPosition.y + half,
                        "Buttons overlap: " + previous.name + " y=" + previous.anchoredPosition.y + " height=" + previous.rect.height + " scale=" + previous.localScale.y +
                        "; " + rect.name + " y=" + rect.anchoredPosition.y + " height=" + rect.rect.height + " scale=" + rect.localScale.y);
                }
            }
            RenderMenu(panel);
            initialDifficulty = GameSettings.difficulty;
            SceneManager.sceneLoaded += OnSceneLoaded;
            deadline = EditorApplication.timeSinceStartup + 30;
            EditorApplication.update += CheckTimeout;
            study.onClick.Invoke();
        }
        catch (Exception error) { Finish(error.ToString()); }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "StudyMode_1") EditorApplication.delayCall += CheckStudy;
    }

    private static void CheckStudy()
    {
        try
        {
            Check(SceneManager.GetActiveScene().name == "StudyMode_1", "Study scene did not become active.");
            Check(UnityEngine.Object.FindObjectOfType<StudyModeManager>() != null, "StudyModeManager is missing.");
            Check(UnityEngine.Object.FindObjectOfType<AnatomyHotspotManager>() != null, "Anatomy manager is missing.");
            Check(GameSettings.difficulty == initialDifficulty, "Study Mode changed puzzle difficulty.");
            AnatomyInfoPanel panel = UnityEngine.Object.FindObjectOfType<AnatomyInfoPanel>();
            Check(panel != null && panel.descriptionText != null && panel.descriptionText.text.Contains("Grip"), "The authored study instructions were not preserved.");
            string original = panel.descriptionText.text;
            panel.ShowInfo("Verification", "Temporary anatomy description");
            panel.ClearInfo();
            Check(panel.descriptionText.text == original, "Clearing anatomy information lost the authored instructions.");
            Finish(null);
        }
        catch (Exception error) { Finish(error.ToString()); }
    }

    private static void CheckTimeout()
    {
        if (EditorApplication.timeSinceStartup > deadline) Finish("Timed out waiting for the Study Mode scene.");
    }

    private static void Finish(string error)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EditorApplication.update -= CheckTimeout;
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "/verification.json", JsonUtility.ToJson(new Result { passed = error == null, checks = checks, error = error }, true));
        if (error == null) Debug.Log("Main Menu to Study Mode verification passed: " + checks + " checks.");
        else Debug.LogError(error);
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
    }

    private static void RenderMenu(RectTransform panel)
    {
        Directory.CreateDirectory(Output);
        var cameraObject = new GameObject("Temporary menu preview camera");
        var camera = cameraObject.AddComponent<Camera>();
        var target = new RenderTexture(800, 1000, 24);
        RenderTexture previous = RenderTexture.active;
        try
        {
            Vector3[] corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            Vector3 center = (corners[0] + corners[2]) * .5f;
            float height = Vector3.Distance(corners[0], corners[1]);
            camera.transform.SetPositionAndRotation(center - panel.forward * (height * 2), Quaternion.LookRotation(panel.forward, panel.up));
            camera.orthographic = true;
            camera.orthographicSize = height * .56f;
            camera.nearClipPlane = .01f;
            camera.farClipPlane = height * 4 + 10;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.07f, .08f, .1f);
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Output + "/menu.png", image.EncodeToPNG());
            UnityEngine.Object.Destroy(image);
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            UnityEngine.Object.Destroy(target);
            UnityEngine.Object.Destroy(cameraObject);
        }
    }
}
