using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;

// Align only the three existing Pit regions. Other anatomy categories are untouched.
[InitializeOnLoad]
public static class StudyModePitSetup
{
    public const string PrefabPath = "Assets/Prefabs/StudyMode/Maxillary_right_1st_molar.prefab";
    private const string ModelPath = "Assets/Maxillary_1st_Molar.fbx";
    private const string MaterialPath = "Assets/CarolineAssets/Materials/Mat_Study_Pit.mat";
    private const string Request = "Temp/StudyModePitSetup.request";
    private static readonly string[] Names = { "Central Pit", "Distal Pit", "Mesial Pit" };
    // Translation fitted to tooth triangles, in the base FBX mesh's local coordinates.
    // Source: Maxillary_1st_Molar.fbx imported 2026-09-24; meshes .024/.025/.026.
    private static readonly Vector3[] Offsets = {
        new Vector3(-3.39559388f, 0.73312943f, -0.17761198f),
        new Vector3(-3.39522315f, 0.73410726f, -0.17543227f),
        new Vector3(-3.39396551f, 0.73073601f, -0.17418695f)
    };
    private static readonly Vector3[] SourceCenters = {
        new Vector3(3.17921877f, .98051912f, .59573412f),
        new Vector3(3.10284996f, .99921143f, .22412942f),
        new Vector3(3.48546028f, 1.05695295f, .89613771f)
    };

    static StudyModePitSetup() { EditorApplication.delayCall += RunRequested; }

    private static void RunRequested()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RunRequested;
            return;
        }
        File.Delete(Request);
        try
        {
            AlignPits();
            File.WriteAllText("Temp/StudyModePitSetup.result.json", "{\"passed\":true}");
        }
        catch (Exception e)
        {
            File.WriteAllText("Temp/StudyModePitSetup.result.json", JsonUtility.ToJson(new Failure { error = e.ToString() }));
            Debug.LogException(e);
        }
    }

    [Serializable] private class Failure { public bool passed; public string error; }

    [MenuItem("Tools/Dental/Align Existing Pit Meshes")]
    public static void AlignPits()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Exit Play Mode and Prefab Mode before aligning Pits.");
        Mesh[] assets = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Mesh>().ToArray();
        Mesh[] meshes = Enumerable.Range(24, 3).Select(index =>
            assets.Single(m => m.name == "Maxillary_1st_Molar." + index.ToString("000"))).ToArray();
        for (int i = 0; i < meshes.Length; i++)
            if (Vector3.Distance(meshes[i].bounds.center, SourceCenters[i]) > .0001f)
                throw new InvalidOperationException("The FBX has changed; recalculate the Pit alignment before using this command.");

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform regions = root.transform.Find("AnatomyRegions");
            Transform group = regions.Find("Pit");
            Transform toothMesh = root.transform.Find("Mesh");
            if (group == null || toothMesh == null) throw new InvalidOperationException("Missing Pit group or tooth Mesh.");
            AnatomyHotspot[] pits = Names.Select(name => root.GetComponentsInChildren<AnatomyHotspot>(true)
                .Single(p => p.structureName == name && p.structureType == AnatomyStructureType.Pit)).ToArray();
            foreach (var pit in pits)
                if (pit.transform.Find("Highlight") == null || pit.GetComponent<XRSimpleInteractable>() == null)
                    throw new InvalidOperationException("Missing existing Pit Highlight or XR interactable.");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Dental/Anatomy Overlay");
                if (shader == null) throw new InvalidOperationException("Missing anatomy overlay shader.");
                material = new Material(shader) { name = "Mat_Study_Pit" };
                material.SetColor("_BaseColor", AnatomyRegion.CategoryColor(AnatomyStructureType.Pit));
                material.SetFloat("_SurfaceOffset", 0f);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            for (int i = 0; i < pits.Length; i++)
            {
                AnatomyHotspot pit = pits[i];
                pit.transform.SetParent(group, false);
                pit.transform.position = toothMesh.TransformPoint(Offsets[i]);
                pit.transform.rotation = toothMesh.rotation;
                Vector3 scale = toothMesh.lossyScale;
                Vector3 parentScale = group.lossyScale;
                pit.transform.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
                Transform highlight = pit.transform.Find("Highlight");
                highlight.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                highlight.localScale = Vector3.one;
                highlight.gameObject.SetActive(true);
                MeshFilter filter = highlight.GetComponent<MeshFilter>();
                MeshRenderer renderer = highlight.GetComponent<MeshRenderer>();
                MeshCollider collider = highlight.GetComponent<MeshCollider>();
                if (filter == null || renderer == null || collider == null)
                    throw new InvalidOperationException("Missing mesh components on " + pit.name);
                filter.sharedMesh = meshes[i];
                collider.sharedMesh = meshes[i];
                collider.convex = false;
                collider.isTrigger = false;
                collider.enabled = true;
                renderer.sharedMaterial = material;
                renderer.enabled = true;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                pit.highlightObject = highlight.gameObject;
                var interactable = pit.GetComponent<XRSimpleInteractable>();
                interactable.colliders.Clear();
                interactable.colliders.Add(collider);
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        ExportPreview();
        Debug.Log("StudyMode: aligned Central/Distal/Mesial Pit meshes, colliders and filter references.");
    }

    [MenuItem("Tools/Dental/Export Pit Alignment Preview")]
    public static void ExportPreview()
    {
        Directory.CreateDirectory("StudyModePreview");
        var preview = new PreviewRenderUtility();
        try
        {
            GameObject tooth = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            preview.AddSingleGO(tooth);
            foreach (AnatomyRegion region in tooth.GetComponentsInChildren<AnatomyRegion>(true))
                region.SetInteractableByFilter(region.gameObject.activeInHierarchy && region.structureType == AnatomyStructureType.Pit);
            Bounds bounds = tooth.transform.Find("Mesh").GetComponent<Renderer>().bounds;
            Camera camera = preview.camera;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.z) * .58f;
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
            preview.BeginStaticPreview(new Rect(0, 0, 1000, 1000));
            preview.Render(true);
            Texture2D texture = preview.EndStaticPreview();
            File.WriteAllBytes("StudyModePreview/PitAligned.png", texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
        finally { preview.Cleanup(); }
    }
}
