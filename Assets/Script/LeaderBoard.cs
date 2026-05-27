using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Leaderboard : MonoBehaviour
{
    [Header("Storage")]
    public int maxEntries = 10;
    public string prefsKey = "HardModeLeaderboard";

    [Header("UI (optional)")]
    public GameObject panel;             // 排行榜面板（可为空）
    public TextMeshProUGUI titleText;    // “Your Time: xx”
    public TextMeshProUGUI listText;     // 1..N 列表文本

    private List<float> scores = new List<float>();

    private void Awake()
    {
        Load();
        if (panel != null) panel.SetActive(false);
        RefreshUI(-1f);
    }

    public void TryAddScore(float seconds)
    {
        Load();
        scores.Add(seconds);
        scores.Sort(); // 小 = 更好
        if (scores.Count > maxEntries)
            scores.RemoveRange(maxEntries, scores.Count - maxEntries);

        Save();
    }

    public void Show(float yourTime)
    {
        RefreshUI(yourTime);
        if (panel != null) panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    public void ClearScores()
    {
        PlayerPrefs.DeleteKey(prefsKey);
        PlayerPrefs.Save();
        scores.Clear();
        RefreshUI(-1f);
    }

    private void RefreshUI(float yourTime)
    {
        Load();

        if (titleText != null)
        {
            titleText.text = (yourTime >= 0f)
                ? $"Finished! Your Time: {Format(yourTime)}"
                : "Hard Mode Leaderboard";
        }

        if (listText != null)
        {
            if (scores.Count == 0)
            {
                listText.text = "No records yet.";
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < scores.Count; i++)
                sb.AppendLine($"{i + 1}. {Format(scores[i])}");
            listText.text = sb.ToString();
        }
    }

    private string Format(float t)
    {
        int m = Mathf.FloorToInt(t / 60f);
        int s = Mathf.FloorToInt(t % 60f);
        int ms = Mathf.FloorToInt((t * 1000f) % 1000f);
        return $"{m:00}:{s:00}.{ms:000}";
    }

    private void Save()
    {
        PlayerPrefs.SetString(prefsKey, string.Join(",", scores));
        PlayerPrefs.Save();
    }

    private void Load()
    {
        scores.Clear();
        string s = PlayerPrefs.GetString(prefsKey, "");
        if (string.IsNullOrEmpty(s)) return;

        string[] parts = s.Split(',');
        foreach (var p in parts)
            if (float.TryParse(p, out float v))
                scores.Add(v);

        scores.Sort();
        if (scores.Count > maxEntries)
            scores.RemoveRange(maxEntries, scores.Count - maxEntries);
    }
}