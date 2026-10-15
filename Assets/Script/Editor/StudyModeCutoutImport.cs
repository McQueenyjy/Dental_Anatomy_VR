using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-shot requests make inspection explicit and prevent automatic prefab changes.
[InitializeOnLoad]
public static class StudyModeCutoutImport
{
    private const string PrefabPath = "Assets/Prefabs/StudyMode/Maxillary_right_1st_molar.prefab";
    private const string SourcePath = "Assets/exp cutouts.fbx";
    [Serializable] private class Entry
    {
        public string name, path, meshName, guid, fileId, structureName;
        public Vector3 position, scale, boundsCenter, boundsSize;
        public Quaternion rotation;
        public float[] matrix;
        public Vector3[] vertices;
        public int[] triangles;
    }
    [Serializable] private class Report
    {
        public List<Entry> source = new List<Entry>();
        public List<Entry> target = new List<Entry>();
    }
    [Serializable] private class Match { public string sourceName, meshFileId, targetPath; public Vector3 surfaceTranslation; }
    [Serializable] private class Plan
    {
        public string sourceSha256, prefabSha256;
        public Vector3 position, scale;
        public Quaternion rotation;
        public Match[] matches;
    }
    [Serializable] private class Result { public bool passed; public int replaced, meshColliders; public string error; }

    static StudyModeCutoutImport() { EditorApplication.delayCall += HandleRequest; }

    private static void HandleRequest()
    {
        const string probeRequest = "Temp/StudyModeCutoutProbe.request";
        const string applyRequest = "Temp/StudyModeCutoutApply.request";
        if (!File.Exists(probeRequest) && !File.Exists(applyRequest)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += HandleRequest;
            return;
        }
        if (File.Exists(probeRequest)) { File.Delete(probeRequest); Probe(); }
        if (File.Exists(applyRequest))
        {
            File.Delete(applyRequest);
            try { ApplyPlan(); }
            catch (Exception error)
            {
                File.WriteAllText("Temp/StudyModeCutoutApply.result.json", JsonUtility.ToJson(new Result { error = error.ToString() }));
                Debug.LogException(error);
            }
        }
    }

