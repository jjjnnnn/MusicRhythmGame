using System;
using SealRhythm;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A mirrored, sample-clock beat guide. It observes notes without submitting inputs.</summary>
public sealed class SealBeatLane : MonoBehaviour
{
    private const int MarkerPairs = 16;
    private const float LookAheadBeats = 3f;
    private static readonly Color Ink = new Color32(29, 58, 72, 255);
    private static readonly Color Coral = new Color32(203, 100, 79, 255);

    private RhythmGame game;
    private RectTransform root, track, center, ring, judgement;
    private Image ringImage;
    private Text judgementText;
    private CanvasGroup judgementGroup;
    private Sprite leftSprite, downSprite, rightSprite;
    private readonly MarkerPair[] pairs = new MarkerPair[MarkerPairs];
    private SealRhythmJudge observedJudge;
    private bool[] resolved;
    private int previousResolvedCount;
    private bool initialized, visible = true;
    private float judgementShownAt = -10f;

    private sealed class MarkerPair
    {
        public RectTransform Left, Right;
        public Image LeftArrow, RightArrow;

        public void SetVisible(bool value)
        {
            if (Left.gameObject.activeSelf != value) Left.gameObject.SetActive(value);
            if (Right.gameObject.activeSelf != value) Right.gameObject.SetActive(value);
        }

        public void Show(float distance, Sprite arrow, Color color, float opacity)
        {
            SetVisible(true);
            Left.anchoredPosition = new Vector2(-distance, 7);
            Right.anchoredPosition = new Vector2(distance, 7);
            if (LeftArrow.sprite != arrow) LeftArrow.sprite = arrow;
            if (RightArrow.sprite != arrow) RightArrow.sprite = arrow;
            color.a = opacity;
            LeftArrow.color = RightArrow.color = color;
        }
    }

