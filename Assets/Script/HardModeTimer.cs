using UnityEngine;
using TMPro;

public class HardModeTimer : MonoBehaviour
{
    public TextMeshProUGUI timerText;

    private float startTime;
    private float elapsed;
    private bool running;

    public void StartTimer()
    {
        startTime = Time.time;
        elapsed = 0f;
        running = true;
        UpdateUI(0f);
    }

    public float StopTimer()
    {
        if (!running) return elapsed;
        elapsed = Time.time - startTime;
        running = false;
        UpdateUI(elapsed);
        return elapsed;
    }

    public void ResetTimer()
    {
        running = false;
        elapsed = 0f;
        UpdateUI(0f);
    }

    private void Update()
    {
        if (!running) return;
        elapsed = Time.time - startTime;
        UpdateUI(elapsed);
    }

    private void UpdateUI(float t)
    {
        if (timerText == null) return;
        int m = Mathf.FloorToInt(t / 60f);
        int s = Mathf.FloorToInt(t % 60f);
        int ms = Mathf.FloorToInt((t * 1000f) % 1000f);
        timerText.text = $"{m:00}:{s:00}.{ms:000}";
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}