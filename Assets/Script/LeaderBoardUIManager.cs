using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class LeaderboardUIManager : MonoBehaviour
{
    [Header("UI")]
    public GameObject leaderboardRoot;       // 你的排行榜 Canvas 根物体（整个UI）
    public TMP_Text leaderboardText;         // 显示排行榜内容的文本

    [Header("Input")]
    public InputActionReference toggleAction; // 绑定到 ToggleLeaderboard

    [Header("Data")]
    public int maxRecords = 10;
    private const string PrefKey = "LB_HardMode_TopTimes"; // 你也可以按关卡/模式拆分key

    void OnEnable()
    {
        if (toggleAction != null)
        {
            toggleAction.action.performed += OnToggle;
            toggleAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (toggleAction != null)
        {
            toggleAction.action.performed -= OnToggle;
            toggleAction.action.Disable();
        }
    }

    void Start()
    {
        if (leaderboardRoot) leaderboardRoot.SetActive(false);
        RefreshUI();
    }

    private void OnToggle(InputAction.CallbackContext ctx)
    {
        if (!leaderboardRoot) return;
        bool next = !leaderboardRoot.activeSelf;
        leaderboardRoot.SetActive(next);

        if (next) RefreshUI();
    }

    // 你完成 HardMode 后调用这个函数：AddRecord(elapsedSeconds)
    public void AddRecord(float seconds)
    {
        var list = LoadTimes();
        list.Add(seconds);
        list.Sort();
        if (list.Count > maxRecords) list.RemoveRange(maxRecords, list.Count - maxRecords);
        SaveTimes(list);
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (!leaderboardText) return;

        var list = LoadTimes();
        if (list.Count == 0)
        {
            leaderboardText.text = "No records yet.\nFinish Hard Mode to create a record!";
            return;
        }

        // 输出格式：1) 00:32.15
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < list.Count; i++)
        {
            sb.AppendLine($"{i + 1}) {FormatTime(list[i])}");
        }
        leaderboardText.text = sb.ToString();
    }

    private static string FormatTime(float sec)
    {
        int m = Mathf.FloorToInt(sec / 60f);
        float s = sec - m * 60f;
        return $"{m:00}:{s:00.00}";
    }

    private List<float> LoadTimes()
    {
        string raw = PlayerPrefs.GetString(PrefKey, "");
        var list = new List<float>();
        if (string.IsNullOrEmpty(raw)) return list;

        string[] parts = raw.Split('|');
        foreach (var p in parts)
        {
            if (float.TryParse(p, out float v)) list.Add(v);
        }
        return list;
    }
    public void Show()
    {
        if (!leaderboardRoot) return;
        leaderboardRoot.SetActive(true);
        RefreshUI();
    }

    public void Hide()
    {
        if (!leaderboardRoot) return;
        Debug.Log($"close leaderboard");
        leaderboardRoot.SetActive(false);
    }
    private void SaveTimes(List<float> list)
    {
        string raw = string.Join("|", list.ConvertAll(t => t.ToString("F4")));
        PlayerPrefs.SetString(PrefKey, raw);
        PlayerPrefs.Save();
    }
}