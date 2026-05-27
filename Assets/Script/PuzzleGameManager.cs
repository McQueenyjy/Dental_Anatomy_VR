using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

public class PuzzleGameManager : MonoBehaviour
{
    [Header("Difficulty Toggles")]
    public bool labelsOn = true;
    public bool shuffleWithinSide = false;
    public bool swapLeftRight = false;

    [Header("Layout Points")]
    public Transform[] leftPoints;
    public Transform[] rightPoints;

    [Header("Teeth Pieces")]
    public PuzzlePiece[] leftTeeth;
    public PuzzlePiece[] rightTeeth;

    [Header("Slots (for reset)")]
    public PuzzleSlot[] allSlots;

    [Header("Hard Mode Timer & Leaderboard")]
    public HardModeTimer hardTimer;
    public Leaderboard leaderboard;
    public GameObject timerCanvas;
    public LeaderboardUIManager leaderboardUI;

    private int totalPieces;
    private int placedCount;
    private bool hardRunActive;

    private List<PuzzlePiece> currentLeftLayout = new List<PuzzlePiece>();
    private List<PuzzlePiece> currentRightLayout = new List<PuzzlePiece>();


    private void OnEnable()
    {
        PuzzlePiece.OnAnyPiecePlaced += HandlePiecePlaced;
    }

    private void OnDisable()
    {
        PuzzlePiece.OnAnyPiecePlaced -= HandlePiecePlaced;
    }

    private void Start()
    {
        totalPieces = (leftTeeth?.Length ?? 0) + (rightTeeth?.Length ?? 0);

        ApplyDifficultyFromSettings();
        RebuildForCurrentDifficulty(resetPuzzleState: true);
        StartOrStopTimerForMode();
    }

    // ====== Runtime switch API (UI按钮会调用它们) ======
    public void SwitchToEasy()
    {
        GameSettings.difficulty = 0;
        ApplyDifficultyFromSettings();
        RebuildForCurrentDifficulty(resetPuzzleState: true);
        StartOrStopTimerForMode();
    }

    public void SwitchToHard()
    {
        GameSettings.difficulty = 1;
        ApplyDifficultyFromSettings();
        RebuildForCurrentDifficulty(resetPuzzleState: true);
        StartOrStopTimerForMode();
    }

    // ====== One place to rebuild layout/labels/home ======
    private void RebuildForCurrentDifficulty(bool resetPuzzleState)
    {
        ApplyLayout();
        if (resetPuzzleState)
        {
            ResetSlots();
            ResetPiecesToLayoutPositions(); // 重置 isPlaced/grab/rb 等，且把home更新为当前布局
        }
        ApplyLabels();
        ResetAllHomePositions(); // 把“当前摆放”记录为每颗牙的home，方便你做“放错弹回”
    }

    private void ApplyDifficultyFromSettings()
    {
        if (GameSettings.difficulty == 0) // Easy
        {
            labelsOn = true;
            shuffleWithinSide = false;
            swapLeftRight = false;
        }
        else // Hard
        {
            labelsOn = false;
            shuffleWithinSide = true;
            swapLeftRight = true;
        }
    }

    public void ApplyLayout()
    {
        List<PuzzlePiece> left = new List<PuzzlePiece>(leftTeeth);
        List<PuzzlePiece> right = new List<PuzzlePiece>(rightTeeth);

        if (shuffleWithinSide)
        {
            Shuffle(left);
            Shuffle(right);
          
        }

        if (swapLeftRight)
        {
            var temp = left;
            left = right;
            right = temp;
        }

        PlaceGroup(left, leftPoints);
        PlaceGroup(right, rightPoints);
        currentLeftLayout = left;
        currentRightLayout = right;
    }

    private void PlaceGroup(List<PuzzlePiece> pieces, Transform[] points)
    {
        int count = Mathf.Min(pieces.Count, points.Length);
        for (int i = 0; i < count; i++)
        {
            pieces[i].transform.position = points[i].position;
            pieces[i].transform.rotation = points[i].rotation;
        }
    }

    private void ApplyLabels()
    {
        foreach (var p in leftTeeth)
            if (p != null) p.SetLabelVisible(labelsOn);

        foreach (var p in rightTeeth)
            if (p != null) p.SetLabelVisible(labelsOn);
    }

    private void ResetAllHomePositions()
    {
        foreach (var p in leftTeeth)
            if (p != null) p.ResetHomeToCurrent();

        foreach (var p in rightTeeth)
            if (p != null) p.ResetHomeToCurrent();
    }


    private void ResetSlots()
    {
        if (allSlots == null || allSlots.Length == 0)
        {
            // 可选：自动收集（如果你不想手动拖）
            allSlots = FindObjectsOfType<PuzzleSlot>(true);
        }

        foreach (var s in allSlots)
            if (s != null) s.ResetSlot();
    }

    private void ResetPiecesToLayoutPositions()
    {
        //reset count
        placedCount = 0;

        //left
        int leftCount = Mathf.Min(currentLeftLayout.Count, leftPoints.Length);
        for (int i = 0; i < leftCount; i++)
        {
            var p = currentLeftLayout[i];
            if (p == null) continue;

            p.ResetForNewRound(leftPoints[i].position, leftPoints[i].rotation, labelsOn);
        }

        //right
        int rightCount = Mathf.Min(currentRightLayout.Count, rightPoints.Length);
        for (int i = 0; i <rightCount; i++)
        {
            var p = currentRightLayout[i];
            if (p == null) continue;

            p.ResetForNewRound(rightPoints[i].position, rightPoints[i].rotation, labelsOn);
        }
    }

    private void StartOrStopTimerForMode()
    {
        bool isHard = (GameSettings.difficulty == 1);

        hardRunActive = isHard;
        placedCount = 0;

   
        if (timerCanvas != null)
            timerCanvas.SetActive(isHard);

        if (hardTimer != null)
        {
            if (isHard) hardTimer.StartTimer();
            else hardTimer.ResetTimer();
        }
    }

    private void HandlePiecePlaced(PuzzlePiece piece)
    {
        if (!hardRunActive) return; // 只在 Hard mode 计时/统计

        placedCount++;

        if (placedCount >= totalPieces)
        {
            hardRunActive = false;

            float finalTime = (hardTimer != null) ? hardTimer.StopTimer() : 0f;

            if (leaderboardUI != null)
            {
                leaderboardUI.AddRecord(finalTime);

                // 自动弹出排行榜（想自动弹出就保留，不想就删掉这一行）
                leaderboardUI.Show();
            }
        }
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rand = Random.Range(i, list.Count);
            (list[i], list[rand]) = (list[rand], list[i]);
        }
    }
}
