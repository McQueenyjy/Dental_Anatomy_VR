using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

// Explicit, one-shot scene migration. It never edits model or tooth prefab assets.
[InitializeOnLoad]
public static class StudyModeSharedViewerSetup
{
    public const string ScenePath = "Assets/Scenes/StudyMode_1.unity";
    public const string Output = "StudyModeBackup/20261027-shared-tooth-viewer";
    private const string Request = "Temp/StudyModeSharedViewerSetup.request";

    static StudyModeSharedViewerSetup()
    {
        EditorApplication.delayCall += HandleRequest;
    }

    private static void HandleRequest()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += HandleRequest; return; }
        File.Delete(Request);
        Configure();
    }

    [MenuItem("Tools/Dental/Configure Shared Tooth Viewer")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        try
        {
            if (!opened && scene.isDirty)
                throw new InvalidOperationException("Save the current Study Mode scene before configuring its viewer.");
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            Transform menu = scene.GetRootGameObjects().Single(o => o.name == "Menu").transform;
            StudyModeManager manager = scene.GetRootGameObjects()
                .SelectMany(o => o.GetComponentsInChildren<StudyModeManager>(true)).Single();
            if (manager.toothSelectionRoot != null)
                throw new InvalidOperationException("The shared viewer is already configured.");
            Transform left = menu.Find("Left_Panel");
            Transform viewer = menu.Find("Central_Panel");
            if (left == null || viewer == null || manager.currentToothRoot == null)
                throw new InvalidOperationException("Expected the original left and central panels and tooth spawn point.");

            viewer.name = "Tooth_Viewer_Panel";
            viewer.localPosition = new Vector3(-1f, 0f, 2f);
            // Preserve the complete arch's authored pose and size inside its group.
            left.SetParent(viewer, false);
            left.name = "Tooth_Selection";
            left.localPosition = Vector3.zero;
            left.Find("Canvas").gameObject.SetActive(false);

            RectTransform canvasRect = (RectTransform)viewer.Find("Canvas");
            canvasRect.sizeDelta = new Vector2(1900f, 1400f);
            canvasRect.localPosition = new Vector3(0f, 0f, .8f);
            RectTransform background = (RectTransform)canvasRect.Find("Panel");
            background.localScale = Vector3.one;
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = background.offsetMax = Vector2.zero;
            Image backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = new Color(1f, 1f, 1f, .13333334f);
            backgroundImage.raycastTarget = false;
            canvasRect.GetComponent<Canvas>().additionalShaderChannels |=
                AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            canvasRect.GetComponent<GraphicRaycaster>().enabled = true;
            var xrRaycaster = canvasRect.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            xrRaycaster.ignoreReversedGraphics = false;
            xrRaycaster.checkFor2DOcclusion = false;
            xrRaycaster.checkFor3DOcclusion = false;

            TMP_Text existingTitle = menu.Find("Right Panel").GetComponent<AnatomyInfoPanel>().titleText;
            TextMeshProUGUI heading = AddText("Viewer_Title", canvasRect, existingTitle.font, "Study Mode", 48);
            heading.color = existingTitle.color;
            heading.fontStyle = FontStyles.Bold;
            SetRect(heading.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                new Vector2(0, -55), new Vector2(800, 90));

            var buttonObject = new GameObject("Back_To_Teeth", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.layer = canvasRect.gameObject.layer;
            buttonObject.transform.SetParent(canvasRect, false);
            SetRect((RectTransform)buttonObject.transform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(60, -60), new Vector2(330, 85));
            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.sprite = backgroundImage.sprite;
            buttonImage.type = Image.Type.Sliced;
            buttonImage.color = new Color(.12f, .36f, .39f, 1f);
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = buttonImage;
            var colors = button.colors;
            colors.highlightedColor = new Color(.8f, 1f, 1f);
            colors.pressedColor = new Color(.6f, .8f, .8f);
            button.colors = colors;
            UnityEventTools.AddPersistentListener(button.onClick, manager.ReturnToToothSelection);
            TextMeshProUGUI label = AddText("Label", buttonObject.transform, existingTitle.font, "Back to teeth", 32);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.color = Color.white;

            manager.toothSelectionRoot = left.gameObject;
            manager.backToTeethButton = buttonObject;
            manager.spawnFirstOnStart = false;
            left.gameObject.SetActive(true);
            manager.currentToothRoot.gameObject.SetActive(false);
            buttonObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager);

            // Keep instructions authored in the scene, consistent with AnatomyInfoPanel.
            AnatomyInfoPanel info = menu.Find("Right Panel").GetComponent<AnatomyInfoPanel>();
            info.descriptionText.text = "Press \"Grip\" to select a tooth.\n\nSelect structure categories below to show their colors. Point at a structure to view its name and description.\n\nUse \"Back to teeth\" to choose another tooth.";
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save the Study Mode scene.");
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/setup.txt", "Shared viewer saved. Arch and tooth transforms preserved; right info panel retained.\n");
            Debug.Log("Shared tooth viewer configured in StudyMode_1.");
        }
        catch (Exception error)
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/setup-error.txt", error.ToString());
            Debug.LogException(error);
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static TextMeshProUGUI AddText(string name, Transform parent, TMP_FontAsset font, string text, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
