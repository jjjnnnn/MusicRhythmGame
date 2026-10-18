using System;
using System.Collections.Generic;

namespace SealRhythm
{
    public enum SealAction { Nod, Left, Right }
    public enum HitGrade { Perfect, Good, Miss, Stray }

    public readonly struct RhythmNote
    {
        public int Sample { get; }
        public SealAction Action { get; }

        public RhythmNote(int sample, SealAction action)
        {
            Sample = sample;
            Action = action;
        }
    }

    public readonly struct Judgement
    {
        /// <summary>Index in the judge's sorted Notes; -1 for a stray input.</summary>
        public int NoteIndex { get; }
        public HitGrade Grade { get; }
        /// <summary>Negative means early, positive means late; zero for stray inputs.</summary>
        public double OffsetMilliseconds { get; }
        /// <summary>The required note action, or the submitted action for a stray.</summary>
        public SealAction Action { get; }

        public Judgement(int noteIndex, HitGrade grade, double offsetMilliseconds, SealAction action)
        {
            NoteIndex = noteIndex;
            Grade = grade;
            OffsetMilliseconds = offsetMilliseconds;
            Action = action;
        }
    }

    /// <summary>
    /// Deterministic, sample-clock rhythm judgement, with no Unity or audio middleware dependencies.
    /// Feed the same audio sample clock to Advance and Submit. Backward seeks do not restore notes;
    /// call Reset before restarting or seeking. Window endpoints are inclusive.
    /// </summary>
    public sealed class SealRhythmJudge
    {
        public const int MaximumScore = 1000000;
        public const double GoodWeight = 0.65;
        public const double StrayPenalty = 0.25;

        private readonly RhythmNote[] notes;
        private readonly bool[] resolved;
        private readonly int sampleRate;
        private readonly double perfectWindowSamples;
        private readonly double goodWindowSamples;
        private int nextNoteIndex;

        public IReadOnlyList<RhythmNote> Notes { get; }
        public int TotalNotes => notes.Length;
        public int ResolvedCount { get; private set; }
        public int PerfectCount { get; private set; }
        public int GoodCount { get; private set; }
        public int MissCount { get; private set; }
        public int StrayCount { get; private set; }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }

        /// <summary>First unresolved note; equals TotalNotes when the chart is complete.</summary>
        public int NextNoteIndex => nextNoteIndex;

        /// <summary>
        /// Weighted hits divided by the whole chart plus 0.25 note per stray input.
        /// Pending notes therefore do not count as hits. An empty chart has zero accuracy.
        /// </summary>
        public double Accuracy => TotalNotes == 0
            ? 0d
            : (PerfectCount + GoodCount * GoodWeight) / (TotalNotes + StrayCount * StrayPenalty);

        public int Score => (int)Math.Round(Accuracy * MaximumScore, MidpointRounding.AwayFromZero);

        public event Action<Judgement> Judged;

        public SealRhythmJudge(IEnumerable<RhythmNote> notes, int sampleRate, double perfectMs, double goodMs)
        {
            if (notes == null) throw new ArgumentNullException(nameof(notes));
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (double.IsNaN(perfectMs) || double.IsInfinity(perfectMs) || perfectMs < 0d)
                throw new ArgumentOutOfRangeException(nameof(perfectMs));
            if (double.IsNaN(goodMs) || double.IsInfinity(goodMs) || goodMs < perfectMs)
                throw new ArgumentOutOfRangeException(nameof(goodMs));

            // List.Sort is not stable. Preserve the source order of simultaneous notes explicitly.
            var indexedNotes = new List<KeyValuePair<int, RhythmNote>>();
            foreach (RhythmNote note in notes)
            {
                if (!Enum.IsDefined(typeof(SealAction), note.Action))
                    throw new ArgumentException("A chart note has an unknown action.", nameof(notes));
                indexedNotes.Add(new KeyValuePair<int, RhythmNote>(indexedNotes.Count, note));
            }
            indexedNotes.Sort((a, b) =>
            {
                int sampleOrder = a.Value.Sample.CompareTo(b.Value.Sample);
                return sampleOrder != 0 ? sampleOrder : a.Key.CompareTo(b.Key);
            });
            this.notes = new RhythmNote[indexedNotes.Count];
            for (int i = 0; i < this.notes.Length; i++) this.notes[i] = indexedNotes[i].Value;
            Notes = Array.AsReadOnly(this.notes);
            resolved = new bool[this.notes.Length];
            this.sampleRate = sampleRate;
            perfectWindowSamples = perfectMs * (sampleRate / 1000d);
            goodWindowSamples = goodMs * (sampleRate / 1000d);
        }

