using UnityEngine;
using SealRhythm;

/// <summary>Penguin conductor poses use the same input and sample clock as the chart.</summary>
[DefaultExecutionOrder(100)]
public sealed class SealCaptainAppearance : MonoBehaviour
{
    private static Sprite[] frames;
    private static readonly Rect[] SourceRects =
    {
        new Rect(120, 49, 311, 422), new Rect(558, 42, 397, 432), new Rect(1071, 44, 434, 425),
        new Rect(157, 562, 279, 397), new Rect(586, 520, 377, 445), new Rect(1083, 524, 377, 439)
    };
    private RhythmGame game;
    private Animator originalAnimator;
    private bool animatorWasEnabled, initialized, paused;
    private SpriteRenderer[] oldRenderers;
    private bool[] rendererWasEnabled;
    private SpriteRenderer body;
    private Vector3 ground;
    // A half-beat is about 226 ms. Finish each gesture before the next one can arrive.
    private const float GestureDuration = .20f;
    private int pausedElapsedSamples;
    public bool IsReady => initialized;
    public int CurrentPose { get; private set; }
    public SealInput LastBeatInput { get; private set; } = SealInput.Nod;
    public int LastBeatSample { get; private set; } = -1;
    public int BeatCueCount { get; private set; }

    public static SealCaptainAppearance Attach(Animator animator, RhythmGame rhythmGame)
    {
        if (animator == null || rhythmGame == null || !LoadArt()) return null;
        var skin = animator.GetComponent<SealCaptainAppearance>();
        if (skin == null) skin = animator.gameObject.AddComponent<SealCaptainAppearance>();
        skin.Initialize(animator, rhythmGame);
        return skin;
    }

    private static bool LoadArt()
    {
        if (frames != null && frames[0] != null) return true;
        var texture = Resources.Load<Texture2D>("Characters/ArcticBand/captain");
        if (texture == null) return false;
        frames = new Sprite[SourceRects.Length];
        float scale = texture.width / 1536f;
        for (int i = 0; i < frames.Length; i++)
        {
            var r = SourceRects[i];
            var rect = new Rect(r.x * scale, texture.height - (r.y + r.height) * scale, r.width * scale, r.height * scale);
            frames[i] = Sprite.Create(texture, rect, new Vector2(.5f, 0), 148 * scale, 0, SpriteMeshType.FullRect);
            frames[i].name = "Arctic penguin pose " + i;
        }
        return true;
    }

    private void Initialize(Animator animator, RhythmGame rhythmGame)
    {
        if (initialized) return;
        game = rhythmGame; originalAnimator = animator; animatorWasEnabled = animator.enabled;
        oldRenderers = animator.GetComponentsInChildren<SpriteRenderer>(true);
        rendererWasEnabled = new bool[oldRenderers.Length];
        var bounds = new Bounds(); bool hasBounds = false;
        for (int i = 0; i < oldRenderers.Length; i++)
        {
            rendererWasEnabled[i] = oldRenderers[i].enabled;
            if (oldRenderers[i].enabled && oldRenderers[i].gameObject.activeInHierarchy)
            {
                if (!hasBounds) { bounds = oldRenderers[i].bounds; hasBounds = true; }
                else bounds.Encapsulate(oldRenderers[i].bounds);
            }
            oldRenderers[i].enabled = false;
        }
        animator.enabled = false;
        var go = new GameObject("Penguin conductor"); go.transform.SetParent(transform, false);
        body = go.AddComponent<SpriteRenderer>(); body.sortingOrder = 5;
        ground = new Vector3(0, hasBounds ? transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)).y : -2.18f, 0);
        initialized = true; ResetPose();
    }

    public void PlayBeat(SealInput input, int sample)
    {
        if (!initialized || (input != SealInput.Nod && input != SealInput.Left && input != SealInput.Right)) return;
        LastBeatInput = input;
        LastBeatSample = sample;
        BeatCueCount++;
        pausedElapsedSamples = ElapsedSamples();
        Render(pausedElapsedSamples);
    }
    public void ResetPose()
    {
        if (!initialized) return;
        paused = false;
        LastBeatInput = SealInput.Nod;
        LastBeatSample = -1;
        BeatCueCount = 0;
        pausedElapsedSamples = 0;
        Render(0);
    }
    public void SetPaused(bool value)
    {
        if (value && !paused) pausedElapsedSamples = ElapsedSamples();
        paused = value;
    }
    private int ElapsedSamples()
    {
        int songSample = Mathf.RoundToInt(game.SongTime * game.SampleRate);
        return Mathf.Max(0, songSample - LastBeatSample);
    }
    private void LateUpdate()
    {
        if (initialized) Render(paused ? pausedElapsedSamples : ElapsedSamples());
    }
    private void Render(int elapsedSamples)
    {
        float elapsed = elapsedSamples / (float)Mathf.Max(1, game.SampleRate);
        int pose = 0;
        float bob = 0, squash = 1;
        if (LastBeatSample >= 0 && elapsed < GestureDuration)
        {
            pose = LastBeatInput == SealInput.Nod ? 3 : LastBeatInput == SealInput.Left ? 1 : 2;
            float wave = Mathf.Sin(Mathf.Clamp01(elapsed / GestureDuration) * Mathf.PI);
            bob = wave * (LastBeatInput == SealInput.Nod ? -.025f : .018f);
            if (LastBeatInput == SealInput.Nod) squash -= .015f * wave;
        }
        CurrentPose = pose; body.sprite = frames[pose];
        body.transform.localPosition = ground + new Vector3(0, bob, 0);
        body.transform.localScale = new Vector3(.94f, .94f * squash, 1);
        body.transform.localRotation = Quaternion.identity;
    }
    private void OnDestroy()
    {
        if (body != null)
        { if (Application.isPlaying) Destroy(body.gameObject); else DestroyImmediate(body.gameObject); }
        if (originalAnimator != null) originalAnimator.enabled = animatorWasEnabled;
        if (oldRenderers != null) for (int i = 0; i < oldRenderers.Length; i++)
            if (oldRenderers[i] != null) oldRenderers[i].enabled = rendererWasEnabled[i];
    }
}
