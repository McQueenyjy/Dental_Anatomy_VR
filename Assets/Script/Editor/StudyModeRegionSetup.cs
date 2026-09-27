using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;

public static class StudyModeRegionSetup
{
    private const string PrefabPath = "Assets/Prefabs/StudyMode/Maxillary_right_1st_molar.prefab";
    private const string RequestPath = "Temp/BuildStudyRegions.request";
    [Serializable] private class Alignment { public int index; public Vector3 offset; }
    [Serializable] private class AlignmentTable { public Alignment[] entries; }

    [InitializeOnLoadMethod]
    private static void HandleRequestedBuild()
    {
        if (File.Exists(RequestPath)) EditorApplication.delayCall += RunRequestedBuild;
    }

    private static void RunRequestedBuild()
    {
        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Exit Play mode before building the StudyMode regions.");
            return;
        }
        // Consume this explicit, one-time request before changing assets.
        File.Delete(RequestPath);
        BuildRegions();
    }

    [MenuItem("Tools/Dental/Build StudyMode Regions")]
    public static void BuildRegions()
    {
        Shader shader = Shader.Find("Dental/Anatomy Overlay");
        if (shader == null) throw new InvalidOperationException("Anatomy Overlay shader has not imported yet.");
        AlignmentTable table = JsonUtility.FromJson<AlignmentTable>(File.ReadAllText("Assets/Prefabs/StudyMode/RegionAlignment.json"));
        if (table.entries.Length != 23) throw new InvalidOperationException("Expected the mapped regions .001 to .023.");
        var meshes = new Dictionary<string, Mesh>();
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath("Assets/Maxillary_1st_Molar.fbx"))
            if (asset is Mesh mesh) meshes.Add(mesh.name, mesh);

        var materials = new Dictionary<AnatomyStructureType, Material>();
        foreach (AnatomyStructureType type in new[] { AnatomyStructureType.Cusp, AnatomyStructureType.Ridge, AnatomyStructureType.Fossa, AnatomyStructureType.Groove })
        {
            string path = "Assets/CarolineAssets/Materials/Mat_Study_" + type + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "Mat_Study_" + type };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", AnatomyRegion.CategoryColor(type));
            material.SetFloat("_SurfaceOffset", 0.012f);
            EditorUtility.SetDirty(material);
            materials.Add(type, material);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform baseMesh = root.transform.Find("Mesh");
            if (baseMesh == null) throw new InvalidOperationException("Tooth base Mesh was not found.");
            Transform generated = root.transform.Find("AnatomyRegions");
            if (generated == null) generated = Child(root.transform, "AnatomyRegions");
            // Existing Pit hotspots are retained. The earlier Ridge experiment remains available, disabled.
            foreach (string legacyName in new[] { "Oblique_Ridge_Group", "Mesiobuccal_Cusp_Group" })
            {
                Transform legacy = root.transform.Find(legacyName);
                if (legacy != null) legacy.gameObject.SetActive(false);
            }

            foreach (Alignment entry in table.entries)
            {
                AnatomyStructureType type = entry.index <= 5 ? AnatomyStructureType.Cusp :
                    entry.index <= 12 ? AnatomyStructureType.Ridge : entry.index <= 16 ? AnatomyStructureType.Fossa : AnatomyStructureType.Groove;
                Transform category = generated.Find(type.ToString());
                if (category == null) category = Child(generated, type.ToString());
                string regionName = type + "_" + entry.index.ToString("00");
                Transform region = category.Find(regionName);
                bool created = region == null;
                if (created) region = Child(category, regionName);
                region.localPosition = baseMesh.localPosition + baseMesh.localRotation * Vector3.Scale(baseMesh.localScale, entry.offset);
                region.localRotation = baseMesh.localRotation;
                region.localScale = baseMesh.localScale;

                Mesh mesh = meshes["Maxillary_1st_Molar." + entry.index.ToString("000")];
                MeshCollider hit = GetOrAdd<MeshCollider>(region.gameObject);
                hit.sharedMesh = mesh;
                hit.convex = false;
                XRSimpleInteractable interactable = GetOrAdd<XRSimpleInteractable>(region.gameObject);
                interactable.colliders.Clear();
                interactable.colliders.Add(hit);
                interactable.interactionLayers = 1;
                Transform highlight = region.Find("Highlight");
                if (highlight == null) highlight = Child(region, "Highlight");
                GetOrAdd<MeshFilter>(highlight.gameObject).sharedMesh = mesh;
                MeshRenderer renderer = GetOrAdd<MeshRenderer>(highlight.gameObject);
                renderer.sharedMaterial = materials[type];
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                AnatomyHotspot hotspot = GetOrAdd<AnatomyHotspot>(region.gameObject);
                hotspot.structureType = type;
                hotspot.highlightObject = highlight.gameObject;
                if (created) hotspot.structureName = type + " " + entry.index.ToString("00");
                // Preserve authored names and descriptions when rebuilding alignment.
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        ExportPreviews();
        File.WriteAllText("Temp/StudyModeRegionBuild.done.json", "{\"regions\":23,\"pitsRetained\":3,\"previews\":5}");
        Debug.Log("StudyMode: connected 23 mapped regions; retained the 3 Pit hotspots; exported category previews.");
    }

    private static Transform Child(Transform parent, string name)
    {
        Transform child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    [MenuItem("Tools/Dental/Export StudyMode Category Previews")]
    public static void ExportPreviews()
    {
        Directory.CreateDirectory("StudyModePreview");
        foreach (AnatomyStructureType type in Enum.GetValues(typeof(AnatomyStructureType)))
        {
            var preview = new PreviewRenderUtility();
            try
            {
                GameObject tooth = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                preview.AddSingleGO(tooth);
                foreach (AnatomyRegion region in tooth.GetComponentsInChildren<AnatomyRegion>(true))
                    region.SetInteractableByFilter(region.gameObject.activeInHierarchy && region.structureType == type);
                Bounds bounds = tooth.transform.Find("Mesh").GetComponent<Renderer>().bounds;
                Camera camera = preview.camera;
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.z) * 0.62f;
                camera.transform.position = bounds.center + Vector3.down * 8f;
                camera.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 30f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.045f, 0.04f, 0.06f);
                preview.lights[0].intensity = 1.2f;
                preview.lights[0].transform.rotation = Quaternion.Euler(230, -30, 0);
                preview.lights[1].intensity = 0.6f;
                preview.ambientColor = new Color(0.4f, 0.4f, 0.4f);
                preview.BeginStaticPreview(new Rect(0, 0, 800, 800));
                preview.Render(true);
                Texture2D texture = preview.EndStaticPreview();
                File.WriteAllBytes("StudyModePreview/" + type + ".png", texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            finally { preview.Cleanup(); }
        }
    }
}
