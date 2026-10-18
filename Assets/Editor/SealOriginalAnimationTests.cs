using System;
using System.IO;
using SealRhythm;
using SonicBloom.Koreo;
using UnityEditor;
using UnityEngine;

/// <summary>Checks the extracted native poses and all original action transitions without Play mode.</summary>
public static class SealOriginalAnimationTests
{
    [Serializable] private sealed class Chart
    {
        public int sampleRate;
        public float bpm;
        public Note[] notes;
        public Counts expectedCounts;
    }
    [Serializable] private sealed class Counts { public int inputs, flip, roll, introDemonstrations; }
    [Serializable] private sealed class Note
    {
        public float beat;
        public string action, originalType, animationName;
        public int originalTick, animationIndex, directionFlagBefore, directionFlagAfter;
        public bool phaseBBefore, phaseBAfter;
    }

    [MenuItem("Tools/Seal Rhythm/Validate Original Animation Data")]
    public static void RunAll()
    {
        var json = Resources.Load<TextAsset>("Art/Level_cellanim/OriginalGoma/goma");
        Require(json != null, "Extracted goma animation JSON is present");
        var data = JsonUtility.FromJson<SealOriginalAnimation.AnimationData>(json.text);
        ValidateData(data);
        ValidateFrameBoundaries(data);
        ValidateTransforms(data);
        ValidateWholeChart(data);
        ValidateErrorRecovery(data);
        Debug.Log("Seal original animation: 78 poses, 30 motions, discrete frame boundaries, signed transforms, " +
            "230 original action/index/sample mappings, separate intro timing and three input-error recovery paths verified.");
    }

    private static void ValidateData(SealOriginalAnimation.AnimationData data)
    {
        Require(data.sprites.Length == 78 && data.animations.Length == 30, "Original pose/motion counts");
        foreach (var sprite in data.sprites)
        foreach (var part in sprite.parts)
        {
            Require(part.texture.Length == 4 && part.position.Length == 2 && part.scale.Length == 2 && part.flip.Length == 2,
                "Complete part transforms");
            var rect = part.texture;
            Require(rect[0] >= 0 && rect[1] >= 0 && rect[2] > 0 && rect[3] > 0 &&
                rect[0] + rect[2] <= 512 && rect[1] + rect[3] <= 512, "Atlas rectangle bounds");
            Require(part.opacity >= 0 && part.opacity <= 255, "Part opacity bounds");
        }
        foreach (var motion in data.animations)
        {
            int duration = 0;
            Require(motion.frames.Length > 0, "Every motion has keys");
            foreach (var key in motion.frames)
            {
                Require(key.sprite >= 0 && key.sprite < data.sprites.Length && key.duration > 0, "Valid pose/key reference");
                Require(key.position.Length == 2 && key.scale.Length == 2, "Original key translation/scale retained");
                duration += key.duration;
            }
            Require(duration == motion.totalDuration, "Native duration equals sum of discrete keys");
        }
    }

    private static void ValidateFrameBoundaries(SealOriginalAnimation.AnimationData data)
    {
        var press = data.animations[1];
        Require(SealOriginalAnimation.FrameAt(press, 0).sprite == 3, "Press starts compressed");
        Require(SealOriginalAnimation.FrameAt(press, 4.999f).sprite == 3, "Native frame key holds before boundary");
        Require(SealOriginalAnimation.FrameAt(press, 5).sprite == 2, "Next pose begins exactly on frame boundary");
        Require(SealOriginalAnimation.FrameAt(press, 9).sprite == 1, "Press returns to upright neutral");
        Require(SealOriginalAnimation.FrameAt(press, 10000).sprite == 1, "Body freezes at final key");
        Require(SealOriginalAnimation.FrameAt(press, 20, true).sprite == 3, "Explicit looping wraps");
        Require(data.animations[8].frames[0].position[0] == -104 &&
            data.animations[9].frames[0].position[0] == 104, "Roll key translations retain signed native values");
    }

