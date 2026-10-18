using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Exercises the visible beat guide against the real music clock, including a saved accent setting of off.</summary>
[InitializeOnLoad]
public static class SealBeatLaneVisibilityTests
{
    private static RhythmGame game;
    private static SealBeatLane lane;
    private static bool running, savedAssist, savedBackground, savedLiveInput, checkedIntro, sawLeft, sawRight, moved, capturedDirections;
    private static float savedOffset, previousDistance, previousSongTime;
    private static int previousIndex = -1;
    private static Sprite previousSprite;
    private static double startedAt, captureAt = -1;
    private static readonly string Folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "SealValidation", "ColoredArrows");

    static SealBeatLaneVisibilityTests() { EditorApplication.update += Tick; }

    [MenuItem("Tools/Seal Rhythm/Run Rolling Arrow Test")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode before running the rolling arrow test.");
        if (running) throw new InvalidOperationException("The rolling arrow test is already running.");
        game = UnityEngine.Object.FindAnyObjectByType<RhythmGame>();
        lane = UnityEngine.Object.FindAnyObjectByType<SealBeatLane>();
        if (game == null || game.Judge == null || lane == null)
            throw new InvalidOperationException("Open GamePlay and wait for the game and beat guide to initialize.");
        Directory.CreateDirectory(Folder);
        savedAssist = game.AssistEnabled; savedOffset = game.TimingOffsetMs;
        savedLiveInput = game.LiveInputEnabled; game.LiveInputEnabled = false;
        savedBackground = Application.runInBackground;
        checkedIntro = sawLeft = sawRight = moved = capturedDirections = false;
        previousIndex = -1; previousSprite = null; captureAt = -1;
        startedAt = EditorApplication.timeSinceStartup; running = true;
        File.WriteAllText(Path.Combine(Folder, "result.txt"), "RUNNING: real music, saved accent setting off, visible colored arrows and inward travel.\n");
        try
        {
            game.ReturnToTitle(); game.SetTimingOffset(0);
            if (game.AssistEnabled) game.ToggleAssist();
            Application.runInBackground = true;
            game.StartGame(false);
        }
        catch (Exception exception) { Finish(exception); }
    }

    private static void Tick()
    {
        if (!running) return;
        try
        {
            if (!EditorApplication.isPlaying || game == null || lane == null)
                throw new Exception("Play mode or the game ended during the rolling arrow test.");
            double now = EditorApplication.timeSinceStartup;
            if (now - startedAt > 45) throw new Exception("Timed out while checking real music playback.");
            if (game.State == SealGameState.Error) throw new Exception(game.StatusMessage);
            if (game.State == SealGameState.Paused) game.TogglePause();
            if (game.State != SealGameState.Playing) return;
            Require(!game.AssistEnabled, "Core note arrows remain visible with the accent setting off");

            if (!checkedIntro && game.SongTime > 3.2f)
            {
                Require(FindArrow("Down") != null, "Scored opening down arrows are visible before their beat");
                ValidateNoDuplicateIntroMarkers();
                var player = game.characterAnimator.GetComponent<SealOriginalAnimation>();
                Require(player.CurrentMotion == 1 && player.CurrentMotionFrame >= player.CurrentMotionDuration,
                    "An opening down beat does not automatically nod the player without an input");
                ValidateVisibleArrows(); ValidateControlColors();
                Capture("01-intro"); checkedIntro = true;
                Log("Down: 44 px, mint tint, centered on the beat line; footer controls use matching colors.");
            }
            if (game.SongTime > 7.5f)
            {
                sawLeft |= FindArrow("Left") != null;
                sawRight |= FindArrow("Right") != null;
                ValidateVisibleArrows();
                if (sawLeft && sawRight && !capturedDirections)
                {
                    Capture("02-directions"); capturedDirections = true;
                    Log("Left and right arrows observed in coral and blue during chart playback.");
                }
            }
            if (!moved && game.SongTime > 9f) CheckInwardTravel();
            if (game.SongTime > 12f && captureAt < 0)
            {
                Require(checkedIntro && sawLeft && sawRight && moved, "All three inputs and actual inward motion were observed");
                Require(FindArrow(null) != null, "Upcoming chart arrows remain visible with the accent setting off");
                ValidateVisibleArrows(); Capture("03-playing"); captureAt = now;
            }
            // CaptureScreenshot writes at the end of the next rendered frame.
            if (captureAt >= 0 && now - captureAt > .4) Finish(null);
        }
        catch (Exception exception) { Finish(exception); }
    }

    private static Image FindArrow(string direction)
    {
        foreach (var image in lane.GetComponentsInChildren<Image>())
            if (image.name == "Required key" && image.color.a > .8f && image.sprite != null &&
                (direction == null || image.sprite.name == "Seal UI " + direction)) return image;
        return null;
    }

    private static void ValidateVisibleArrows()
    {
        var track = lane.transform.Find("Beat line") as RectTransform;
        Require(track != null, "Beat line exists");
        foreach (var image in lane.GetComponentsInChildren<Image>())
        {
            if (image.name != "Required key" || image.color.a <= .8f) continue;
            string sprite = image.sprite != null ? image.sprite.name : "";
            int input = sprite == "Seal UI Down" ? 0 : sprite == "Seal UI Left" ? 1 : sprite == "Seal UI Right" ? 2 : -1;
            Require(input >= 0, "Each incoming marker shows its required key sprite");
            Require(Math.Abs(image.rectTransform.rect.width - 44) < .01f && Math.Abs(image.rectTransform.rect.height - 44) < .01f,
                "Incoming arrows are enlarged to 44 by 44");
            Require(Math.Abs(image.rectTransform.position.y - track.position.y) < .01f, "Incoming arrows are centered on the line");
            Require(SameRgb(image.color, SealUiArt.InputColor(input)), "Incoming arrow color matches its physical input");
        }
    }

    private static void ValidateNoDuplicateIntroMarkers()
    {
        int expected = 0, actual = 0;
        double horizon = game.SampleRate * 60d / game.beatsPerMinute * 3d;
        for (int i = game.Judge.NextNoteIndex; i < game.Judge.TotalNotes; ++i)
        {
            if ((long)game.Judge.Notes[i].Sample - game.InputSample > horizon) break;
            expected++;
        }
        for (int i = 0; i < lane.transform.childCount; ++i)
        {
            var child = lane.transform.GetChild(i);
            if (child.name.StartsWith("Beat pair ") && child.name.EndsWith(" left") && child.gameObject.activeInHierarchy)
                actual++;
        }
        Require(actual == expected, "Each scored opening beat appears once in the lane, without a duplicate demonstration arrow");
    }

    private static void ValidateControlColors()
    {
        var buttons = UnityEngine.Object.FindObjectsByType<Button>();
        for (int input = 0; input < 3; input++)
        {
            bool found = false;
            foreach (var button in buttons)
                if (button.name == "Action " + input)
                {
                    Require(SameRgb(button.image.color, SealUiArt.InputColor(input)), "Footer key matches incoming note color for input " + input);
                    found = true; break;
                }
            Require(found, "Footer input button exists: " + input);
        }
        Require(!SameRgb(SealUiArt.InputColor(0), SealUiArt.InputColor(1)) &&
            !SameRgb(SealUiArt.InputColor(0), SealUiArt.InputColor(2)) &&
            !SameRgb(SealUiArt.InputColor(1), SealUiArt.InputColor(2)), "All three keys have distinct colors");
    }

    private static void CheckInwardTravel()
    {
        var left = lane.transform.Find("Beat pair 0 left") as RectTransform;
        var right = lane.transform.Find("Beat pair 0 right") as RectTransform;
        if (left == null || right == null || !left.gameObject.activeInHierarchy || !right.gameObject.activeInHierarchy) return;
        var image = left.GetComponentInChildren<Image>();
        float distance = Mathf.Abs(left.anchoredPosition.x), songTime = game.SongTime;
        int index = game.Judge.NextNoteIndex;
        Require(Math.Abs(left.anchoredPosition.x + right.anchoredPosition.x) < .01f, "Incoming pairs are mirrored around the hit point");
        float elapsed = songTime - previousSongTime;
        // The first pair stays attached to the same unresolved note until the judge advances.
        // Ignore duplicate Editor ticks, boundary reassignments, and arrows already under the seal.
        if (previousIndex == index && previousSprite == image.sprite && elapsed >= .02f && elapsed < .2f && previousDistance > 30)
        {
            Require(distance < previousDistance - .5f, "A time-linked arrow pair travels inward as music samples advance");
            moved = true;
            Log("Actual mirrored pair moved inward from " + previousDistance.ToString("F2") + " to " + distance.ToString("F2") + " over " + elapsed.ToString("F3") + " s.");
        }
        // Retain a sample long enough to span at least one rendered audio frame.
        if (previousIndex != index || previousSprite != image.sprite || elapsed >= .02f || elapsed < 0)
        {
            previousIndex = index; previousSprite = image.sprite;
            previousDistance = distance; previousSongTime = songTime;
        }
    }

    private static bool SameRgb(Color a, Color b) => Math.Abs(a.r - b.r) < .002f && Math.Abs(a.g - b.g) < .002f && Math.Abs(a.b - b.b) < .002f;
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Log(string text) { File.AppendAllText(Path.Combine(Folder, "result.txt"), text + "\n"); }
    private static void Capture(string name) { ScreenCapture.CaptureScreenshot(Path.Combine(Folder, name + ".png")); }

    private static void Finish(Exception error)
    {
        running = false;
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
            // These still restore if Play mode was stopped and the runtime object was destroyed.
            PlayerPrefs.SetFloat("SealRhythm.Offset", savedOffset);
            PlayerPrefs.SetInt("SealRhythm.Assist", savedAssist ? 1 : 0);
            PlayerPrefs.Save();
            Application.runInBackground = savedBackground;
        }
        Log(error == null ? "PASS: all three colored input arrows, footer color parity, line alignment, size, inward travel, visibility with accent off and preference restoration." : "FAIL: " + error);
        if (error == null) Debug.Log("Seal rolling arrow test PASSED. " + Folder);
        else Debug.LogException(error);
    }
}
