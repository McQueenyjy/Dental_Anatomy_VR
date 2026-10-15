using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

// Explicit, one-shot Play Mode checks using disposable instances; never saves project assets.
[InitializeOnLoad]
public static class StudyModeTransparencyVerification
{
    private const string Request = "Temp/StudyModeTransparencyVerification.request";
    private const string Output = "StudyModePreview/20261015-transparency";
    private const string PrefabPath = "Assets/Prefabs/StudyMode/Maxillary_right_1st_molar.prefab";
    private static int checks;
    [Serializable] private class Result { public bool passed; public int checks; public string error; }

    static StudyModeTransparencyVerification()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Request))
                EditorApplication.delayCall += Run;
        };
    }

    [MenuItem("Tools/Dental/Verify Hover Transparency")]
    public static void RequestVerification()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.WriteAllText(Request, "Run once");
        EditorApplication.isPlaying = true;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }

    private static Renderer Visual(AnatomyHotspot r) => r.highlightObject.GetComponent<Renderer>();
    private static float Alpha(AnatomyHotspot r)
    {
        var properties = new MaterialPropertyBlock();
        Visual(r).GetPropertyBlock(properties);
        return properties.GetColor("_BaseColor").a;
    }

    private static void Hover(AnatomyHotspot region, XRRayInteractor ray, bool enter)
    {
        var target = region.GetComponent<XRSimpleInteractable>();
        if (enter) target.hoverEntered.Invoke(new HoverEnterEventArgs { interactorObject = ray, interactableObject = target });
        else target.hoverExited.Invoke(new HoverExitEventArgs { interactorObject = ray, interactableObject = target });
    }

    private static void CheckFocus(AnatomyHotspot[] regions, AnatomyHotspot focused, float opacity)
    {
        Check(regions.Where(r => r.isActiveAndEnabled && r.FilterEnabled).All(r =>
            Visual(r).enabled && Mathf.Approximately(Alpha(r), r == focused ? 1f : opacity)),
            "Visible regions must stay visible with correct opacity: " + focused.name);
        Check(regions.Where(r => r != focused && r.FilterEnabled && r.isActiveAndEnabled).All(r =>
            Visual(r).sharedMaterial.GetFloat("_ZWrite") == 0 &&
            Visual(r).sharedMaterial.GetFloat("_SrcBlend") == (float)BlendMode.SrcAlpha &&
            Visual(r).sharedMaterial.renderQueue < Visual(focused).sharedMaterial.renderQueue),
            "Transparent depth/blending/order is incorrect.");
    }

    private static void Run()
    {
        File.Delete(Request);
        checks = 0;
        GameObject root = null;
        var result = new Result();
        try
        {
            Directory.CreateDirectory(Output);
            Scene scene = SceneManager.CreateScene("Hover Transparency Verification");
            root = new GameObject("Hover Transparency Verification");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            var manager = root.AddComponent<AnatomyHotspotManager>();
            var study = root.AddComponent<StudyModeManager>();
            var spawn = new GameObject("Spawn").transform;
            spawn.SetParent(root.transform, false);
            var info = new GameObject("Info");
            info.transform.SetParent(root.transform, false);
            var panel = info.AddComponent<AnatomyInfoPanel>();
            var title = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            title.transform.SetParent(info.transform, false);
            panel.titleText = title.GetComponent<TextMeshProUGUI>();
            manager.infoPanel = panel;
            study.currentToothRoot = spawn;
            study.spawnFirstOnStart = false;
            study.teeth = new[] { new StudyModeManager.ToothEntry { toothId = "UR6",
                viewerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), localScale = Vector3.one } };
            var rays = new XRRayInteractor[2];
            for (int i = 0; i < rays.Length; i++)
            {
                var ray = new GameObject("Test Ray " + i);
                ray.transform.SetParent(root.transform, false);
                ray.AddComponent<ActionBasedController>();
                rays[i] = ray.AddComponent<XRRayInteractor>();
                rays[i].enabled = false;
            }
            root.SetActive(true);
            study.SelectTooth("UR6");
            GameObject tooth = spawn.GetChild(0).gameObject;
            var regions = tooth.GetComponentsInChildren<AnatomyHotspot>();
            Check(regions.Length == 27, "Expected all 27 anatomy regions.");
            var originals = regions.ToDictionary(r => r, r => Visual(r).sharedMaterial);
            var hits = regions.SelectMany(r => r.GetComponent<XRSimpleInteractable>().colliders)
                .Distinct().ToDictionary(c => c, c => c.enabled);
            var body = tooth.transform.Find("Mesh").GetComponent<Renderer>();
            var bodyMaterial = body.sharedMaterial;
            Snapshot(tooth, "normal");

            foreach (var region in regions)
            {
                Hover(region, rays[0], true);
                CheckFocus(regions, region, manager.unselectedOpacity);
                Check(panel.titleText.text == region.structureName, "Panel and focus disagree.");
                Check(hits.All(pair => pair.Key.enabled == pair.Value), "Opacity changed a collider.");
                Check(body.sharedMaterial == bodyMaterial && body.enabled, "The base tooth must retain its material.");
                if (region.structureName == "Oblique Ridge" || region.structureName == "Transverse Ridge" ||
                    region.structureName == "Mesiobuccal Triangular Ridge") Snapshot(tooth, region.structureName);
                Hover(region, rays[0], false);
                Check(regions.All(r => Visual(r).sharedMaterial == originals[r] &&
                    Visual(r).enabled && Mathf.Approximately(Alpha(r), 1f)), "Original materials/opacity not restored.");
            }

            var oblique = regions.Single(r => r.structureName == "Oblique Ridge");
            var transverse = regions.Single(r => r.structureName == "Transverse Ridge");
            Hover(oblique, rays[0], true);
            Material focusedMaterial = Visual(oblique).sharedMaterial;
            Material transparentMaterial = Visual(transverse).sharedMaterial;
            VerifyPixels(focusedMaterial, transparentMaterial, originals[oblique]);
            Hover(transverse, rays[1], true);
            CheckFocus(regions, transverse, manager.unselectedOpacity);
            Hover(transverse, rays[1], false);
            CheckFocus(regions, oblique, manager.unselectedOpacity);
            Hover(oblique, rays[1], true);
            Hover(oblique, rays[0], false);
            CheckFocus(regions, oblique, manager.unselectedOpacity);
            Hover(oblique, rays[1], false);
            Hover(oblique, rays[0], true);
            Check(Visual(oblique).sharedMaterial == focusedMaterial && Visual(transverse).sharedMaterial == transparentMaterial,
                "Hover should reuse runtime materials.");

            manager.SetCuspFilter(false);
            Check(regions.Where(r => r.structureType == AnatomyStructureType.Cusp).All(r => !Visual(r).enabled), "Filter-off exposed a cusp.");
            manager.SetCuspFilter(true);
            CheckFocus(regions, oblique, manager.unselectedOpacity);
            manager.SetRidgeFilter(false);
            Check(regions.Where(r => r.structureType == AnatomyStructureType.Ridge).All(r => !Visual(r).enabled && !r.IsHovered), "Filter-off retained selected ridges.");
            Check(regions.Where(r => r.FilterEnabled).All(r => Alpha(r) == 1f), "Filtering the focused category must restore others.");
            manager.SetRidgeFilter(true);

            Hover(oblique, rays[0], true);
            transverse.gameObject.SetActive(false);
            transverse.gameObject.SetActive(true);
            CheckFocus(regions, oblique, manager.unselectedOpacity);
            oblique.gameObject.SetActive(false);
            Check(regions.Where(r => r != oblique).All(r => Alpha(r) == 1f), "Disabling focus left transparent peers.");
            oblique.gameObject.SetActive(true);
            manager.infoPanel = null;
            Hover(transverse, rays[0], true);
            CheckFocus(regions, transverse, manager.unselectedOpacity);
            manager.unselectedOpacity = .4f;
            manager.ApplyStructureFilters();
            CheckFocus(regions, transverse, .4f);
            Hover(transverse, rays[0], false);
            manager.infoPanel = panel;

            Hover(oblique, rays[0], true);
            manager.enabled = false;
            Check(regions.All(r => Visual(r).sharedMaterial == originals[r] && Alpha(r) == 1f), "Manager disable did not restore materials.");
            manager.enabled = true;
            Hover(oblique, rays[0], false);
            Hover(oblique, rays[0], true);
            study.SelectTooth("UR6");
            var replacement = spawn.GetComponentsInChildren<AnatomyHotspot>();
            Check(replacement.Length == 27 && replacement.All(r => Visual(r).enabled && Alpha(r) == 1f), "New tooth retained stale transparency.");
            Check(panel.titleText.text == panel.defaultTitle, "New tooth retained stale information.");
            Check(originals.Values.Distinct().All(m => m.renderQueue == 2010 && m.GetFloat("_ZWrite") == 1f), "Shared material assets were mutated.");
            result.passed = true;
            Debug.Log("Hover transparency verification passed: " + checks + " checks.");
        }
        catch (Exception error) { result.error = error.ToString(); Debug.LogException(error); }
        finally
        {
            result.checks = checks;
            File.WriteAllText("Temp/StudyModeTransparencyVerification.result.json", JsonUtility.ToJson(result, true));
            if (root != null) UnityEngine.Object.Destroy(root);
            EditorApplication.isPlaying = false;
        }
    }

    private static Texture2D Render(PreviewRenderUtility preview, int size)
    {
        preview.BeginStaticPreview(new Rect(0, 0, size, size));
        preview.Render(true);
        return preview.EndStaticPreview();
    }

    private static void Snapshot(GameObject tooth, string name)
    {
        var preview = new PreviewRenderUtility();
        try
        {
            var copy = new GameObject("Render-only tooth");
            preview.AddSingleGO(copy);
            foreach (var source in tooth.GetComponentsInChildren<MeshRenderer>())
            {
                if (!source.enabled) continue;
                var target = new GameObject(source.name);
                target.transform.SetParent(copy.transform, false);
                target.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                target.transform.localScale = source.transform.lossyScale;
                target.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
                var visual = target.AddComponent<MeshRenderer>();
                visual.sharedMaterials = source.sharedMaterials;
                var properties = new MaterialPropertyBlock();
                source.GetPropertyBlock(properties);
                visual.SetPropertyBlock(properties);
            }
            Bounds bounds = tooth.transform.Find("Mesh").GetComponent<Renderer>().bounds;
            preview.camera.orthographic = true;
            preview.camera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.z) * .6f;
            preview.camera.transform.position = bounds.center + Vector3.down * 8f;
            preview.camera.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            preview.camera.nearClipPlane = .01f;
            preview.camera.farClipPlane = 30f;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(.045f, .04f, .06f);
            preview.lights[0].intensity = 1.2f;
            preview.lights[0].transform.rotation = Quaternion.Euler(230, -30, 0);
            preview.lights[1].intensity = .6f;
            preview.ambientColor = new Color(.4f, .4f, .4f);
            var texture = Render(preview, 700);
            File.WriteAllBytes(Output + "/" + name + ".png", texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
        finally { preview.Cleanup(); }
    }

    private static void VerifyPixels(Material focused, Material transparent, Material original)
    {
        var preview = new PreviewRenderUtility();
        try
        {
            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var front = GameObject.CreatePrimitive(PrimitiveType.Quad);
            preview.AddSingleGO(back);
            preview.AddSingleGO(front);
            front.transform.position = new Vector3(0, 0, -.1f);
            var a = back.GetComponent<Renderer>();
            var b = front.GetComponent<Renderer>();
            a.sharedMaterial = focused;
            b.sharedMaterial = transparent;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", Color.cyan);
            a.SetPropertyBlock(properties);
            properties.SetColor("_BaseColor", new Color(1, 0, 0, .2f));
            b.SetPropertyBlock(properties);
            preview.camera.transform.position = new Vector3(0, 0, -2);
            preview.camera.transform.rotation = Quaternion.identity;
            preview.camera.orthographic = true;
            preview.camera.orthographicSize = 1;
            preview.camera.nearClipPlane = .01f;
            preview.camera.farClipPlane = 10f;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = Color.black;
            Func<Color> sample = () =>
            {
                var texture = Render(preview, 128);
                Color color = texture.GetPixel(64, 64);
                UnityEngine.Object.DestroyImmediate(texture);
                return color;
            };
            b.enabled = false;
            Color selectedAlone = sample();
            b.enabled = true;
            Color overlap = sample();
            Check(selectedAlone.g > .1f && selectedAlone.b > .1f, "Shader did not render the focused surface.");
            Check(Vector4.Distance(selectedAlone, overlap) < .02f, "A transparent patch obscured/tinted the selected patch.");
            a.enabled = false;
            Color dim = sample();
            b.sharedMaterial = original;
            Color opaque = sample();
            Check(dim.r > .03f && dim.r < opaque.r * .8f,
                "Rendered pixels did not blend with the background. Dim=" + dim + ", opaque=" + opaque);
        }
        finally { preview.Cleanup(); }
    }
}
