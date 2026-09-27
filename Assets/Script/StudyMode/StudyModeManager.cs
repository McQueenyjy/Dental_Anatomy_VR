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

    private GameObject currentTooth;



    private void Start()
    {
        if (spawnFirstOnStart && teeth != null && teeth.Length > 0)
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

        ClearCurrentTooth();

        currentTooth = Instantiate(entry.viewerPrefab, currentToothRoot);
        currentTooth.name = entry.toothId;

        currentTooth.transform.localPosition = entry.localPosition;
        currentTooth.transform.localRotation = Quaternion.Euler(entry.localEulerAngles);
        currentTooth.transform.localScale = entry.localScale;
        FindHotspotManager()?.ApplyStructureFilters();
    }


    
    public void ClearCurrentTooth()
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
