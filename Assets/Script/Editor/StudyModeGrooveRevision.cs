using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

// Explicit import/inspection for the three course-reference groove patches.
[InitializeOnLoad]
public static class StudyModeGrooveRevision
{
    private const string NewModel = "Assets/Models/StudyMode/Corrected Grooves.fbx";
    private const string OldModel = "Assets/exp cutouts.fbx";
    private const string PrefabPath = "Assets/Prefabs/StudyMode/Maxillary_right_1st_molar.prefab";
    private const string Output = "StudyModeBackup/20261027-groove-recut";
    private static readonly string[] Names = { "Buccal Groove", "Central Groove", "Transverse Groove (of oblique ridge)" };

    [Serializable] private class Geometry
    {
        public string name, source;
        public Vector3 center, size;
        public Vector3[] vertices;
        public int[] triangles;
    }
    [Serializable] private class ProbeReport { public List<Geometry> meshes = new List<Geometry>(); }

    static StudyModeGrooveRevision() { EditorApplication.delayCall += HandleRequest; }
    private static void HandleRequest()
    {
        const string request = "Temp/StudyModeGrooveProbe.request";
        const string apply = "Temp/StudyModeGrooveApply.request";
        if (!File.Exists(request) && !File.Exists(apply)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += HandleRequest; return; }
        try
        {
            if (File.Exists(request)) { File.Delete(request); Probe(); }
            if (File.Exists(apply)) { File.Delete(apply); Apply(); }
        }
        catch (Exception error) { File.WriteAllText(Output + "/unity-error.txt", error.ToString()); Debug.LogException(error); }
    }

    [MenuItem("Tools/Dental/Inspect Corrected Grooves")]
    public static void Probe()
    {
        var report = new ProbeReport();
        foreach (string path in new[] { OldModel, NewModel })
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            foreach (Mesh mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
            {
                if (!Names.Contains(mesh.name) && mesh.name != "Maxillary_right_1st_molar") continue;
                report.meshes.Add(new Geometry { name = mesh.name, source = path, center = mesh.bounds.center,
                    size = mesh.bounds.size, vertices = mesh.vertices, triangles = mesh.triangles });
            }
        }
        File.WriteAllText(Output + "/unity-mesh-probe.json", JsonUtility.ToJson(report));
        Debug.Log("Corrected groove probe complete: " + report.meshes.Count + " meshes.");
    }

    [MenuItem("Tools/Dental/Apply Corrected Grooves")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before replacing the groove meshes.");
        const string backup = Output + "/Maxillary_right_1st_molar.before.prefab";
        using (var hash = SHA256.Create())
            if (!hash.ComputeHash(File.ReadAllBytes(backup)).SequenceEqual(hash.ComputeHash(File.ReadAllBytes(PrefabPath))))
                throw new InvalidOperationException("The prefab changed since inspection; preserve those edits and inspect again first.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        bool reopen = stage != null && stage.assetPath == PrefabPath;
        if (reopen)
        {
            if (stage.scene.isDirty) throw new InvalidOperationException("Save the current prefab edits before applying corrected grooves.");
            StageUtility.GoToMainStage();
        }
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var meshes = AssetDatabase.LoadAllAssetsAtPath(NewModel).OfType<Mesh>().ToDictionary(m => m.name);
            if (meshes.Count != 3 || Names.Any(n => !meshes.ContainsKey(n)))
                throw new InvalidOperationException("Corrected FBX must contain exactly the three requested groove meshes.");
            Transform body = root.transform.Find("Mesh");
            if (AssetDatabase.GetAssetPath(body.GetComponent<MeshFilter>().sharedMesh) != OldModel)
                throw new InvalidOperationException("The tooth body no longer uses the inspected source geometry.");
            var allowed = new HashSet<Component>();
            var targets = Names.Select(n => root.transform.Find("AnatomyRegions/Groove/" + n)).ToArray();
            foreach (Transform region in targets)
            {
                if (region == null) throw new InvalidOperationException("A target groove is missing.");
                var filter = region.GetComponentsInChildren<MeshFilter>(true).Single();
                allowed.Add(region); allowed.Add(filter.transform); allowed.Add(filter);
                foreach (MeshCollider collider in region.GetComponentsInChildren<MeshCollider>(true))
                {
                    if (collider.transform != region && collider.transform != filter.transform)
                        throw new InvalidOperationException("Unexpected collider transform: " + region.name);
                    allowed.Add(collider);
                }
                if (region.name == "Buccal Groove") allowed.Add(region.GetComponent<AnatomyHotspot>());
            }
            var preserved = root.GetComponentsInChildren<Component>(true).Where(c => c != null && !allowed.Contains(c))
                .ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
            foreach (Transform region in targets)
            {
                MeshFilter filter = region.GetComponentInChildren<MeshFilter>(true);
                // Export validation confirmed identical local mesh coordinates/units.
                // The new surface offset is already baked, so no old manual translation is needed.
                Matrix4x4 local = region.parent.worldToLocalMatrix * body.localToWorldMatrix;
                region.localPosition = local.GetColumn(3);
                region.localRotation = local.rotation;
                region.localScale = local.lossyScale;
                if (filter.transform != region)
                {
                    filter.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    filter.transform.localScale = Vector3.one;
                }
                filter.sharedMesh = meshes[region.name];
                foreach (MeshCollider collider in region.GetComponentsInChildren<MeshCollider>(true))
                    collider.sharedMesh = filter.sharedMesh;
                if (region.name == "Buccal Groove")
                    region.GetComponent<AnatomyHotspot>().description = "Also known as the buccal developmental groove. Extends buccally from the central pit between the mesiobuccal and distobuccal cusps, and may terminate in a buccal pit on the buccal surface.";
            }
            foreach (var item in preserved)
                if (EditorJsonUtility.ToJson(item.Key) != item.Value)
                    throw new InvalidOperationException("An unrelated component changed: " + item.Key.name + " / " + item.Key.GetType().Name);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            File.WriteAllText(Output + "/applied.txt", "Replaced exactly 3 groove meshes and their MeshColliders.\nPreserved " + preserved.Count + " unrelated components.\nCorrected the buccal/lingual description typo.\n");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            if (reopen) PrefabStageUtility.OpenPrefab(PrefabPath);
        }
        ExportPreview();
        Debug.Log("Three corrected groove meshes applied; unrelated prefab components preserved.");
    }

