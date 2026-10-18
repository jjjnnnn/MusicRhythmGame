using System;
using System.Collections.Generic;
using SealRhythm;
using UnityEditor;
using UnityEngine;

/// <summary>Dependency-free editor checks. Also callable with -executeMethod SealRhythmJudgeTests.RunAll.</summary>
public static class SealRhythmJudgeTests
{
    [MenuItem("Tools/Seal Rhythm/Run Judge Tests")]
    public static void RunAll()
    {
        Action[] tests =
        {
            InclusiveWindowsAndSignedOffsets,
            FractionalSampleWindows,
            EmptyChartAndStrayBounds,
            MissingNotesAndFrameJumps,
            WrongActionConsumesNote,
            RepeatedPressCannotScoreTwice,
            NearestNoteAndStableOrder,
            ScoreComboAndStrayPenalty,
            ResetRestoresAllState,
            InputValidationAndSampleOverflow,
            PhysicalInputsChooseTheChartAction,
            DownCannotRollAndRollDirectionMatters,
            PhysicalInputUsesNearestBeatAndTimingWindow
        };
        foreach (Action test in tests)
        {
            try { test(); }
            catch (Exception exception)
            {
                throw new InvalidOperationException("Seal rhythm check failed: " + test.Method.Name, exception);
            }
        }
        Debug.Log("SealRhythmJudge: " + tests.Length + " test groups passed.");
    }

    private static SealRhythmJudge Single(int sample = 1000) =>
        new SealRhythmJudge(new[] { new RhythmNote(sample, SealAction.Nod) }, 1000, 40, 100);

    private static void InclusiveWindowsAndSignedOffsets()
    {
        foreach (int offset in new[] { -100, -41, -40, 0, 40, 41, 100 })
        {
            var judge = Single();
            Judgement result = judge.Submit(SealAction.Nod, 1000 + offset);
            Equal(Math.Abs(offset) <= 40 ? HitGrade.Perfect : HitGrade.Good, result.Grade);
            Near(offset, result.OffsetMilliseconds);
            Equal(1, judge.ResolvedCount);
        }
        var early = Single();
        Equal(HitGrade.Stray, early.Submit(SealAction.Nod, 899).Grade);
        Equal(0, early.ResolvedCount);
        early.Advance(1100);
        Equal(0, early.ResolvedCount);
        early.Advance(1101);
        Equal(1, early.MissCount);
    }

    private static void FractionalSampleWindows()
    {
        var judge = new SealRhythmJudge(new[] { new RhythmNote(10000, SealAction.Left) }, 44100, 40, 90);
        Equal(HitGrade.Good, judge.Submit(SealAction.Left, 10000 - 3969).Grade);
        Near(-90, -3969 * 1000d / 44100);
        judge.Reset();
        Equal(HitGrade.Perfect, judge.Submit(SealAction.Left, 10000 + 1764).Grade);
        judge.Reset();
        Equal(HitGrade.Good, judge.Submit(SealAction.Left, 10000 + 1765).Grade);
    }

    private static void EmptyChartAndStrayBounds()
    {
        var judge = new SealRhythmJudge(Array.Empty<RhythmNote>(), 48000, 0, 100);
        judge.Advance(int.MaxValue);
        for (int i = 0; i < 2000; i++)
        {
            Judgement result = judge.Submit(SealAction.Right, i);
            Equal(-1, result.NoteIndex);
            Equal(HitGrade.Stray, result.Grade);
        }
        Equal(0, judge.TotalNotes);
        Equal(0, judge.NextNoteIndex);
        Equal(2000, judge.StrayCount);
        Equal(0, judge.Score);
        Near(0, judge.Accuracy);
    }

    private static void MissingNotesAndFrameJumps()
    {
        var judge = new SealRhythmJudge(new[]
        {
            new RhythmNote(1000, SealAction.Nod), new RhythmNote(2000, SealAction.Left),
            new RhythmNote(3000, SealAction.Right), new RhythmNote(4000, SealAction.Nod)
        }, 1000, 40, 100);
        var events = new List<Judgement>();
        judge.Judged += events.Add;
        judge.Submit(SealAction.Nod, 1000);
        judge.Advance(3101);
        Equal(2, judge.MissCount);
        Equal(3, judge.ResolvedCount);
        Equal(3, judge.NextNoteIndex);
        Equal(0, judge.Combo);
        Equal(3, events.Count);
        Equal(1, events[1].NoteIndex);
        Equal(2, events[2].NoteIndex);
        judge.Advance(10000);
        judge.Advance(20000);
        Equal(3, judge.MissCount);
        Equal(4, events.Count);
    }

    private static void WrongActionConsumesNote()
    {
        var judge = Single();
        Judgement wrong = judge.Submit(SealAction.Left, 1000);
        Equal(HitGrade.Miss, wrong.Grade);
        Equal(SealAction.Nod, wrong.Action);
        Equal(1, judge.MissCount);
        Equal(1, judge.ResolvedCount);
        Equal(HitGrade.Stray, judge.Submit(SealAction.Nod, 1000).Grade);
        Equal(0, judge.Score);
    }

