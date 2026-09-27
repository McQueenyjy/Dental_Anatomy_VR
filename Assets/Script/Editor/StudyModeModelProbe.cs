using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Temporary, read-only report of the imported FBX and its coordinate systems.
[InitializeOnLoad]
public static class StudyModeModelProbe
{
    [Serializable] private class Entry
    {
        public string objectName;
        public string meshName;
        public long meshFileId;
        public Vector3 position;
        public Vector3 eulerAngles;
        public Vector3 scale;
        public Vector3 boundsCenter;
        public Vector3 boundsSize;
        public int vertices;
        public Vector3[] vertexPositions;
        public int[] triangles;
    }
    [Serializable] private class Report { public List<Entry> entries = new List<Entry>(); }

    static StudyModeModelProbe()
    {
        EditorApplication.delayCall += Export;
    }

    private static void Export()
    {
        const string output = "Temp/StudyModeModelGeometry.json";
        if (File.Exists(output)) return;
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Maxillary_1st_Molar.fbx");
        if (model == null) return;
        Report report = new Report();
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null) continue;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long fileId);
            report.entries.Add(new Entry {
                objectName = filter.name, meshName = mesh.name, meshFileId = fileId,
                position = filter.transform.position, eulerAngles = filter.transform.rotation.eulerAngles,
                scale = filter.transform.lossyScale, boundsCenter = mesh.bounds.center,
                boundsSize = mesh.bounds.size, vertices = mesh.vertexCount,
                vertexPositions = mesh.vertices, triangles = mesh.triangles
            });
        }
        File.WriteAllText(output, JsonUtility.ToJson(report, true));
        Debug.Log("StudyMode model report exported: " + report.entries.Count + " meshes.");
    }
}
