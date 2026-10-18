using System;
using System.Collections.Generic;
using System.IO;
using SealRhythm;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Checks generated rhythm bars against real music without automatically playing any notes.</summary>
[InitializeOnLoad]
public static class SealBeatLaneVisibilityTests
{
    private static RhythmGame game;
    private static SealBeatLane lane;
    private static SealRhythmJudge observedJudge;
    private static readonly HashSet<int> Resolved = new HashSet<int>();
    private static bool running, savedAssist, savedBackground, savedLiveInput, checkedIntro, moved, capturedChart;
    private static float savedOffset, previousDistance;
    private static int previousIndex = -1, previousSample;
    private static double startedAt, captureAt = -1;
    private static readonly string Folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "SealValidation", "RhythmBars");

    static SealBeatLaneVisibilityTests() { EditorApplication.update += Tick; }

    // Retain the old entry point for existing project-check scripts.
    [MenuItem("Tools/Seal Rhythm/Run Rolling Arrow Test")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode before running the rhythm bar test.");
        if (running) throw new InvalidOperationException("The rhythm bar test is already running.");
        game = UnityEngine.Object.FindAnyObjectByType<RhythmGame>();
        lane = UnityEngine.Object.FindAnyObjectByType<SealBeatLane>();
        if (game == null || game.Judge == null || lane == null)
            throw new InvalidOperationException("Open GamePlay and wait for the game and beat guide to initialize.");
        Directory.CreateDirectory(Folder);
        savedAssist = game.AssistEnabled; savedOffset = game.TimingOffsetMs;
        savedLiveInput = game.LiveInputEnabled; game.LiveInputEnabled = false;
        savedBackground = Application.runInBackground;
        checkedIntro = moved = capturedChart = false;
        previousIndex = -1; previousSample = 0; captureAt = -1;
        Resolved.Clear();
        startedAt = EditorApplication.timeSinceStartup; running = true;
        File.WriteAllText(Path.Combine(Folder, "result.txt"), "RUNNING: real music, accent off, generated vertical bars, one circle button and inward travel.\n");
        try
        {
            game.ReturnToTitle(); game.SetTimingOffset(0);
            if (game.AssistEnabled) game.ToggleAssist();
            Application.runInBackground = true;
            game.StartGame(false);
            observedJudge = game.Judge;
            observedJudge.Judged += OnJudged;
        }
        catch (Exception exception) { Finish(exception); }
    }

    [MenuItem("Tools/Seal Rhythm/Run Rhythm Bar Test")]
    public static void RunRhythmBarTest() { Run(); }

    private static void Tick()
    {
        if (!running) return;
        try
        {
            if (!EditorApplication.isPlaying || game == null || lane == null)
                throw new Exception("Play mode or the game ended during the rhythm bar test.");
            double now = EditorApplication.timeSinceStartup;
            if (now - startedAt > 45) throw new Exception("Timed out while checking real music playback.");
            if (game.State == SealGameState.Error) throw new Exception(game.StatusMessage);
            if (game.State == SealGameState.Paused) game.TogglePause();
            if (game.State != SealGameState.Playing) return;
            Require(!game.AssistEnabled, "Core rhythm bars remain visible with the accent setting off");
            Require(game.Judge.PerfectCount == 0 && game.Judge.GoodCount == 0,
                "The beat guide never automatically scores a player's notes");
            // Editor ticks can precede the HUD's LateUpdate. Refresh at the same public
            // input sample used for expectations rather than comparing different frames.
            lane.Refresh();

            if (!checkedIntro && game.SongTime > 3.2f)
            {
                Require(FindBar() != null, "Opening scored beats show generated bars before their beat");
                ValidateChartMarkers();
                var player = game.characterAnimator.GetComponent<SealOriginalAnimation>();
                Require(player.CurrentMotion == 1 && player.CurrentMotionFrame >= player.CurrentMotionDuration,
                    "An opening nod does not automatically animate the player without an input");
                ValidateVisibleBars(); ValidateSingleControl();
                Capture("01-intro"); checkedIntro = true;
                Log("Intro: one mirrored pair per scored beat, generated 14x48 bars on the line, one circle control; no automatic player nod.");
            }
            if (game.SongTime > 7.5f)
            {
                ValidateVisibleBars(); ValidateChartMarkers();
                if (!capturedChart)
                {
                    Capture("02-chart"); capturedChart = true;
                    Log("Main chart: left, right and roll actions share the same vertical beat-bar artwork.");
                }
            }
            if (!moved && game.SongTime > 9f) CheckInwardTravel();
            if (game.SongTime > 12f && captureAt < 0)
            {
                Require(checkedIntro && capturedChart && moved, "Intro, main chart and actual inward motion were observed");
                Require(FindBar() != null, "Upcoming chart bars remain visible with the accent setting off");
                ValidateVisibleBars(); ValidateChartMarkers(); Capture("03-playing"); captureAt = now;
            }
            // CaptureScreenshot writes at the end of the next rendered frame.
            if (captureAt >= 0 && now - captureAt > .4) Finish(null);
        }
        catch (Exception exception) { Finish(exception); }
    }

    private static void OnJudged(Judgement result)
    {
        if (result.NoteIndex >= 0) Resolved.Add(result.NoteIndex);
    }

    private static Image FindBar()
    {
        foreach (var image in lane.GetComponentsInChildren<Image>())
            if (image.name == "Rhythm bar" && image.color.a > .8f && image.sprite != null) return image;
        return null;
    }

    private static void ValidateVisibleBars()
    {
        var track = lane.transform.Find("Beat line") as RectTransform;
        Require(track != null, "Beat line exists");
        Image target = null;
        int visibleBars = 0;
        foreach (var image in lane.GetComponentsInChildren<Image>(true))
        {
            Require(image.name != "Required key", "Old directional markers are removed");
            if (image.name == "Beat target") target = image;
            if (image.name != "Rhythm bar" || !image.gameObject.activeInHierarchy) continue;
            ValidateGeneratedSprite(image);
            Require(Math.Abs(image.rectTransform.rect.width - 14) < .01f && Math.Abs(image.rectTransform.rect.height - 48) < .01f,
                "Incoming bars are vertical 14 by 48 markers");
            Require(Math.Abs(image.rectTransform.position.y - track.position.y) < .01f, "Incoming bars are centered on the beat line");
            if (image.color.a > .8f) visibleBars++;
        }
        Require(visibleBars > 0, "At least one incoming bar is clearly visible");
        Require(target != null && target.gameObject.activeInHierarchy, "The original seal icon marks the central hit point");
        Require(target.sprite != null && target.sprite.name == "Seal UI Seal" &&
            target.sprite.texture != null && target.sprite.texture.name == "seal-ui-atlas", "The central target uses the original seal UI artwork");
        Require(Math.Abs(target.rectTransform.rect.width - 48) < .01f && Math.Abs(target.rectTransform.rect.height - 48) < .01f,
            "The central seal target is 48 by 48");
        Require(target.preserveAspect, "The central seal target retains its original aspect ratio");
        Require(!target.raycastTarget, "The central seal target does not intercept touch input");
        Require(Math.Abs(target.rectTransform.position.y - track.position.y) < .01f &&
            Math.Abs(target.rectTransform.anchoredPosition.x) < .01f, "The central target sits at the line's hit point");
    }

    private static void ValidateGeneratedSprite(Image image)
    {
        Require(image.sprite != null && image.sprite.name == "Generated rhythm bar" &&
            image.sprite.texture != null && image.sprite.texture.name == "rhythm-bar", "Markers use the generated rhythm-bar resource");
        Require(!image.preserveAspect, "The generated sprite fills its vertical marker rectangle");
        Require(SameRgb(image.color, Color.white), "One neutral sprite is shared without directional tint");
        Require(!image.raycastTarget, "Beat artwork does not intercept touch input");
    }

    private static void ValidateChartMarkers()
    {
        var track = lane.transform.Find("Beat line") as RectTransform;
        Require(track != null, "Beat line exists");
        double horizon = game.SampleRate * 60d / game.beatsPerMinute * 3d;
        int expected = 0;
        for (int i = game.Judge.NextNoteIndex; i < game.Judge.TotalNotes; ++i)
        {
            long delta = (long)game.Judge.Notes[i].Sample - game.InputSample;
            if (delta > horizon) break;
            if (Resolved.Contains(i)) continue;
            var left = lane.transform.Find("Beat pair " + expected + " left") as RectTransform;
            var right = lane.transform.Find("Beat pair " + expected + " right") as RectTransform;
            Require(left != null && right != null && left.gameObject.activeInHierarchy && right.gameObject.activeInHierarchy,
                "Every unresolved chart beat has one complete mirrored pair");
            float distance = Mathf.Clamp01((float)(delta / horizon)) * track.rect.width * .5f;
            Require(Math.Abs(left.anchoredPosition.x + distance) < .05f && Math.Abs(right.anchoredPosition.x - distance) < .05f,
                "Each bar pair follows its judge-note sample, including half beats and opening nods");
            expected++;
        }
        int actualLeft = 0, actualRight = 0;
        for (int i = 0; i < lane.transform.childCount; ++i)
        {
            var child = lane.transform.GetChild(i);
            if (!child.name.StartsWith("Beat pair ") || !child.gameObject.activeInHierarchy) continue;
            if (child.name.EndsWith(" left")) actualLeft++;
            if (child.name.EndsWith(" right")) actualRight++;
        }
        Require(actualLeft == expected && actualRight == expected,
            "Every scored beat appears exactly once per side, without duplicate demonstration markers");
    }

    private static void ValidateSingleControl()
    {
        var hud = UnityEngine.Object.FindAnyObjectByType<SealRhythmHud>();
        Require(hud != null && hud.ControlsRect != null, "Shared rhythm control exists");
        var buttons = hud.ControlsRect.GetComponentsInChildren<Button>(true);
        Require(buttons.Length == 1 && buttons[0].name == "Rhythm action", "Exactly one action button serves all chart actions");
        var button = buttons[0];
        Require(SameRgb(button.image.color, SealUiArt.InputColor(0)), "The shared button uses the neutral mint stage color");
        var trigger = button.GetComponent<EventTrigger>();
        Require(trigger != null && trigger.triggers.Exists(entry => entry.eventID == EventTriggerType.PointerDown),
            "The shared control still responds on touch down");
        int circles = 0;
        foreach (var image in button.GetComponentsInChildren<Image>(true))
        {
            Require(image.name != "Direction", "The shared button has no directional arrow");
            if (image.name != "Rhythm circle") continue;
            Require(image.sprite != null && image.sprite.name == "Rhythm circle ⚪", "The button displays the white circle symbol");
            Require(image.rectTransform.rect.width >= 60 && image.rectTransform.rect.height >= 60, "The white circle remains clearly visible on mobile");
            circles++;
        }
        Require(circles == 1, "The action button contains one white circle");
    }

    private static void CheckInwardTravel()
    {
        var left = lane.transform.Find("Beat pair 0 left") as RectTransform;
        var right = lane.transform.Find("Beat pair 0 right") as RectTransform;
        if (left == null || right == null || !left.gameObject.activeInHierarchy || !right.gameObject.activeInHierarchy) return;
        float distance = Mathf.Abs(left.anchoredPosition.x);
        int sample = game.InputSample, index = game.Judge.NextNoteIndex;
        Require(Math.Abs(left.anchoredPosition.x + right.anchoredPosition.x) < .01f, "Incoming pairs are mirrored around the hit point");
        float elapsed = (sample - previousSample) / (float)game.SampleRate;
        // A pair stays attached to the first unresolved note until it expires.
        // Ignore duplicate Editor ticks, boundary reassignments and bars already at the target.
        if (previousIndex == index && elapsed >= .02f && elapsed < .2f && previousDistance > 30)
        {
            Require(distance < previousDistance - .5f, "A time-linked bar pair travels inward as actual music samples advance");
            moved = true;
            Log("Actual mirrored pair moved inward from " + previousDistance.ToString("F2") + " to " + distance.ToString("F2") + " over " + elapsed.ToString("F3") + " s.");
        }
        if (previousIndex != index || elapsed >= .02f || elapsed < 0)
        {
            previousIndex = index; previousSample = sample; previousDistance = distance;
        }
    }

    private static bool SameRgb(Color a, Color b) => Math.Abs(a.r - b.r) < .002f && Math.Abs(a.g - b.g) < .002f && Math.Abs(a.b - b.b) < .002f;
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Log(string text) { File.AppendAllText(Path.Combine(Folder, "result.txt"), text + "\n"); }
    private static void Capture(string name) { ScreenCapture.CaptureScreenshot(Path.Combine(Folder, name + ".png")); }

    private static void Finish(Exception error)
    {
        running = false;
        if (observedJudge != null) observedJudge.Judged -= OnJudged;
        observedJudge = null;
        Resolved.Clear();
        try
        {
            if (game != null)
            {
                game.ReturnToTitle(); game.SetTimingOffset(savedOffset);
                game.LiveInputEnabled = savedLiveInput;
                if (game.AssistEnabled != savedAssist) game.ToggleAssist();
                PlayerPrefs.Save();
            }
        }
        finally
        {
            // Restore persisted settings even if stopping Play destroyed the runtime object.
            PlayerPrefs.SetFloat("SealRhythm.Offset", savedOffset);
            PlayerPrefs.SetInt("SealRhythm.Assist", savedAssist ? 1 : 0);
            PlayerPrefs.Save();
            Application.runInBackground = savedBackground;
        }
        Log(error == null ? "PASS: generated vertical bars, shared circle control, line alignment, note-sample positions, no duplicate intro markers, actual inward travel, no autoplay, accent-off visibility and preference restoration." : "FAIL: " + error);
        if (error == null) Debug.Log("Seal rhythm bar test PASSED. " + Folder);
        else Debug.LogException(error);
    }
}