    private static void RepeatedPressCannotScoreTwice()
    {
        var judge = Single();
        judge.Submit(SealAction.Nod, 1000);
        Equal(1000000, judge.Score);
        for (int i = 0; i < 5000; i++) judge.Submit(SealAction.Nod, 1000);
        Equal(1, judge.PerfectCount);
        Equal(1, judge.ResolvedCount);
        Equal(1, judge.MaxCombo);
        Equal(0, judge.Combo);
        True(judge.Score >= 0 && judge.Score <= 1000000);
        True(judge.Accuracy >= 0 && judge.Accuracy <= 1);
    }

    private static void NearestNoteAndStableOrder()
    {
        var judge = new SealRhythmJudge(new[]
        {
            new RhythmNote(1080, SealAction.Right), new RhythmNote(1000, SealAction.Nod),
            new RhythmNote(1080, SealAction.Left)
        }, 1000, 40, 100);
        Equal(SealAction.Nod, judge.Notes[0].Action);
        Equal(SealAction.Right, judge.Notes[1].Action);
        Equal(SealAction.Left, judge.Notes[2].Action);
        Equal(1, judge.Submit(SealAction.Right, 1070).NoteIndex);
        Equal(0, judge.NextNoteIndex);
        Equal(0, judge.Submit(SealAction.Nod, 1000).NoteIndex);
        Equal(2, judge.NextNoteIndex);
        Equal(2, judge.Submit(SealAction.Left, 1080).NoteIndex);
        Equal(3, judge.NextNoteIndex);

        judge = new SealRhythmJudge(new[]
        {
            new RhythmNote(1000, SealAction.Nod), new RhythmNote(1080, SealAction.Left)
        }, 1000, 40, 100);
        Equal(0, judge.Submit(SealAction.Nod, 1040).NoteIndex);
    }

    private static void ScoreComboAndStrayPenalty()
    {
        var judge = new SealRhythmJudge(new[]
        {
            new RhythmNote(1000, SealAction.Nod), new RhythmNote(2000, SealAction.Left)
        }, 1000, 40, 100);
        judge.Submit(SealAction.Nod, 1000);
        Equal(500000, judge.Score);
        judge.Submit(SealAction.Left, 2060);
        Equal(1, judge.GoodCount);
        Equal(2, judge.Combo);
        Equal(2, judge.MaxCombo);
        Equal(825000, judge.Score);
        Near(0.825, judge.Accuracy);
        judge.Submit(SealAction.Nod, 2200);
        Equal(0, judge.Combo);
        Equal(2, judge.MaxCombo);
        Near(1.65 / 2.25, judge.Accuracy);
        True(judge.Score < 825000);
    }

    private static void ResetRestoresAllState()
    {
        var judge = Single();
        int eventCount = 0;
        judge.Judged += _ => eventCount++;
        judge.Submit(SealAction.Nod, 1000);
        judge.Submit(SealAction.Nod, 1000);
        judge.Reset();
        Equal(0, judge.ResolvedCount);
        Equal(0, judge.PerfectCount);
        Equal(0, judge.GoodCount);
        Equal(0, judge.MissCount);
        Equal(0, judge.StrayCount);
        Equal(0, judge.Combo);
        Equal(0, judge.MaxCombo);
        Equal(0, judge.Score);
        Equal(0, judge.NextNoteIndex);
        Equal(2, eventCount);
        Equal(HitGrade.Perfect, judge.Submit(SealAction.Nod, 1000).Grade);
        Equal(3, eventCount);
    }

    private static void InputValidationAndSampleOverflow()
    {
        Throws<ArgumentNullException>(() => new SealRhythmJudge(null, 1000, 40, 100));
        Throws<ArgumentOutOfRangeException>(() => new SealRhythmJudge(Array.Empty<RhythmNote>(), 0, 40, 100));
        Throws<ArgumentOutOfRangeException>(() => new SealRhythmJudge(Array.Empty<RhythmNote>(), 1000, -1, 100));
        Throws<ArgumentOutOfRangeException>(() => new SealRhythmJudge(Array.Empty<RhythmNote>(), 1000, 40, 39));
        Throws<ArgumentOutOfRangeException>(() => new SealRhythmJudge(Array.Empty<RhythmNote>(), 1000, double.NaN, 100));
        Throws<ArgumentOutOfRangeException>(() => new SealRhythmJudge(Array.Empty<RhythmNote>(), 1000, 40, double.PositiveInfinity));
        var judge = Single(int.MinValue);
        judge.Advance(int.MaxValue);
        Equal(1, judge.MissCount);
        judge = Single(int.MaxValue);
        Equal(HitGrade.Stray, judge.Submit(SealAction.Nod, int.MinValue).Grade);
        Throws<ArgumentOutOfRangeException>(() => judge.Submit((SealAction)99, 0));
    }

