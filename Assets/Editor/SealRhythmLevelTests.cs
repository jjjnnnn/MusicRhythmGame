using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Runs real audio playback through the public input path; never ships with the player.</summary>
[InitializeOnLoad]
public static class SealRhythmLevelTests
{
    private static RhythmGame game;
    private static int phase, submitted = -1, savedBest;
    private static float savedOffset;
    private static bool savedBackground;
    private static double changedAt, startedAt;
    private static float pausedTime;
    private static readonly string Folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "SealValidation");

    static SealRhythmLevelTests() { EditorApplication.update += Tick; }

    [MenuItem("Tools/Seal Rhythm/Run Full Level Smoke Test")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first, then run this test.");
        game = UnityEngine.Object.FindAnyObjectByType<RhythmGame>();
        if (game == null || game.Judge == null) throw new InvalidOperationException("Open GamePlay and wait for initialization.");
        Directory.CreateDirectory(Folder);
        savedBest = PlayerPrefs.GetInt("SealRhythm.BestScore.v1", 0);
        savedOffset = game.TimingOffsetMs;
        game.ReturnToTitle(); game.SetTimingOffset(0);
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        phase = 1; submitted = -1; startedAt = changedAt = EditorApplication.timeSinceStartup;
        savedBackground = Application.runInBackground; Application.runInBackground = true;
        File.WriteAllText(Path.Combine(Folder, "result.txt"), "RUNNING: practice, count-in pause, restart, full-song autoplay, playback pause/resume.\n");
    }

    private static void Tick()
    {
        if (phase == 0) return;
        if (!EditorApplication.isPlaying || game == null) { phase = 0; return; }
        try
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - startedAt > 180) throw new Exception("Playback timed out, phase=" + phase + " state=" + game.State);
            if (game.State == SealGameState.Error) throw new Exception(game.StatusMessage);
            // MCP/editor windows can take focus during unattended runs. Intentional pauses are
            // verified in phases 3 and 6; resume external focus pauses in all other phases.
            if (game.State == SealGameState.Paused && phase != 3 && phase != 6) game.TogglePause();
            if (phase == 1 && now - changedAt > .5)
            { game.StartGame(true); WatchJudge(); phase = 2; changedAt = now; }
            else if (phase == 2 && game.State == SealGameState.CountIn && now - changedAt > .3)
            { game.TogglePause(); Require(game.State == SealGameState.Paused, "Count-in pause"); phase = 3; changedAt = now; }
            else if (phase == 3 && now - changedAt > .3)
            { game.TogglePause(); Require(game.State == SealGameState.CountIn, "Count-in resume"); phase = 4; }
            else if (phase == 4 && game.State == SealGameState.Results)
            {
                CheckResults("practice"); game.RestartGame(); Require(game.State == SealGameState.CountIn, "Restart enters count-in");
                game.TogglePause(); game.ReturnToTitle(); Require(game.State == SealGameState.Ready, "Return from pause");
                game.StartGame(false); WatchJudge(); submitted = -1; phase = 5;
            }
            else if (phase == 5 && game.State == SealGameState.Playing && game.SongTime > 20)
            { game.TogglePause(); pausedTime = game.SongTime; Capture("02-paused"); phase = 6; changedAt = now; }
            else if (phase == 6 && now - changedAt > .5)
            { Require(Math.Abs(pausedTime - game.SongTime) < .001f, "Paused sample clock is frozen"); game.TogglePause(); phase = 7; }
            else if (phase == 7 && game.State == SealGameState.Playing && game.SongTime > 22)
            { Capture("03-playing"); phase = 8; }
            else if (phase == 8 && game.State == SealGameState.Results)
            { CheckResults("full song"); Capture("04-results"); phase = 9; changedAt = now; }
            else if (phase == 9 && now - changedAt > .5)
            {
                game.SetTimingOffset(savedOffset); PlayerPrefs.SetInt("SealRhythm.BestScore.v1", savedBest); PlayerPrefs.Save();
                File.AppendAllText(Path.Combine(Folder, "result.txt"), "PASS: sample clock, all notes, no stray input, pause/resume, restart, practice and results.\n");
                Debug.Log("SealRhythm full level smoke test PASSED. " + Folder); phase = 0; game.ReturnToTitle(); Application.runInBackground = savedBackground;
            }
            if ((phase == 4 || phase == 5 || phase == 7 || phase == 8) && game.State == SealGameState.Playing)
            {
                int index = game.Judge.NextNoteIndex;
                if (index < game.Judge.TotalNotes && index != submitted && game.SongTime * 44100 >= game.Judge.Notes[index].Sample - 441)
                { submitted = index; game.SubmitAction((int)game.Judge.Notes[index].Action); }
            }
        }
        catch (Exception exception)
        {
            File.AppendAllText(Path.Combine(Folder, "result.txt"), "FAIL: " + exception + "\n");
            PlayerPrefs.SetInt("SealRhythm.BestScore.v1", savedBest);
            if (game != null) game.SetTimingOffset(savedOffset);
            Application.runInBackground = savedBackground; Debug.LogException(exception); phase = 0;
        }
    }
    private static void CheckResults(string mode)
    {
        var j = game.Judge;
        File.AppendAllText(Path.Combine(Folder, "result.txt"), mode + ": total=" + j.TotalNotes + ", perfect=" + j.PerfectCount + ", good=" + j.GoodCount + ", miss=" + j.MissCount + ", stray=" + j.StrayCount + ", score=" + j.Score + "\n");
        Require(j.TotalNotes > 0 && j.ResolvedCount == j.TotalNotes && j.MissCount == 0 && j.StrayCount == 0, mode + " all notes hit exactly once");
    }
    private static void WatchJudge()
    {
        game.Judge.Judged += judgement =>
        {
            if (judgement.Grade == SealRhythm.HitGrade.Miss || judgement.Grade == SealRhythm.HitGrade.Stray)
                File.AppendAllText(Path.Combine(Folder, "result.txt"), "Unexpected " + judgement.Grade + " note=" + judgement.NoteIndex + " offset=" + judgement.OffsetMilliseconds + "ms song=" + game.SongTime + "\n");
        };
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Capture(string name) { ScreenCapture.CaptureScreenshot(Path.Combine(Folder, name + ".png")); }
}
