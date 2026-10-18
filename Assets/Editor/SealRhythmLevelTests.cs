using System;
using System.Collections.Generic;
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
    private static bool savedBackground, savedLiveInput;
    private static int rolls, flips, leftRolls, rightRolls;
    private static SealCaptainAppearance captain;
    private static readonly List<SealRhythm.RhythmNote> captainBeats = new List<SealRhythm.RhythmNote>();
    private static int latestCaptainBeat, verifiedNods, verifiedLeft, verifiedRight;
    private static bool capturedVariation;
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
        savedBest = PlayerPrefs.GetInt("SealRhythm.BestScore.OriginalSOMJ01.v2", 0);
        savedOffset = game.TimingOffsetMs;
        savedLiveInput = game.LiveInputEnabled; game.LiveInputEnabled = false;
        game.ReturnToTitle(); game.SetTimingOffset(0);
        Require(game.CaptainDialogueVisible && game.CaptainDialogueText == "开始准备，根据节拍跟上队伍的动作",
            "Ready conductor gives the exact Chinese preparation instruction");
        CheckBackground();
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        phase = 1; submitted = -1; capturedVariation = false; startedAt = changedAt = EditorApplication.timeSinceStartup;
        savedBackground = Application.runInBackground; Application.runInBackground = true;
        File.WriteAllText(Path.Combine(Folder, "result.txt"), "RUNNING: physical Nod/Left/Right inputs, practice, count-in pause, restart, full-song autoplay, playback pause/resume.\n");
        game.SubmitInput(1);
        Require(game.characterAnimator.GetComponent<SealOriginalAnimation>().CurrentMotion == 4, "Left preview follows the left key");
        game.SubmitInput(0);
        Require(game.characterAnimator.GetComponent<SealOriginalAnimation>().CurrentMotion == 1, "Down previews a nod, never a roll");
        game.SubmitInput(2);
        Require(game.characterAnimator.GetComponent<SealOriginalAnimation>().CurrentMotion == 5, "Right preview follows the right key");
        game.ReturnToTitle(); Capture("01-ready");
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
            {
                game.TogglePause(); Require(game.State == SealGameState.Paused, "Count-in pause");
                Require(!game.CaptainDialogueVisible, "Count-in pause hides conductor dialogue");
                phase = 3; changedAt = now;
            }
            else if (phase == 3 && now - changedAt > .3)
            { game.TogglePause(); Require(game.State == SealGameState.CountIn, "Count-in resume"); phase = 4; }
            else if (phase == 4 && game.State == SealGameState.Results)
            {
                CheckResults("practice"); game.RestartGame(); Require(game.State == SealGameState.CountIn, "Restart enters count-in");
                game.TogglePause(); game.ReturnToTitle(); Require(game.State == SealGameState.Ready, "Return from pause");
                game.StartGame(false); WatchJudge(); submitted = -1; phase = 5;
            }
            else if (phase == 5 && game.State == SealGameState.Playing && game.SongTime > 20)
            {
                game.TogglePause(); pausedTime = game.SongTime;
                Require(!game.CaptainDialogueVisible, "Playback pause hides conductor dialogue");
                Capture("02-paused"); phase = 6; changedAt = now;
            }
            else if (phase == 6 && now - changedAt > .5)
            { Require(Math.Abs(pausedTime - game.SongTime) < .001f, "Paused sample clock is frozen"); game.TogglePause(); phase = 7; }
            else if (phase == 7 && game.State == SealGameState.Playing && game.SongTime > 22)
            { Capture("03-playing"); phase = 8; }
            else if (phase == 8 && game.State == SealGameState.Results)
            { CheckResults("full song"); Capture("04-results"); phase = 9; changedAt = now; }
            else if (phase == 9 && now - changedAt > .5)
            {
                game.SetTimingOffset(savedOffset); game.LiveInputEnabled = savedLiveInput;
                PlayerPrefs.SetInt("SealRhythm.BestScore.OriginalSOMJ01.v2", savedBest); PlayerPrefs.Save();
                File.AppendAllText(Path.Combine(Folder, "result.txt"), "PASS: sample clock, all notes, no stray input, pause/resume, restart, practice and results.\n");
                Debug.Log("SealRhythm full level smoke test PASSED. " + Folder); phase = 0; game.ReturnToTitle(); Application.runInBackground = savedBackground;
            }
            if ((phase == 4 || phase == 5 || phase == 7 || phase == 8) && game.State == SealGameState.Playing)
            {
                CheckCaptainPlayback();
                if (!game.IsPractice && !capturedVariation && game.SongTime >= 12.8f)
                {
                    Require(game.SongTime < 14f && game.CaptainDialogueVisible &&
                        HasChinese(game.CaptainDialogueText) &&
                        game.CaptainDialogueText != "开始准备，根据节拍跟上队伍的动作",
                        "A Chinese variation cue is visible before the first roll phrase");
                    capturedVariation = true; Capture("05-variation");
                    File.AppendAllText(Path.Combine(Folder, "result.txt"),
                        "Variation dialogue at " + game.SongTime.ToString("F3") + " s: " + game.CaptainDialogueText + "\n");
                }
                int index = game.Judge.NextNoteIndex;
                if (index < game.Judge.TotalNotes && index != submitted &&
                    game.SongTime * game.SampleRate >= game.Judge.Notes[index].Sample - game.SampleRate * .01f)
                {
                    var note = game.Judge.Notes[index];
                    // Independent original-chart fixture: all 230 scored actions advance
                    // direction, starting right. A roll uses that direction's physical key.
                    var input = note.Action == SealRhythm.SealAction.Roll
                        ? (index % 2 == 0 ? SealRhythm.SealInput.Right : SealRhythm.SealInput.Left)
                        : (SealRhythm.SealInput)note.Action;
                    Require(note.Input == input, "Original physical direction at note " + index);
                    submitted = index; game.SubmitInput((int)input);
                }
            }
        }
        catch (Exception exception)
        {
            File.AppendAllText(Path.Combine(Folder, "result.txt"), "FAIL: " + exception + "\n");
            PlayerPrefs.SetInt("SealRhythm.BestScore.OriginalSOMJ01.v2", savedBest);
            if (game != null) { game.SetTimingOffset(savedOffset); game.LiveInputEnabled = savedLiveInput; }
            Application.runInBackground = savedBackground; Debug.LogException(exception); phase = 0;
        }
    }
    private static void CheckResults(string mode)
    {
        var j = game.Judge;
        File.AppendAllText(Path.Combine(Folder, "result.txt"), mode + ": total=" + j.TotalNotes + ", perfect=" + j.PerfectCount + ", good=" + j.GoodCount + ", miss=" + j.MissCount + ", stray=" + j.StrayCount + ", score=" + j.Score + "\n");
        Require(j.TotalNotes > 0 && j.ResolvedCount == j.TotalNotes && j.MissCount == 0 && j.StrayCount == 0, mode + " all notes hit exactly once");
        Require(!game.CaptainDialogueVisible, mode + " results hide conductor dialogue");
        Require(captain != null && captain.BeatCueCount == captainBeats.Count,
            mode + " conductor receives all five intro beats and every scored beat exactly once");
        var lastBeat = captainBeats[captainBeats.Count - 1];
        Require(captain.LastBeatSample == lastBeat.Sample && captain.LastBeatInput == lastBeat.Input,
            mode + " conductor ends on the exact final chart sample and physical direction");
        Require(verifiedNods > 0 && verifiedLeft > 0 && verifiedRight > 0,
            mode + " actual playback verifies nod, left and right conductor poses");
        File.AppendAllText(Path.Combine(Folder, "result.txt"),
            mode + " conductor: " + captain.BeatCueCount + " exact sample cues; last=" +
            captain.LastBeatSample + ", input=" + captain.LastBeatInput +
            "; verified nod/left/right pose frames=" + verifiedNods + "/" + verifiedLeft + "/" + verifiedRight + ".\n");
        if (mode == "full song")
        {
            Require(captain.BeatCueCount == 235 && capturedVariation,
                "Full song verifies 235 conductor beats and captures the variation dialogue");
            Require(j.TotalNotes == 230 && rolls == 62 && flips == 168 && leftRolls == 31 && rightRolls == 31,
                "Original ROM flip/roll counts and both physical roll directions");
            var actors = UnityEngine.Object.FindObjectsByType<SealOriginalAnimation>();
            Require(actors.Length == 4, "Four original-animation seals");
            foreach (var actor in actors)
                Require(actor.CurrentMotion == 9 && !actor.IsUpsideDown && !actor.NextFlipToLeft,
                    "All four seals finish with the original final roll and phase");
            File.AppendAllText(Path.Combine(Folder, "result.txt"), "Original animation state: 4 seals, final motion 9, upright, next flip right. 168 flips / 62 rolls verified.\n");
            File.AppendAllText(Path.Combine(Folder, "result.txt"), "Physical controls: down nod preview; 31 left rolls / 31 right rolls through directional input.\n");
            CheckCharacterAppearances();
            CheckBackground();
        }
    }
    private static void CheckCharacterAppearances()
    {
        var seals = UnityEngine.Object.FindObjectsByType<SealCharacterAppearance>();
        Require(seals.Length == 4, "Four replacement seal appearances remain active after the full song");
        var sealTexture = Resources.Load<Texture2D>("Characters/ArcticBand/seals");
        Require(sealTexture != null, "Replacement seal artwork is available in the player Resources");
        int players = 0;
        var positions = new List<float>(seals.Length);
        foreach (var seal in seals)
        {
            positions.Add(seal.transform.position.x);
            if (seal.IsPlayer) players++;
            Require(seal.IsPlayer == (seal.gameObject == game.characterAnimator.gameObject),
                "Only the controlled seal uses the blue player artwork");
            RequireSingleArtworkRenderer(seal.gameObject, sealTexture, "Replacement seal");
            var originalVisual = seal.transform.Find("Original Wii seal");
            Require(originalVisual != null && !originalVisual.gameObject.activeInHierarchy,
                "Original seal graphics stay hidden while their animation controller runs");
        }
        Require(players == 1, "Exactly one blue player seal");
        positions.Sort();
        for (int i = 1; i < positions.Count; i++)
            Require(positions[i] - positions[i - 1] >= 1.05f,
                "All adjacent seal centers have at least 1.05 world units of spacing");
        var captains = UnityEngine.Object.FindObjectsByType<SealCaptainAppearance>();
        Require(captains.Length == 1 && captains[0].IsReady, "One initialized replacement penguin conductor");
        Require(captains[0].CurrentPose >= 0 && captains[0].CurrentPose < 6,
            "Conductor finishes on a valid replacement animation pose");
        var captainTexture = Resources.Load<Texture2D>("Characters/ArcticBand/captain");
        Require(captainTexture != null, "Replacement conductor artwork is available in the player Resources");
        RequireSingleArtworkRenderer(captains[0].gameObject, captainTexture, "Replacement conductor");
        File.AppendAllText(Path.Combine(Folder, "result.txt"),
            "Replacement artwork: 4 seals spaced at least 1.05 world units apart, exactly 1 blue controlled seal, 1 penguin conductor; original graphics hidden, one visible artwork renderer per actor.\n");
    }
    private static void CheckBackground()
    {
        var background = GameObject.Find("Bg");
        Require(background != null && background.TryGetComponent<SpriteRenderer>(out var renderer) &&
            renderer.sprite != null && renderer.sprite.texture != null && renderer.sprite.texture.name == "background",
            "The stage uses the new generated background artwork");
    }
    private static bool HasChinese(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (char character in text) if (character >= '\u4e00' && character <= '\u9fff') return true;
        return false;
    }
    private static void CheckCaptainPlayback()
    {
        int sample = Mathf.RoundToInt(game.SongTime * game.SampleRate);
        while (latestCaptainBeat + 1 < captainBeats.Count && captainBeats[latestCaptainBeat + 1].Sample <= sample)
            latestCaptainBeat++;
        int due = latestCaptainBeat + 1;
        Require(captain != null && captain.BeatCueCount >= 0 && captain.BeatCueCount <= due,
            "Conductor does not play future or extra chart beats");
        // Editor updates and MonoBehaviour.LateUpdate can run on opposite sides of one
        // rendered frame. Compare exact counts and poses only clear of that boundary.
        int frameSamples = Mathf.CeilToInt(Mathf.Max(1f / 60f, Time.unscaledDeltaTime) * game.SampleRate);
        if (due > 0 && sample - captainBeats[latestCaptainBeat].Sample > frameSamples)
            Require(captain.BeatCueCount == due, "Conductor follows the latest due audio-sample beat");
        if (captain.BeatCueCount == 0) return;
        var actualBeat = captainBeats[captain.BeatCueCount - 1];
        Require(captain.LastBeatSample == actualBeat.Sample && captain.LastBeatInput == actualBeat.Input,
            "Conductor input and sample match the independent merged intro/scored sequence");
        int elapsed = sample - actualBeat.Sample;
        if (elapsed <= frameSamples || elapsed >= game.SampleRate * .18f) return;
        int expectedPose = actualBeat.Input == SealRhythm.SealInput.Nod ? 3 :
            actualBeat.Input == SealRhythm.SealInput.Left ? 1 : 2;
        Require(captain.CurrentPose == expectedPose, "Conductor shows the corresponding nod/left/right pose on the beat");
        if (actualBeat.Input == SealRhythm.SealInput.Nod) verifiedNods++;
        else if (actualBeat.Input == SealRhythm.SealInput.Left) verifiedLeft++;
        else verifiedRight++;
    }
    private static void RequireSingleArtworkRenderer(GameObject actor, Texture2D texture, string label)
    {
        int visible = 0;
        foreach (var renderer in actor.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            visible++;
            Require(renderer.sprite != null && renderer.sprite.texture == texture,
                label + " uses the new generated artwork");
        }
        Require(visible == 1, label + " has exactly one visible renderer, without old-art overlays");
    }
    private static void WatchJudge()
    {
        rolls = flips = leftRolls = rightRolls = 0;
        captain = UnityEngine.Object.FindAnyObjectByType<SealCaptainAppearance>();
        Require(captain != null && captain.IsReady && captain.BeatCueCount == 0,
            "Conductor resets its chart state before each playback");
        captainBeats.Clear();
        foreach (var intro in game.IntroNotes) captainBeats.Add(intro);
        foreach (var note in game.Judge.Notes) captainBeats.Add(note);
        captainBeats.Sort((a, b) => a.Sample.CompareTo(b.Sample));
        Require(game.IntroNotes.Count == 5, "Five original unscored nod demonstrations");
        latestCaptainBeat = -1; verifiedNods = verifiedLeft = verifiedRight = 0;
        game.Judge.Judged += judgement =>
        {
            if (judgement.Grade == SealRhythm.HitGrade.Perfect || judgement.Grade == SealRhythm.HitGrade.Good)
            {
                if (judgement.Action == SealRhythm.SealAction.Roll)
                {
                    rolls++;
                    if (game.Judge.Notes[judgement.NoteIndex].Input == SealRhythm.SealInput.Left) leftRolls++;
                    else rightRolls++;
                }
                else if (judgement.Action == SealRhythm.SealAction.Left || judgement.Action == SealRhythm.SealAction.Right) flips++;
            }
            if (judgement.Grade == SealRhythm.HitGrade.Miss || judgement.Grade == SealRhythm.HitGrade.Stray)
                File.AppendAllText(Path.Combine(Folder, "result.txt"), "Unexpected " + judgement.Grade + " note=" + judgement.NoteIndex + " offset=" + judgement.OffsetMilliseconds + "ms song=" + game.SongTime + "\n");
        };
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Capture(string name) { ScreenCapture.CaptureScreenshot(Path.Combine(Folder, name + ".png")); }
}
