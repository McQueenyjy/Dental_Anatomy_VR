using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;

[InitializeOnLoad]
public static class StudyModeVerification
{
    private const string Request = "Temp/StudyModeVerification.request";
    private static int checks;

    static StudyModeVerification()
    {
        EditorApplication.playModeStateChanged += OnPlayState;
    }

    private static void OnPlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Request))
            EditorApplication.delayCall += Run;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }

    private static void Run()
    {
        File.Delete(Request);
        GameObject testRoot = null;
        try
        {
            Scene scene = SceneManager.CreateScene("StudyMode Verification");
            testRoot = new GameObject("StudyMode Verification");
            testRoot.SetActive(false);
            SceneManager.MoveGameObjectToScene(testRoot, scene);
            AnatomyHotspotManager manager = testRoot.AddComponent<AnatomyHotspotManager>();
            StudyModeManager study = testRoot.AddComponent<StudyModeManager>();
            Transform spawn = new GameObject("Spawn").transform;
            spawn.SetParent(testRoot.transform, false);
            var infoObject = new GameObject("Info");
            infoObject.transform.SetParent(testRoot.transform, false);
            AnatomyInfoPanel panel = infoObject.AddComponent<AnatomyInfoPanel>();
            var titleObject = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObject.transform.SetParent(infoObject.transform, false);
            var bodyObject = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
            bodyObject.transform.SetParent(infoObject.transform, false);
            panel.titleText = titleObject.GetComponent<TextMeshProUGUI>();
            panel.descriptionText = bodyObject.GetComponent<TextMeshProUGUI>();
            manager.infoPanel = panel;
            study.currentToothRoot = spawn;
            study.spawnFirstOnStart = false;
            study.teeth = new[] { new StudyModeManager.ToothEntry {
                toothId = "UR6", viewerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/StudyMode/Maxillary_right_1st_molar.prefab"), localScale = Vector3.one } };
            XRRayInteractor[] rays = new XRRayInteractor[2];
            for (int i = 0; i < rays.Length; i++)
            {
                var rayObject = new GameObject("Test Ray " + i);
                rayObject.transform.SetParent(testRoot.transform, false);
                rays[i] = rayObject.AddComponent<XRRayInteractor>();
                rays[i].enabled = false;
            }
            testRoot.SetActive(true);
            study.SelectTooth("UR6");
            AnatomyHotspot[] regions = spawn.GetComponentsInChildren<AnatomyHotspot>();
            Check(regions.Length == 26, "Expected 23 regions plus 3 retained Pits.");
            int[] counts = { 3, 5, 7, 4, 7 };
            foreach (AnatomyStructureType type in Enum.GetValues(typeof(AnatomyStructureType)))
            {
                Check(regions.Count(r => r.structureType == type) == counts[(int)type], "Wrong count for " + type);
                Check(regions.Where(r => r.structureType == type).All(r => r.highlightObject.GetComponent<Renderer>().enabled), "Initial highlight missing: " + type);
            }
            AnatomyHotspot cusp = regions.First(r => r.structureType == AnatomyStructureType.Cusp);
            XRSimpleInteractable interactable = cusp.GetComponent<XRSimpleInteractable>();
            foreach (XRRayInteractor ray in rays)
                interactable.hoverEntered.Invoke(new HoverEnterEventArgs { interactorObject = ray, interactableObject = interactable });
            Check(panel.titleText.text == cusp.structureName, "Hover should show information immediately.");
            interactable.hoverExited.Invoke(new HoverExitEventArgs { interactorObject = rays[0], interactableObject = interactable });
            Check(cusp.IsHovered && panel.titleText.text == cusp.structureName, "One ray leaving must not clear the other ray.");
            interactable.hoverExited.Invoke(new HoverExitEventArgs { interactorObject = rays[1], interactableObject = interactable });
            Check(!cusp.IsHovered && cusp.highlightObject.GetComponent<Renderer>().enabled, "Category color must persist after hover exit.");
            Check(panel.titleText.text == panel.defaultTitle, "Information should clear after the final ray leaves.");
            interactable.hoverEntered.Invoke(new HoverEnterEventArgs { interactorObject = rays[0], interactableObject = interactable });
            manager.SetCuspFilter(false);
            Check(!cusp.FilterEnabled && !cusp.GetComponent<Collider>().enabled && !cusp.highlightObject.GetComponent<Renderer>().enabled, "Filtering must hide color and disable hit area.");
            Check(!cusp.IsHovered && panel.titleText.text == panel.defaultTitle, "Filtering must clear stale hover information.");
            manager.SetCuspFilter(true);
            Check(cusp.GetComponent<Collider>().enabled && cusp.highlightObject.GetComponent<Renderer>().enabled, "Re-enabling the category should restore it.");
            manager.SetRidgeFilter(false);
            manager.SetFossaFilter(false);
            manager.SetGrooveFilter(false);
            study.SelectTooth("UR6");
            regions = spawn.GetComponentsInChildren<AnatomyHotspot>();
            Check(regions.Length == 26, "Old tooth should be inactive immediately when replaced.");
            Check(regions.Where(r => r.structureType == AnatomyStructureType.Ridge || r.structureType == AnatomyStructureType.Fossa || r.structureType == AnatomyStructureType.Groove)
                .All(r => !r.FilterEnabled && !r.highlightObject.GetComponent<Renderer>().enabled), "New tooth must inherit current category filters.");
            Check(panel.titleText.text == panel.defaultTitle, "Switching teeth should clear the previous information.");
            manager.SetPitFilter(false);
            Check(regions.Where(r => r.structureType == AnatomyStructureType.Pit).All(r => !r.GetComponent<Collider>().enabled), "Existing Pit filters must still work.");
            File.WriteAllText("Temp/StudyModeVerification.result.json", "{\"passed\":true,\"checks\":" + checks + "}");
            Debug.Log("StudyMode verification passed: " + checks + " checks.");
        }
        catch (Exception error)
        {
            File.WriteAllText("Temp/StudyModeVerification.result.json", "{\"passed\":false,\"checks\":" + checks + ",\"error\":\"" + error.Message.Replace("\"", "'") + "\"}");
            Debug.LogException(error);
        }
        finally
        {
            if (testRoot != null) UnityEngine.Object.Destroy(testRoot);
            EditorApplication.isPlaying = false;
        }
    }
}