    private static void ValidateTransforms(SealOriginalAnimation.AnimationData data)
    {
        var uprightAnchor = SealOriginalAnimation.PartCenter(data.sprites[1].parts[3]);
        Near(0, uprightAnchor.x, "Upright face anchor x");
        Near(.34f, uprightAnchor.y, "Source down-axis converts to Unity up-axis");
        var upsideDown = data.sprites[31].parts[3];
        Require(upsideDown.scale[1] < 0, "Upside-down face uses signed anchor scale");
        var testObject = new GameObject("Original seal transform validation");
        try
        {
            var player = SealOriginalAnimation.Attach(testObject.AddComponent<Animator>());
            Require(player != null && player.IsReady, "Original renderer loads extracted JSON and atlas");
            player.PlayAnimation(12);
            var anchor = testObject.transform.Find("Original Wii seal/Face attachment");
            Require(anchor != null && anchor.gameObject.activeSelf, "Transparent anchor keeps linked face visible");
            Near(upsideDown.scale[1], anchor.localScale.y, "Linked face retains upside-down scale");
            Near(SealOriginalAnimation.PartCenter(upsideDown).y, anchor.localPosition.y, "Face anchor center includes signed height");
            player.PlayAnimation(8);
            Near(-1.04f, testObject.transform.Find("Original Wii seal").localPosition.x, "Original roll offset retained");
            player.ResetPose();
            player.PlayAction(SealAction.Right);
            var bodyPart = testObject.transform.Find("Original Wii seal/Part 2");
            var centerBefore = bodyPart.position;
            player.PlayAction(SealAction.Roll);
            Near(centerBefore.x, bodyPart.position.x, "Fixed-stage roll begins at previous body center x");
            Near(centerBefore.y, bodyPart.position.y, "Fixed-stage roll begins at previous body center y");
            player.AdvanceFrames(9);
            Near(0, testObject.transform.Find("Original Wii seal").localPosition.x, "Stage adaptation settles with native translation keys");
        }
        finally { UnityEngine.Object.DestroyImmediate(testObject); }
    }

