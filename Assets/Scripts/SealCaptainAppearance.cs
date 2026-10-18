using UnityEngine;

/// <summary>New penguin artwork, driven only by the existing captain cue samples.</summary>
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
    private int cue = -1, step;
    private float cueTime, pausedElapsed;
    public bool IsReady => initialized;
    public int CurrentPose { get; private set; }

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

    public void PlayCue(int index, int sample)
    {
        if (!initialized || index < 0 || index > 2) return;
        cue = index; cueTime = sample / (float)game.SampleRate;
        if (index == 0) step = 1 - step;
        Render(Mathf.Max(0, game.SongTime - cueTime));
    }
    public void ResetPose()
    {
        if (!initialized) return;
        paused = false; cue = -1; step = 0; cueTime = pausedElapsed = 0; Render(1);
    }
    public void SetPaused(bool value)
    {
        if (value && !paused) pausedElapsed = Mathf.Max(0, game.SongTime - cueTime);
        paused = value;
    }
    private void LateUpdate()
    {
        if (initialized) Render(paused ? pausedElapsed : Mathf.Max(0, game.SongTime - cueTime));
    }
    private void Render(float elapsed)
    {
        int pose = 0; float bob = 0, tilt = 0, squash = 1, side = 0;
        if (cue == 0 && elapsed < .34f)
        {
            pose = step == 0 ? 1 : 2;
            float wave = Mathf.Sin(Mathf.Clamp01(elapsed / .34f) * Mathf.PI);
            bob = wave * .055f; side = wave * (step == 0 ? -.035f : .035f); tilt = wave * (step == 0 ? 2 : -2);
        }
        else if (cue == 1 && elapsed < .26f)
        { pose = 3; squash = 1 - .025f * Mathf.Sin(elapsed / .26f * Mathf.PI); }
        else if (cue == 2 && elapsed < .38f)
        { pose = elapsed < .18f ? 4 : 5; bob = .035f * Mathf.Sin(elapsed / .38f * Mathf.PI); }
        CurrentPose = pose; body.sprite = frames[pose];
        body.transform.localPosition = ground + new Vector3(side, bob, 0);
        body.transform.localScale = new Vector3(.94f, .94f * squash, 1);
        body.transform.localRotation = Quaternion.Euler(0, 0, tilt);
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