        public void Reset()
        {
            Array.Clear(resolved, 0, resolved.Length);
            nextNoteIndex = 0;
            ResolvedCount = PerfectCount = GoodCount = MissCount = StrayCount = Combo = MaxCombo = 0;
        }

        /// <summary>Resolve every overdue note, even when a frame skips several notes.</summary>
        public void Advance(int sample)
        {
            while (nextNoteIndex < notes.Length)
            {
                int index = nextNoteIndex;
                long offsetSamples = (long)sample - notes[index].Sample;
                // Equality is still playable; a note only expires after the late Good boundary.
                if (offsetSamples <= goodWindowSamples) break;
                Resolve(index, HitGrade.Miss, offsetSamples);
            }
        }

        /// <summary>
        /// Judge one press against the nearest unresolved note inside the Good window.
        /// A wrong action consumes that note as a Miss. Equal-distance ties choose the earlier
        /// note, with original chart order breaking simultaneous ties. Consumed notes never score twice.
        /// </summary>
        public Judgement Submit(SealAction action, int sample)
        {
            if (!Enum.IsDefined(typeof(SealAction), action))
                throw new ArgumentOutOfRangeException(nameof(action));

            Advance(sample);
            int nearestIndex = -1;
            long nearestDistance = long.MaxValue;
            long nearestOffset = 0;
            for (int i = nextNoteIndex; i < notes.Length; i++)
            {
                if (resolved[i]) continue;
                long offsetSamples = (long)sample - notes[i].Sample;
                if (offsetSamples < -goodWindowSamples) break;
                long distance = Math.Abs(offsetSamples);
                if (distance <= goodWindowSamples && distance < nearestDistance)
                {
                    nearestIndex = i;
                    nearestDistance = distance;
                    nearestOffset = offsetSamples;
                }
            }

            if (nearestIndex >= 0)
            {
                HitGrade grade = notes[nearestIndex].Action != action
                    ? HitGrade.Miss
                    : nearestDistance <= perfectWindowSamples ? HitGrade.Perfect : HitGrade.Good;
                return Resolve(nearestIndex, grade, nearestOffset);
            }

            // Saturation keeps repeated invalid input bounded, even during a very long session.
            if (StrayCount < int.MaxValue) StrayCount++;
            Combo = 0;
            var stray = new Judgement(-1, HitGrade.Stray, 0d, action);
            Judged?.Invoke(stray);
            return stray;
        }

        private Judgement Resolve(int index, HitGrade grade, long offsetSamples)
        {
            resolved[index] = true;
            ResolvedCount++;
            if (grade == HitGrade.Perfect) PerfectCount++;
            else if (grade == HitGrade.Good) GoodCount++;
            else MissCount++;

            if (grade == HitGrade.Miss) Combo = 0;
            else
            {
                Combo++;
                MaxCombo = Math.Max(MaxCombo, Combo);
            }
            while (nextNoteIndex < notes.Length && resolved[nextNoteIndex]) nextNoteIndex++;

            var judgement = new Judgement(index, grade, offsetSamples * 1000d / sampleRate, notes[index].Action);
            Judged?.Invoke(judgement);
            return judgement;
        }
    }
}