    [MenuItem("Tools/Dental/Preview Corrected Grooves")]
    public static void ExportPreview()
    {
        var preview = new PreviewRenderUtility();
        var temporaryMaterials = new List<Material>();
        try
        {
            GameObject tooth = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            preview.AddSingleGO(tooth);
            foreach (Transform child in tooth.GetComponentsInChildren<Transform>(true)) child.gameObject.SetActive(true);
            foreach (AnatomyRegion region in tooth.GetComponentsInChildren<AnatomyRegion>(true))
                region.SetInteractableByFilter(Names.Contains(region.name));
            Color[] colors = { new Color(.05f,.85f,.2f), new Color(.05f,.5f,1f), new Color(1f,.55f,.03f) };
            for (int i = 0; i < Names.Length; i++)
            {
                Renderer renderer = tooth.transform.Find("AnatomyRegions/Groove/" + Names[i]).GetComponentInChildren<Renderer>(true);
                var material = new Material(renderer.sharedMaterial);
                material.SetColor("_BaseColor", colors[i]);
                temporaryMaterials.Add(material);
                renderer.sharedMaterial = material;
                renderer.SetPropertyBlock(null);
            }
            Transform body = tooth.transform.Find("Mesh");
            // Original Blender mesh rotation; inverse maps the aligned reference view to raw mesh coordinates.
            Matrix4x4 rotation = Matrix4x4.identity;
            rotation.SetColumn(0, new Vector4(.23222735f,-.08335219f,-.96908357f,0));
            rotation.SetColumn(1, new Vector4(.97070693f,.08299281f,.22547804f,0));
            rotation.SetColumn(2, new Vector4(.06163288f,-.99305820f,.10018374f,0));
            Matrix4x4 toWorld = body.localToWorldMatrix * Matrix4x4.Scale(new Vector3(-10,10,10)) * rotation.inverse;
            Camera camera = preview.camera;
            Quaternion roll = Quaternion.Euler(0,0,-35);
            camera.transform.SetPositionAndRotation(toWorld.MultiplyPoint3x4(new Vector3(-.15f,.2f,5)),
                Quaternion.LookRotation(toWorld.MultiplyVector(Vector3.back), toWorld.MultiplyVector(roll * Vector3.up)));
            camera.orthographic = true;
            camera.orthographicSize = 1.25f * body.lossyScale.x * 10f;
            camera.nearClipPlane = .01f; camera.farClipPlane = 40f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f,.06f,.07f);
            preview.lights[0].intensity = 1.1f;
            preview.lights[0].transform.rotation = camera.transform.rotation * Quaternion.Euler(35,-30,0);
            preview.lights[1].intensity = .5f;
            preview.ambientColor = new Color(.35f,.35f,.35f);
            preview.BeginStaticPreview(new Rect(0,0,1000,1000));
            preview.Render(true);
            Texture2D image = preview.EndStaticPreview();
            File.WriteAllBytes(Output + "/unity-corrected-grooves.png", image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
        finally
        {
            preview.Cleanup();
            foreach (Material material in temporaryMaterials) UnityEngine.Object.DestroyImmediate(material);
        }
    }

    public static void VerifyRuntime(StudyModeManager study, XRRayInteractor ray)
    {
        var checks = new List<string>();
        Action<bool, string> check = (condition, message) =>
        {
            if (!condition) throw new InvalidOperationException(message);
            checks.Add(message);
        };
        ProbeReport probe = JsonUtility.FromJson<ProbeReport>(File.ReadAllText(Output + "/unity-mesh-probe.json"));
        AnatomyHotspotManager anatomy = study.GetComponent<AnatomyHotspotManager>();
        Toggle toggle = UnityEngine.Object.FindObjectsOfType<Toggle>().Single(t => t.name == "groove_toggle");
        bool original = toggle.isOn;
        toggle.isOn = true;
        Physics.SyncTransforms();
        var tooth = study.currentToothRoot.Cast<Transform>().Single(t => t.gameObject.activeSelf);
        var regions = Names.Select(n => tooth.Find("AnatomyRegions/Groove/" + n).GetComponent<AnatomyHotspot>()).ToArray();
        foreach (AnatomyHotspot region in regions)
        {
            MeshFilter filter = region.GetComponentInChildren<MeshFilter>(true);
            MeshCollider collider = region.GetComponentsInChildren<MeshCollider>(true).Single();
            check(AssetDatabase.GetAssetPath(filter.sharedMesh) == NewModel, region.name + ": corrected FBX is used at runtime.");
            check(collider.enabled && collider.sharedMesh == filter.sharedMesh, region.name + ": MeshCollider uses the visible mesh.");
            check(region.FilterEnabled && filter.GetComponent<Renderer>().enabled, region.name + ": Groove filter displays the patch.");
            Geometry geometry = probe.meshes.Single(m => m.source == NewModel && m.name == region.name);
            var triangles = Enumerable.Range(0, geometry.triangles.Length / 3).OrderByDescending(i =>
            {
                Vector3 a = geometry.vertices[geometry.triangles[i * 3]];
                Vector3 b = geometry.vertices[geometry.triangles[i * 3 + 1]];
                Vector3 c = geometry.vertices[geometry.triangles[i * 3 + 2]];
                return Vector3.Cross(b - a, c - a).sqrMagnitude;
            }).Take(12);
            foreach (int i in triangles)
            {
                Vector3 a = filter.transform.TransformPoint(geometry.vertices[geometry.triangles[i * 3]]);
                Vector3 b = filter.transform.TransformPoint(geometry.vertices[geometry.triangles[i * 3 + 1]]);
                Vector3 c = filter.transform.TransformPoint(geometry.vertices[geometry.triangles[i * 3 + 2]]);
                Vector3 center = (a + b + c) / 3f;
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                bool hit = collider.Raycast(new Ray(center + normal * .03f, -normal), out RaycastHit contact, .06f);
                check(hit && Vector3.Distance(contact.point, center) < .001f, region.name + ": ray intersects rendered surface at sample " + i + ".");
            }
            XRBaseInteractable target = region.GetComponent<XRBaseInteractable>();
            check(target.colliders.Contains(collider), region.name + ": XR interactable keeps its collider reference.");
            target.hoverEntered.Invoke(new HoverEnterEventArgs { interactorObject = ray, interactableObject = target });
            check(anatomy.infoPanel.titleText.text == region.structureName, region.name + ": hovering shows the correct name.");
            var properties = new MaterialPropertyBlock();
            filter.GetComponent<Renderer>().GetPropertyBlock(properties);
            check(Mathf.Approximately(properties.GetColor("_BaseColor").a, 1f), region.name + ": focused patch stays opaque.");
            AnatomyHotspot other = regions.First(r => r != region);
            other.GetComponentInChildren<Renderer>(true).GetPropertyBlock(properties);
            check(Mathf.Approximately(properties.GetColor("_BaseColor").a, anatomy.unselectedOpacity), region.name + ": other visible patches retain context transparency.");
            target.hoverExited.Invoke(new HoverExitEventArgs { interactorObject = ray, interactableObject = target });
        }
        check(!regions[0].description.Contains("lingual side"), "Buccal groove information no longer points to the lingual side.");
        toggle.isOn = false;
        check(regions.All(r => !r.FilterEnabled && !r.GetComponentInChildren<Renderer>(true).enabled &&
            !r.GetComponentInChildren<MeshCollider>(true).enabled), "Turning Grooves off disables both visuals and collision targets.");
        toggle.isOn = original;
        File.WriteAllText(Output + "/runtime-checks.txt", string.Join("\n", checks));
        Debug.Log("Corrected grooves runtime verification passed: " + checks.Count + " checks.");
    }
}