    /// <summary>The parent is the HUD's dedicated 640 by 86 lane rectangle.</summary>
    public void Initialize(RhythmGame rhythmGame, RectTransform parent, Font font,
        Sprite tickSprite, Sprite centerSprite, Sprite leftArrow, Sprite downArrow,
        Sprite rightArrow, Sprite ringSprite)
    {
        game = rhythmGame;
        leftSprite = leftArrow;
        downSprite = downArrow;
        rightSprite = rightArrow;
        if (initialized) { BindJudge(); Refresh(); return; }

        root = parent;
        track = MakeImage("Beat line", root, null, new Vector2(600, 1), new Vector2(0, 7)).rectTransform;
        track.GetComponent<Image>().color = new Color(Ink.r, Ink.g, Ink.b, .2f);

        for (int i = 0; i < pairs.Length; i++)
        {
            var pair = new MarkerPair();
            pair.Left = MakeRect("Beat pair " + i + " left", root, new Vector2(48, 48), Vector2.zero);
            pair.Right = MakeRect("Beat pair " + i + " right", root, new Vector2(48, 48), Vector2.zero);
            pair.LeftArrow = MakeImage("Required key", pair.Left, downSprite, new Vector2(44, 44), Vector2.zero);
            pair.RightArrow = MakeImage("Required key", pair.Right, downSprite, new Vector2(44, 44), Vector2.zero);
            pair.SetVisible(false);
            pairs[i] = pair;
        }

        // Draw the central art after the markers, so incoming arrows disappear beneath it.
        ringImage = MakeImage("Beat pulse", root, ringSprite, new Vector2(60, 60), new Vector2(0, 7));
        ring = ringImage.rectTransform;
        ringImage.color = new Color(1, 1, 1, 0);
        center = MakeImage("Seal beat", root, centerSprite, new Vector2(48, 48), new Vector2(0, 7)).rectTransform;

        judgement = MakeRect("Beat judgement", root, new Vector2(250, 28), new Vector2(0, -30));
        judgementGroup = judgement.gameObject.AddComponent<CanvasGroup>();
        judgementGroup.interactable = judgementGroup.blocksRaycasts = false;
        judgementGroup.alpha = 0;
        var textObject = new GameObject("Judgement", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(judgement, false);
        judgementText = textObject.GetComponent<Text>();
        judgementText.font = font;
        judgementText.fontSize = 18;
        judgementText.fontStyle = FontStyle.Bold;
        judgementText.alignment = TextAnchor.MiddleCenter;
        judgementText.color = Ink;
        judgementText.raycastTarget = false;
        judgementText.supportRichText = false;
        judgementText.horizontalOverflow = HorizontalWrapMode.Overflow;
        judgementText.verticalOverflow = VerticalWrapMode.Overflow;
        var textRect = judgementText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;

        initialized = true;
        BindJudge();
        Refresh();
    }

    public void SetVisible(bool value)
    {
        visible = value;
        if (root != null && root.gameObject.activeSelf != value) root.gameObject.SetActive(value);
        if (!value && judgementGroup != null)
        {
            judgementShownAt = -10f;
            judgementGroup.alpha = 0;
        }
    }

    public void Refresh()
    {
        if (!initialized || game == null) return;
        BindJudge();
        if (!visible) return;

        float extent = Mathf.Max(30, Mathf.Min(300, root.rect.width * .5f - 20));
        track.sizeDelta = new Vector2(extent * 2, 1);
        int used = 0;
        int sample = game.InputSample;
        int sampleRate = Mathf.Max(1, game.SampleRate);
        float bpm = Mathf.Max(1, game.beatsPerMinute);
        double horizon = sampleRate * 60d / bpm * LookAheadBeats;

        if (observedJudge != null && observedJudge.ResolvedCount < previousResolvedCount)
            Array.Clear(resolved, 0, resolved.Length);
        previousResolvedCount = observedJudge != null ? observedJudge.ResolvedCount : 0;

        // All playable arrows, including the five opening nods, share the judge's
        // sequence and resolved state. A successful nod removes its arrow immediately.
        if (observedJudge != null)
        {
            var notes = observedJudge.Notes;
            for (int i = observedJudge.NextNoteIndex; i < notes.Count && used < pairs.Length; i++)
            {
                long delta = (long)notes[i].Sample - sample;
                if (delta > horizon) break;
                if (resolved[i]) continue;
                ShowMarker(used++, delta, notes[i].Input, sampleRate, bpm, extent);
            }
        }
        for (int i = used; i < pairs.Length; i++) pairs[i].SetVisible(false);

        // The seal breathes on the music's quarter notes, independently of input offset.
        double beat = game.SongTime * bpm / 60d;
        if (game.State == SealGameState.CountIn) beat = game.CountInBeats;
        float phase = (float)(beat - Math.Floor(beat));
        float pulse = 1f - Mathf.SmoothStep(0, 1, Mathf.Clamp01(phase / .36f));
        center.localScale = Vector3.one * (1f + (game.AssistEnabled ? .055f : .02f) * pulse);
        ring.localScale = Vector3.one * (1f + .22f * (1f - pulse));
        ringImage.color = new Color(1, 1, 1, game.AssistEnabled ? .6f * pulse : 0);

        float age = Time.unscaledTime - judgementShownAt;
        float life = Mathf.Clamp01(age / .5f);
        judgementGroup.alpha = age >= 0 && age < .5f ? 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.28f, .5f, age)) : 0;
        judgement.anchoredPosition = new Vector2(0, -32 + 7f * life);
        judgement.localScale = Vector3.one * (1f + .08f * (1f - Mathf.Clamp01(age / .12f)));
    }

    public void ShowJudgement(string label, string timing, bool success)
    {
        if (!initialized) return;
        // A single short grade keeps attention on the same point as the incoming beats.
        // Timing prose is intentionally omitted here; calibration stays in the settings.
        judgementText.text = string.IsNullOrEmpty(label) ? string.Empty : label.TrimEnd('!');
        judgementText.color = success ? Ink : Coral;
        judgementShownAt = Time.unscaledTime;
        judgementGroup.alpha = visible ? 1 : 0;
    }

    /// <summary>Zero is the hit point; the full look-ahead window starts at the lane edge.</summary>
    public static float DistanceFromCenter(long sampleDelta, int sampleRate, float bpm,
        float extent, float lookAheadBeats = LookAheadBeats)
    {
        if (sampleRate <= 0 || bpm <= 0 || lookAheadBeats <= 0 || extent <= 0) return 0;
        double fraction = sampleDelta * (double)bpm / (sampleRate * 60d * lookAheadBeats);
        return Mathf.Clamp01((float)fraction) * extent;
    }

    private void ShowMarker(int index, long delta, SealInput input, int sampleRate, float bpm, float extent)
    {
        float distance = DistanceFromCenter(delta, sampleRate, bpm, extent);
        float edge = distance / Mathf.Max(1, extent);
        float opacity = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.88f, 1f, edge));
        Sprite arrow = input == SealInput.Left ? leftSprite : input == SealInput.Right ? rightSprite : downSprite;
        pairs[index].Show(distance, arrow, SealUiArt.InputColor((int)input), opacity);
    }

    private void BindJudge()
    {
        var nextJudge = game != null ? game.Judge : null;
        if (ReferenceEquals(nextJudge, observedJudge)) return;
        if (observedJudge != null) observedJudge.Judged -= OnNoteJudged;
        observedJudge = nextJudge;
        previousResolvedCount = 0;
        resolved = nextJudge != null ? new bool[nextJudge.TotalNotes] : null;
        if (nextJudge == null) return;
        for (int i = 0; i < nextJudge.NextNoteIndex; i++) resolved[i] = true;
        previousResolvedCount = nextJudge.ResolvedCount;
        nextJudge.Judged += OnNoteJudged;
    }

    private void OnNoteJudged(Judgement result)
    {
        if (resolved != null && result.NoteIndex >= 0 && result.NoteIndex < resolved.Length)
            resolved[result.NoteIndex] = true;
    }

    private static RectTransform MakeRect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var item = new GameObject(name, typeof(RectTransform));
        var rect = item.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static Image MakeImage(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 position)
    {
        var rect = MakeRect(name, parent, size, position);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = sprite != null;
        image.raycastTarget = false;
        image.color = Color.white;
        return image;
    }

    private void OnDestroy()
    {
        if (observedJudge != null) observedJudge.Judged -= OnNoteJudged;
    }
}