    [MenuItem("Tools/Dental/Inspect Named Cutouts")]
    public static void Probe()
    {
        var report = new Report();
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            report.source.Add(Read(filter, model.transform));
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null) report.target.Add(Read(filter, root.transform));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        File.WriteAllText("Temp/StudyModeCutoutGeometry.json", JsonUtility.ToJson(report));
        Debug.Log("Named cutout probe: " + report.source.Count + " source meshes.");
    }

    private static Entry Read(MeshFilter filter, Transform root)
    {
        Mesh mesh = filter.sharedMesh;
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long id);
        Matrix4x4 matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
        var elements = new float[16];
        for (int i = 0; i < 16; i++) elements[i] = matrix[i];
        AnatomyHotspot hotspot = filter.GetComponentInParent<AnatomyHotspot>();
        return new Entry {
            name = filter.name, path = AnimationUtility.CalculateTransformPath(filter.transform, root),
            meshName = mesh.name, guid = guid, fileId = id.ToString(),
            position = filter.transform.localPosition, rotation = filter.transform.localRotation,
            scale = filter.transform.localScale, matrix = elements,
            structureName = hotspot == null ? "" : hotspot.structureName,
            boundsCenter = mesh.bounds.center, boundsSize = mesh.bounds.size,
            vertices = mesh.vertices, triangles = mesh.triangles
        };
    }

    private static string Hash(string path)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
    }

    [MenuItem("Tools/Dental/Apply Named Cutout Plan")]
    public static void ApplyPlan()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Exit Play Mode and Prefab Mode before replacing named cutouts.");
        Plan plan = JsonUtility.FromJson<Plan>(File.ReadAllText("Temp/StudyModeCutoutPlan.json"));
        if (Hash(SourcePath) != plan.sourceSha256 || Hash(PrefabPath) != plan.prefabSha256)
            throw new InvalidOperationException("Source or prefab changed since inspection. Reinspect before applying.");
        var source = AssetDatabase.LoadAllAssetsAtPath(SourcePath).OfType<Mesh>()
            .Where(mesh => mesh.name != "testtooth3").ToDictionary(mesh => mesh.name);
        if (source.Count != plan.matches.Length || plan.matches.Select(m => m.sourceName).Distinct().Count() != source.Count)
            throw new InvalidOperationException("Every source mesh except testtooth3 must have exactly one target.");
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        int colliderCount = 0;
        try
        {
            // Validate the entire operation before changing any target.
            foreach (Match match in plan.matches)
            {
                if (match.sourceName == "testtooth3" || !source.ContainsKey(match.sourceName))
                    throw new InvalidOperationException("Excluded or missing source mesh.");
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source[match.sourceName], out string guid, out long id);
                if (id.ToString() != match.meshFileId) throw new InvalidOperationException("Mesh identifier changed.");
                Transform region = root.transform.Find(match.targetPath);
                if (region == null || region.GetComponentsInChildren<MeshFilter>(true).Length != 1)
                    throw new InvalidOperationException("Missing or ambiguous target: " + match.targetPath);
                if (match.targetPath != "Mesh" && region.name != match.sourceName)
                    throw new InvalidOperationException("Target name mismatch.");
                Transform visual = region.GetComponentInChildren<MeshFilter>(true).transform;
                foreach (MeshCollider collider in region.GetComponentsInChildren<MeshCollider>(true))
                    if (collider.transform != region && collider.transform != visual)
                        throw new InvalidOperationException("Unexpected collider placement: " + match.targetPath);
            }
            string backup = "StudyModeBackup/20261008-exp-cutouts/Maxillary_right_1st_molar.prefab";
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(PrefabPath, backup);
            foreach (Match match in plan.matches)
            {
                Transform region = root.transform.Find(match.targetPath);
                MeshFilter filter = region.GetComponentInChildren<MeshFilter>(true);
                // All cutouts share the tooth mesh's authored local coordinate system.
                // Discard the FBX's exploded-display translations, preserving geometry.
                Matrix4x4 local = region.parent.worldToLocalMatrix * root.transform.localToWorldMatrix *
                    Matrix4x4.TRS(plan.position + match.surfaceTranslation, plan.rotation, plan.scale);
                region.localPosition = local.GetColumn(3);
                region.localRotation = local.rotation;
                region.localScale = local.lossyScale;
                if (filter.transform != region)
                {
                    filter.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    filter.transform.localScale = Vector3.one;
                }
                filter.sharedMesh = source[match.sourceName];
                foreach (MeshCollider collider in region.GetComponentsInChildren<MeshCollider>(true))
                {
                    collider.sharedMesh = filter.sharedMesh;
                    colliderCount++;
                }
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Probe();
        ExportPreviews();
        File.WriteAllText("Temp/StudyModeCutoutApply.result.json", JsonUtility.ToJson(new Result {
            passed = true, replaced = plan.matches.Length, meshColliders = colliderCount
        }));
        Debug.Log("Named cutouts replaced: " + plan.matches.Length + "; testtooth3 and unmatched regions retained.");
    }

    [MenuItem("Tools/Dental/Preview Named Cutouts")]
    public static void ExportPreviews()
    {
        Directory.CreateDirectory("StudyModePreview/20261008-exp-cutouts");
        foreach (string category in new[] { "Body", "Cusp", "Ridge", "Fossa", "Groove", "Pit" })
        {
            var preview = new PreviewRenderUtility();
            try
            {
                GameObject tooth = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                preview.AddSingleGO(tooth);
                // Only this temporary preview enables the categories; the prefab states are retained.
                foreach (Transform child in tooth.transform.GetComponentsInChildren<Transform>(true)) child.gameObject.SetActive(true);
                foreach (AnatomyRegion region in tooth.GetComponentsInChildren<AnatomyRegion>(true))
                    region.SetInteractableByFilter(region.structureType.ToString() == category);
                MeshFilter body = tooth.transform.Find("Mesh").GetComponent<MeshFilter>();
                Vector3[] vertices = body.sharedMesh.vertices;
                Bounds bounds = new Bounds(body.transform.TransformPoint(vertices[0]), Vector3.zero);
                foreach (Vector3 vertex in vertices) bounds.Encapsulate(body.transform.TransformPoint(vertex));
                Camera camera = preview.camera;
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.z) * .6f;
                camera.transform.position = bounds.center + Vector3.down * 8f;
                camera.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
                camera.nearClipPlane = .01f;
                camera.farClipPlane = 30f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.045f, .04f, .06f);
                preview.lights[0].intensity = 1.2f;
                preview.lights[0].transform.rotation = Quaternion.Euler(230, -30, 0);
                preview.lights[1].intensity = .6f;
                preview.ambientColor = new Color(.4f, .4f, .4f);
                preview.BeginStaticPreview(new Rect(0, 0, 900, 900));
                preview.Render(true);
                Texture2D texture = preview.EndStaticPreview();
                File.WriteAllBytes("StudyModePreview/20261008-exp-cutouts/" + category + ".png", texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            finally { preview.Cleanup(); }
        }
    }
}
