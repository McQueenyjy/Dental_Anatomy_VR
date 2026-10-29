using UnityEngine;

public class StudyModeManager : MonoBehaviour
{
    [System.Serializable]
    public class ToothEntry
    {
        public string toothId;
        public GameObject viewerPrefab;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
    }

    [Header("Spawn")]
    public Transform currentToothRoot;
    public ToothEntry[] teeth;
    public bool spawnFirstOnStart = true;

    [Header("Shared Tooth Viewer")]
    [Tooltip("The full dental arch shown while choosing a tooth.")]
    public GameObject toothSelectionRoot;
    [Tooltip("The Back to teeth button, shown only while inspecting a tooth.")]
    public GameObject backToTeethButton;

    [Tooltip("Fit the spawned instance inside the shared viewer without changing the source prefab.")]
    public bool fitToSharedViewer = true;
    public Vector3 toothViewSize = new Vector3(3.2f, 2.1f, 1.4f);
    public Vector3 toothViewCenter = new Vector3(0f, -.12f, -.1f);

    public bool IsViewingTooth => currentTooth != null;

    private GameObject currentTooth;
    private void Start()
    {
        // A shared viewer starts with the arch. Preserve the old spawn option
        // for scenes that do not use the shared selection/viewing flow.
        if (toothSelectionRoot != null)
        {
            UpdateView();
        }
        else if (currentTooth == null && spawnFirstOnStart && teeth != null && teeth.Length > 0)
        {
            SelectTooth(teeth[0].toothId);
        }
    }

    public void SelectTooth(string toothId)
    {
        ToothEntry entry = FindTooth(toothId);
        if (entry == null || entry.viewerPrefab == null || currentToothRoot == null)
        {
            Debug.LogWarning($"StudyModeManager: Cannot spawn tooth '{toothId}'.");
            return;
        }

        DestroyCurrentTooth();

        currentTooth = Instantiate(entry.viewerPrefab, currentToothRoot);
        currentTooth.name = entry.toothId;

        currentTooth.transform.localPosition = entry.localPosition;
        currentTooth.transform.localRotation = Quaternion.Euler(entry.localEulerAngles);
        currentTooth.transform.localScale = entry.localScale;
        FitCurrentTooth();
        UpdateView();
        FindHotspotManager()?.ApplyStructureFilters();
    }
    public void ClearCurrentTooth()
    {
        DestroyCurrentTooth();
        UpdateView();
    }

    public void ReturnToToothSelection()
    {
        ClearCurrentTooth();
    }

    private void FitCurrentTooth()
    {
        if (toothSelectionRoot == null || !fitToSharedViewer ||
            toothViewSize.x <= 0f || toothViewSize.y <= 0f || toothViewSize.z <= 0f) return;

        // Mesh bounds work even before an inactive spawn container is enabled.
        // Reading bounds does not require Read/Write access to vertex data.
        Bounds bounds = default;
        bool hasBounds = false;
        foreach (MeshFilter filter in currentTooth.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Bounds meshBounds = filter.sharedMesh.bounds;
            Matrix4x4 toViewer = currentToothRoot.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = meshBounds.center + Vector3.Scale(meshBounds.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                point = toViewer.MultiplyPoint3x4(point);
                if (!hasBounds) { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                else bounds.Encapsulate(point);
            }
        }
        if (!hasBounds || bounds.size.sqrMagnitude < .000001f) return;

        float scale = Mathf.Min(toothViewSize.x / Mathf.Max(bounds.size.x, .0001f),
            toothViewSize.y / Mathf.Max(bounds.size.y, .0001f),
            toothViewSize.z / Mathf.Max(bounds.size.z, .0001f));
        Transform tooth = currentTooth.transform;
        Vector3 centerOffset = bounds.center - tooth.localPosition;
        tooth.localScale *= scale;
        tooth.localPosition = toothViewCenter - centerOffset * scale;
    }

    private void UpdateView()
    {
        if (toothSelectionRoot != null)
        {
            toothSelectionRoot.SetActive(!IsViewingTooth);
            if (currentToothRoot != null)
                currentToothRoot.gameObject.SetActive(IsViewingTooth);
        }
        if (backToTeethButton != null)
            backToTeethButton.SetActive(IsViewingTooth);
    }

    private void DestroyCurrentTooth()
    {
        FindHotspotManager()?.ClearSelection();
        if (currentTooth != null)
        {
            // Destroy is deferred; stop old hit areas receiving rays immediately.
            currentTooth.SetActive(false);
            Destroy(currentTooth);
            currentTooth = null;
        }
    }

    private AnatomyHotspotManager FindHotspotManager()
    {
        AnatomyHotspotManager manager = GetComponent<AnatomyHotspotManager>();
        if (manager != null) return manager;
        manager = AnatomyHotspotManager.Active;
        return manager != null && manager.gameObject.scene == gameObject.scene ? manager : null;
    }

    private ToothEntry FindTooth(string toothId)
    {
        if (teeth == null) return null;

        foreach (ToothEntry tooth in teeth)
        {
            if (tooth != null && tooth.toothId == toothId)
            {
                return tooth;
            }
        }

        return null;
    }
}
