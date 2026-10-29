using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

// Opt-in integration check: real scene loading, XR selection and UI button events.
[InitializeOnLoad]
public static class StudyModeSharedViewerVerification
{
    private const string StartRequest = "Temp/StudyModeSharedViewerStart.request";
    private const string RunRequest = "Temp/StudyModeSharedViewerRun.request";
    private const string PreviousStart = "Dental.SharedViewer.PreviousStart";
    private static readonly List<string> errors = new List<string>();
    private static readonly List<string> warnings = new List<string>();
    private static readonly List<string> passedChecks = new List<string>();
    private static IEnumerator routine;
    private static double deadline;
    private static int lastFrame;

    [Serializable]
    private class Result
    {
        public bool passed;
        public string failure;
        public string[] checks, consoleErrors, consoleWarnings;
    }

    static StudyModeSharedViewerVerification()
    {
        EditorApplication.delayCall += HandleRequest;
        EditorApplication.playModeStateChanged += OnPlayState;
        if (File.Exists(RunRequest)) Application.logMessageReceived += CaptureLog;
    }

    private static void HandleRequest()
    {
        if (!File.Exists(StartRequest)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += HandleRequest; return; }
        File.Delete(StartRequest);
        StartVerification();
    }

    [MenuItem("Tools/Dental/Verify Shared Tooth Viewer")]
    public static void StartVerification()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetString(PreviousStart, AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MainMenu.unity");
        File.WriteAllText(RunRequest, "Run once");
        errors.Clear(); warnings.Clear(); passedChecks.Clear();
        Application.logMessageReceived -= CaptureLog;
        Application.logMessageReceived += CaptureLog;
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayState(PlayModeStateChange state)
    {
        if (!File.Exists(RunRequest)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            routine = Run();
            deadline = EditorApplication.timeSinceStartup + 90;
            lastFrame = -1;
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= CaptureLog;
            File.Delete(RunRequest);
            string previous = SessionState.GetString(PreviousStart, "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous)
                ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.EraseString(PreviousStart);
        }
    }

    private static void CaptureLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(message + "\n" + stack);
        else if (type == LogType.Warning) warnings.Add(message);
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline) { Finish("Verification timed out."); return; }
        if (!EditorApplication.isPlaying || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try { if (!routine.MoveNext()) Finish(null); }
        catch (Exception error) { Finish(error.ToString()); }
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        passedChecks.Add(description);
    }

    private static StudyModeManager Manager() => UnityEngine.Object.FindObjectOfType<StudyModeManager>();
    private static Transform LiveTooth(StudyModeManager manager) => manager.currentToothRoot.Cast<Transform>()
        .Single(t => t.gameObject.activeSelf);

    private static void CheckSelectionView(StudyModeManager manager)
    {
        Check(!manager.IsViewingTooth, "Selection view has no current tooth.");
        Check(manager.toothSelectionRoot.activeInHierarchy, "Full arch is visible for selection.");
        Check(!manager.currentToothRoot.gameObject.activeInHierarchy, "Single tooth container is hidden.");
        Check(!manager.backToTeethButton.activeInHierarchy, "Back button is hidden in selection view.");
        Check(manager.currentToothRoot.childCount == 0, "No old tooth remains in the spawn container.");
        Check(manager.toothSelectionRoot.GetComponentsInChildren<StudyToothSelectable>()
            .Where(s => s.outline != null).All(s => !s.outline.enabled), "No stale tooth outline remains after returning.");
    }

    private static void SelectUsingXR(StudyModeManager manager, string id, XRRayInteractor ray)
    {
        var source = manager.toothSelectionRoot.GetComponentsInChildren<StudyToothSelectable>()
            .Single(s => s.toothId == id);
        var interactable = source.GetComponent<XRBaseInteractable>();
        Check(interactable != null && interactable.isActiveAndEnabled, id + " has an active XR selection target.");
        interactable.interactionManager.HoverEnter((IXRHoverInteractor)ray, (IXRHoverInteractable)interactable);
        interactable.interactionManager.SelectEnter((IXRSelectInteractor)ray, (IXRSelectInteractable)interactable);
    }

    private static void ClickBack(StudyModeManager manager)
    {
        Button button = manager.backToTeethButton.GetComponent<Button>();
        Check(button.isActiveAndEnabled && button.interactable, "Back button accepts clicks in single tooth view.");
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current)
            { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    }

    private static IEnumerator Run()
    {
        yield return null;
        Check(SceneManager.GetActiveScene().name == "MainMenu", "Verification starts from MainMenu.");
        Button studyButton = UnityEngine.Object.FindObjectsOfType<Button>().Single(b => b.name == "Study Mode");
        studyButton.onClick.Invoke();
        while (SceneManager.GetActiveScene().name != "StudyMode_1") yield return null;
        for (int i = 0; i < 3; i++) yield return null;

        StudyModeManager manager = Manager();
        Check(manager != null && manager.toothSelectionRoot != null && manager.backToTeethButton != null,
            "Shared viewer references survive loading from the menu.");
        CheckSelectionView(manager);
        Transform viewer = manager.currentToothRoot.parent;
        Check(manager.toothSelectionRoot.transform.parent == viewer, "Arch and single tooth share one display panel.");
        RectTransform panel = (RectTransform)viewer.Find("Canvas/Panel");
        Check(Mathf.Abs(panel.rect.width * panel.lossyScale.x - 3.8f) < .01f, "Merged panel spans the former two panel widths.");
        Check(!panel.GetComponent<Image>().raycastTarget, "Display background cannot block tooth or button rays.");
        Check(manager.backToTeethButton.GetComponentInParent<TrackedDeviceGraphicRaycaster>() != null,
            "Back button has VR UI raycasting support.");
        Button back = manager.backToTeethButton.GetComponent<Button>();
        Check(back.onClick.GetPersistentEventCount() == 1 && back.onClick.GetPersistentTarget(0) == manager &&
            back.onClick.GetPersistentMethodName(0) == "ReturnToToothSelection", "Back button is bound to the scene manager.");
        AnatomyHotspotManager anatomy = manager.GetComponent<AnatomyHotspotManager>();
        string initialText = anatomy.infoPanel.descriptionText.text;
        Check(initialText.Contains("Back to teeth") && initialText.Contains("Grip"), "Right panel explains selection and returning.");
        Check(!anatomy.showPits && !anatomy.showCusps && !anatomy.showRidges && !anatomy.showFossae && !anatomy.showGrooves,
            "All anatomy filters still start unchecked.");
        Capture(viewer.parent, "01-choose-tooth");

        manager.SelectTooth("__unconfigured_tooth__");
        Check(!manager.IsViewingTooth && manager.toothSelectionRoot.activeInHierarchy,
            "An unconfigured tooth cannot hide the arch or open an empty viewer.");
        XRRayInteractor ray = UnityEngine.Object.FindObjectsOfType<XRRayInteractor>().First(r => r.isActiveAndEnabled);
        SelectUsingXR(manager, "UR6", ray);
        for (int i = 0; i < 3; i++) yield return null;
        Check(manager.IsViewingTooth && LiveTooth(manager).name == "UR6", "XR selection displays UR6.");
        CheckToothFits(manager);
        Check(!manager.toothSelectionRoot.activeInHierarchy && manager.backToTeethButton.activeInHierarchy,
            "Selecting UR6 replaces the arch and exposes Back.");
        Check(!ray.interactablesSelected.Any(s => ((Component)s).transform.IsChildOf(manager.toothSelectionRoot.transform)),
            "Hiding the arch cancels its XR selection safely.");
        var regions = LiveTooth(manager).GetComponentsInChildren<AnatomyRegion>(true);
        Check(regions.Length == 27 && regions.All(r => !r.FilterEnabled), "UR6 retains its 27 regions with filters initially off.");
        Toggle ridgeToggle = UnityEngine.Object.FindObjectsOfType<Toggle>().Single(t => t.name == "ridge_toggle");
        ridgeToggle.isOn = true;
        Check(anatomy.showRidges && regions.Where(r => r.structureType == AnatomyStructureType.Ridge).All(r => r.FilterEnabled),
            "Existing Ridge filter still operates from the right panel.");
        StudyModeGrooveRevision.VerifyRuntime(manager, ray);
        Capture(viewer.parent, "02-view-tooth");
        var ridge = regions.First(r => r.structureType == AnatomyStructureType.Ridge);
        var ridgeTarget = ridge.GetComponent<XRSimpleInteractable>();
        ridgeTarget.hoverEntered.Invoke(new HoverEnterEventArgs { interactorObject = ray, interactableObject = ridgeTarget });
        Check(anatomy.infoPanel.titleText.text == ridge.structureName, "Anatomy hover still displays the selected structure.");
        ClickBack(manager);
        for (int i = 0; i < 3; i++) yield return null;
        CheckSelectionView(manager);
        Check(anatomy.infoPanel.descriptionText.text == initialText && anatomy.infoPanel.titleText.text == anatomy.infoPanel.defaultTitle,
            "Returning clears the previous structure's information.");
        Check(anatomy.showRidges && ridgeToggle.isOn, "Returning retains the player's filter choices.");
        Capture(viewer.parent, "03-return-to-arch");

        SelectUsingXR(manager, "UR7", ray);
        for (int i = 0; i < 3; i++) yield return null;
        Check(manager.IsViewingTooth && LiveTooth(manager).name == "UR7", "A different tooth can be selected after returning.");
        CheckToothFits(manager);
        // Returning must also remove a tooth currently grabbed outside its original pose.
        XRGrabInteractable grab = LiveTooth(manager).GetComponent<XRGrabInteractable>();
        Check(grab != null, "Single tooth retains its original grab interaction.");
        grab.interactionManager.SelectEnter((IXRSelectInteractor)ray, (IXRSelectInteractable)grab);
        yield return null;
        ClickBack(manager);
        for (int i = 0; i < 3; i++) yield return null;
        CheckSelectionView(manager);
        Check(grab == null, "Returning destroys a held tooth and releases its interaction.");

        SelectUsingXR(manager, "UR6", ray);
        for (int i = 0; i < 3; i++) yield return null;
        Check(LiveTooth(manager).GetComponentsInChildren<AnatomyRegion>(true)
            .Where(r => r.structureType == AnatomyStructureType.Ridge).All(r => r.FilterEnabled),
            "Selecting UR6 again preserves the active Ridge filter.");
        Check(manager.currentToothRoot.childCount == 1, "Repeated selection never accumulates old tooth instances.");
        ClickBack(manager);
        for (int i = 0; i < 3; i++) yield return null;
        manager.ReturnToToothSelection();
        CheckSelectionView(manager);

        SceneManager.LoadScene("StudyMode_1");
        for (int i = 0; i < 5; i++) yield return null;
        manager = Manager();
        CheckSelectionView(manager);
        Check(!manager.GetComponent<AnatomyHotspotManager>().showRidges, "A fresh scene entry starts with the original filter defaults.");
        Check(errors.Count == 0, "No Console errors or exceptions during scene load, selection, return or reload.");
    }

    private static void CheckToothFits(StudyModeManager manager)
    {
        // Measure rendered bounds independently of the fitting implementation.
        Bounds allowed = new Bounds(manager.toothViewCenter, manager.toothViewSize + Vector3.one * .01f);
        foreach (Renderer renderer in LiveTooth(manager).GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is MeshRenderer)) continue;
            Bounds bounds = renderer.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                if (!allowed.Contains(manager.currentToothRoot.InverseTransformPoint(point)))
                    throw new InvalidOperationException("Tooth extends outside the viewing area: " + renderer.name);
            }
        }
        Check(true, LiveTooth(manager).name + " fits below the header and in front of the panel.");
    }

    private static void Capture(Transform menu, string name)
    {
        string output = StudyModeSharedViewerSetup.Output;
        Directory.CreateDirectory(output);
        var go = new GameObject("Temporary shared viewer preview camera");
        var camera = go.AddComponent<Camera>();
        var target = new RenderTexture(1400, 800, 24);
        RenderTexture previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            camera.transform.SetPositionAndRotation(menu.TransformPoint(new Vector3(0, 0, -5)), menu.rotation);
            camera.orthographic = true;
            camera.orthographicSize = 1.85f;
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 30f;
            camera.backgroundColor = new Color(.3f, .35f, .4f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(output + "/" + name + ".png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            if (image != null) UnityEngine.Object.Destroy(image);
            UnityEngine.Object.Destroy(target);
            UnityEngine.Object.Destroy(go);
        }
    }

    private static void Finish(string failure)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= CaptureLog;
        routine = null;
        if (failure == null && errors.Count != 0) failure = "Console errors were logged during verification.";
        Directory.CreateDirectory(StudyModeSharedViewerSetup.Output);
        File.WriteAllText(StudyModeSharedViewerSetup.Output + "/verification.json", JsonUtility.ToJson(new Result
        {
            passed = failure == null, failure = failure, checks = passedChecks.ToArray(),
            consoleErrors = errors.ToArray(), consoleWarnings = warnings.ToArray()
        }, true));
        if (failure == null) Debug.Log("Shared tooth viewer verification passed: " + passedChecks.Count + " checks; no Console errors.");
        else Debug.LogError("Shared tooth viewer verification failed: " + failure);
        EditorApplication.isPlaying = false;
    }
}