    private static void PhysicalInputsChooseTheChartAction()
    {
        var judge = new SealRhythmJudge(new[]
        {
            new RhythmNote(1000, SealAction.Nod),
            new RhythmNote(2000, SealAction.Left),
            new RhythmNote(3000, SealAction.Right),
            new RhythmNote(4000, SealAction.Roll, false),
            new RhythmNote(5000, SealAction.Roll, true)
        }, 1000, 40, 100);
        var inputs = new[] { SealInput.Nod, SealInput.Left, SealInput.Right, SealInput.Right, SealInput.Left };
        var actions = new[] { SealAction.Nod, SealAction.Left, SealAction.Right, SealAction.Roll, SealAction.Roll };
        for (int i = 0; i < inputs.Length; ++i)
        {
            Equal(inputs[i], judge.Notes[i].Input);
            Judgement result = judge.SubmitInput(inputs[i], (i + 1) * 1000);
            Equal(HitGrade.Perfect, result.Grade);
            Equal(actions[i], result.Action);
            Equal(i, result.NoteIndex);
        }
        Equal(5, judge.PerfectCount);
        Equal(1000000, judge.Score);
    }

    private static void DownCannotRollAndRollDirectionMatters()
    {
        foreach (bool rollToLeft in new[] { false, true })
        {
            var judge = new SealRhythmJudge(new[]
            {
                new RhythmNote(1000, SealAction.Roll, rollToLeft)
            }, 1000, 40, 100);
            SealInput correct = rollToLeft ? SealInput.Left : SealInput.Right;
            SealInput wrong = rollToLeft ? SealInput.Right : SealInput.Left;
            Judgement down = judge.SubmitInput(SealInput.Nod, 1000);
            Equal(HitGrade.Miss, down.Grade);
            Equal(SealAction.Roll, down.Action);
            Equal(1, judge.MissCount);
            Equal(0, judge.PerfectCount);
            // A wrong press consumes the beat; the correct direction cannot rescue it.
            Equal(HitGrade.Stray, judge.SubmitInput(correct, 1000).Grade);
            judge.Reset();
            Equal(HitGrade.Miss, judge.SubmitInput(wrong, 1000).Grade);
            Equal(0, judge.Score);
            judge.Reset();
            Equal(HitGrade.Perfect, judge.SubmitInput(correct, 1000).Grade);
        }
        // A directional press remains a wrong action at a nod beat.
        Equal(HitGrade.Miss, Single().SubmitInput(SealInput.Left, 1000).Grade);
        Equal(HitGrade.Perfect, Single().SubmitInput(SealInput.Nod, 1000).Grade);
    }

    private static void PhysicalInputUsesNearestBeatAndTimingWindow()
    {
        var judge = new SealRhythmJudge(new[]
        {
            new RhythmNote(1000, SealAction.Left),
            new RhythmNote(1080, SealAction.Roll, false)
        }, 1000, 40, 100);
        // Select by time before checking the key; do not skip a nearby roll to find a matching flip.
        Judgement nearest = judge.SubmitInput(SealInput.Left, 1070);
        Equal(1, nearest.NoteIndex);
        Equal(SealAction.Roll, nearest.Action);
        Equal(HitGrade.Miss, nearest.Grade);
        Equal(0, judge.NextNoteIndex);

        judge = new SealRhythmJudge(new[] { new RhythmNote(1000, SealAction.Roll, false) }, 1000, 40, 100);
        Judgement early = judge.SubmitInput(SealInput.Right, 899);
        Equal(HitGrade.Stray, early.Grade);
        Equal(SealAction.Right, early.Action);
        Equal(-1, early.NoteIndex);
        Equal(0, judge.ResolvedCount);
        Equal(HitGrade.Good, judge.SubmitInput(SealInput.Right, 900).Grade);
        Equal(HitGrade.Stray, judge.SubmitInput(SealInput.Right, 900).Grade);
        Equal(1, judge.ResolvedCount);
        judge.Reset();
        Equal(HitGrade.Good, judge.SubmitInput(SealInput.Right, 1100).Grade);
        judge.Reset();
        Equal(HitGrade.Stray, judge.SubmitInput(SealInput.Right, 1101).Grade);
        Equal(1, judge.MissCount);
        Throws<ArgumentOutOfRangeException>(() => judge.SubmitInput((SealInput)3, 1000));
        Throws<ArgumentOutOfRangeException>(() => judge.SubmitInput((SealInput)(-1), 1000));
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception("Expected " + expected + ", got " + actual + ".");
    }

    private static void Near(double expected, double actual)
    {
        if (Math.Abs(expected - actual) > 0.0000001d)
            throw new Exception("Expected " + expected + ", got " + actual + ".");
    }

    private static void True(bool condition)
    {
        if (!condition) throw new Exception("Assertion was false.");
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name + ".");
    }
}