    private static void ValidateWholeChart(SealOriginalAnimation.AnimationData data)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "Tools", "SealChart.json");
        var chart = JsonUtility.FromJson<Chart>(File.ReadAllText(path));
        Require(chart.notes.Length == 230 && chart.expectedCounts.inputs == 230 && chart.expectedCounts.roll == 62 &&
            chart.expectedCounts.flip == 168 && chart.expectedCounts.introDemonstrations == 5, "Original input counts");
        var song = Resources.Load<Koreography>("Art/Level_cellanim/Kore/haibao");
        Require(song != null && song.SourceClip.frequency == chart.sampleRate, "Chart and clip sample rates match");
        var events = song.GetTrackByID("haibaoEventID").GetAllEvents();
        Require(events.Count == chart.notes.Length, "All original player inputs imported once");
        var intro = song.GetTrackByID("haibaoIntroEventID").GetAllEvents();
        int nods = 0, preparations = 0;
        foreach (var cue in intro)
        {
            if (cue.GetTextValue() == "diantou") nods++;
            else if (cue.GetTextValue() == "readyRight") preparations++;
            else Require(false, "Unexpected automatic intro payload");
        }
        Require(intro.Count == 6 && nods == 5 && preparations == 1, "Five nods and one preparation on a separate track");

        var testObject = new GameObject("Original seal state validation");
        try
        {
            var player = SealOriginalAnimation.Attach(testObject.AddComponent<Animator>());
            player.PlayReady(false);
            Require(player.CurrentMotion == 3 && !player.IsUpsideDown && !player.NextFlipToLeft,
                "Preparation plays ReadyForR without advancing phase/direction");
            int rolls = 0;
            for (int i = 0; i < chart.notes.Length; ++i)
            {
                var note = chart.notes[i];
                Require(RhythmGame.TryGetAction(note.action, out var action) && action != SealAction.Nod,
                    "The original main track contains only flip/roll inputs at " + i);
                Require(events[i].GetTextValue() == note.action, "Koreography action matches source at " + i);
                int sample = (int)Math.Round(note.beat * 60d / chart.bpm * chart.sampleRate, MidpointRounding.AwayFromZero);
                Require(events[i].StartSample == sample, "Sample-exact imported original beat at " + i);
                Require(player.IsUpsideDown == note.phaseBBefore && player.NextFlipToLeft == (note.directionFlagBefore != 0),
                    "Original state before action at " + i);
                player.PlayAction(action);
                Require(note.animationIndex > 0 && player.CurrentMotion == note.animationIndex,
                    "Actual state machine matches original animation index at " + i);
                Require(data.animations[player.CurrentMotion].name == note.animationName,
                    "Original named motion matches at " + i);
                Require(player.IsUpsideDown == note.phaseBAfter && player.NextFlipToLeft == (note.directionFlagAfter != 0),
                    "Original state after action at " + i);
                if (action == SealAction.Roll) rolls++;
            }
            Require(rolls == 62 && !player.IsUpsideDown && !player.NextFlipToLeft, "Complete chart final original state");
            player.ResetPose();
            Require(!player.IsUpsideDown && !player.NextFlipToLeft, "Restart restores original upright/right-first state");
            player.PlayAction(SealAction.Right);
            Require(player.CurrentMotion == 5, "First flip is A_TurnToR after reset");
        }
        finally { UnityEngine.Object.DestroyImmediate(testObject); }
    }

    private static void ValidateErrorRecovery(SealOriginalAnimation.AnimationData data)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "Tools", "SealChart.json");
        var chart = JsonUtility.FromJson<Chart>(File.ReadAllText(path));
        int firstRoll = Array.FindIndex(chart.notes, note => note.action == "roll");
        Require(firstRoll > 0 && firstRoll + 1 < chart.notes.Length, "Recovery scenarios have original reference notes");
        CheckRecovery(chart, data, "missed flip", 0, 1, false);
        CheckRecovery(chart, data, "missed roll", firstRoll, firstRoll + 1, false);
        CheckRecovery(chart, data, "extra roll", -1, firstRoll, true);
    }

    private static void CheckRecovery(Chart chart, SealOriginalAnimation.AnimationData data, string scenario,
        int skippedNote, int correctNote, bool extraRoll)
    {
        var testObject = new GameObject("Original seal recovery: " + scenario);
        try
        {
            var player = SealOriginalAnimation.Attach(testObject.AddComponent<Animator>());
            for (int i = 0; i < correctNote; ++i)
                if (i != skippedNote && RhythmGame.TryGetAction(chart.notes[i].action, out var priorAction))
                    player.PlayAction(priorAction);
            if (extraRoll) player.PlayAction(SealAction.Roll);
            var expected = chart.notes[correctNote];
            Require(player.IsUpsideDown != expected.phaseBBefore ||
                player.NextFlipToLeft != (expected.directionFlagBefore != 0), scenario + " first disrupts native state");
            Require(RhythmGame.TryGetAction(expected.action, out var action), "Recovery action exists");
            // Exercise the same chart-state entry point used by the controller after a hit.
            // Expected flags and animation index come directly from the original PPC extract.
            player.PlayChartAction(action, expected.phaseBBefore, expected.directionFlagBefore != 0);
            Require(player.CurrentMotion == expected.animationIndex &&
                data.animations[player.CurrentMotion].name == expected.animationName, scenario + " next hit restores original motion");
            Require(player.IsUpsideDown == expected.phaseBAfter &&
                player.NextFlipToLeft == (expected.directionFlagAfter != 0), scenario + " restores original resulting state");
        }
        finally { UnityEngine.Object.DestroyImmediate(testObject); }
    }

    private static void Near(float expected, float actual, string message)
    {
        Require(Mathf.Abs(expected - actual) < .0001f, message + ": expected " + expected + ", got " + actual);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
