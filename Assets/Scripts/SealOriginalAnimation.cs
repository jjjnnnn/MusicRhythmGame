using System;
using System.Collections.Generic;
using SealRhythm;
using UnityEngine;

/// <summary>
/// Draws the original Wii goma BRCAD poses from the extracted, unmodified texture atlas.
/// Animation keys are discrete 60 Hz frames. Both per-key translation and signed part scale
/// matter to a roll; rotating the old Unity body rig cannot reproduce those poses.
/// </summary>
public sealed class SealOriginalAnimation : MonoBehaviour
{
    [Serializable] public sealed class AnimationData
    {
        public SpriteData[] sprites;
        public Motion[] animations;
    }
    [Serializable] public sealed class SpriteData { public Part[] parts; }
    [Serializable] public sealed class Part
    {
        public int[] texture, position, flip;
        public float[] scale;
        public float rotation;
        public int opacity;
    }
    [Serializable] public sealed class Motion
    {
        public string name;
        public int totalDuration;
        public Key[] frames;
    }
    [Serializable] public sealed class Key
    {
        public int sprite, duration, opacity;
        public int[] position;
        public float[] scale;
        public float rotation;
    }

    private const float PixelsPerUnit = 100f;
    private const float FrameRate = 60f;
    private static AnimationData data;
    private static Texture2D atlas;
    private static readonly Dictionary<Rect, Sprite> spriteCache = new Dictionary<Rect, Sprite>();
    private readonly List<SpriteRenderer> bodyParts = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> faceParts = new List<SpriteRenderer>();
    private Transform bodyRoot, faceAnchor, faceRoot;
    private Animator oldAnimator;
    private SpriteRenderer[] oldRenderers;
    private bool[] oldRendererEnabled;
    private bool oldAnimatorEnabled, paused;
    private bool initialized, phaseB, nextFlipToLeft;
    private int motionIndex = 1;
    private float elapsedFrames, faceFrames;
    private float speed = 1f;
    private int motionStartedFrame = -1;
    private Vector3 rollCompensation;
    private int rollSettleFrames;

    public bool IsReady => initialized;
    public int CurrentMotion => motionIndex;
    public bool IsUpsideDown => phaseB;
    public bool NextFlipToLeft => nextFlipToLeft;

    public static SealOriginalAnimation Attach(Animator animator)
    {
        if (animator == null || !LoadData()) return null;
        var player = animator.GetComponent<SealOriginalAnimation>();
        if (player == null) player = animator.gameObject.AddComponent<SealOriginalAnimation>();
        player.Initialize(animator);
        return player;
    }

    private static bool LoadData()
    {
        if (data != null && atlas != null) return true;
        var json = Resources.Load<TextAsset>("Art/Level_cellanim/OriginalGoma/goma");
        atlas = Resources.Load<Texture2D>("Art/Level_cellanim/Sprite/cellanim.tpl");
        if (json == null || atlas == null) return false;
        data = JsonUtility.FromJson<AnimationData>(json.text);
        return data != null && data.sprites != null && data.animations != null && data.animations.Length >= 24;
    }

    private void Initialize(Animator animator)
    {
        if (initialized) return;
        oldAnimator = animator;
        oldAnimatorEnabled = animator.enabled;
        oldRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        oldRendererEnabled = new bool[oldRenderers.Length];
        animator.enabled = false;
        for (int i = 0; i < oldRenderers.Length; ++i)
        {
            oldRendererEnabled[i] = oldRenderers[i].enabled;
            oldRenderers[i].enabled = false;
        }
        bodyRoot = Child("Original Wii seal", transform);
        faceAnchor = Child("Face attachment", bodyRoot);
        faceRoot = Child("Face animation", faceAnchor);
        initialized = true;
        ResetPose();
    }

    public void ResetPose()
    {
        if (!initialized) return;
        paused = false;
        phaseB = nextFlipToLeft = false;
        rollCompensation = Vector3.zero;
        faceFrames = 0;
        // Press finishes on the neutral pose; Interval is the two-seal instruction diagram.
        motionIndex = 1;
        elapsedFrames = data.animations[motionIndex].totalDuration;
        Render();
    }

    public void SetPaused(bool value) { paused = value; }

    /// <summary>
    /// A scored hit or scheduled partner action follows the chart's original state. A player's
    /// earlier missed or extra inputs must not change the animation of a later correct hit.
    /// Preserve the current visible pose so fixed-stage roll alignment remains continuous.
    /// </summary>
    public void PlayChartAction(SealAction action, bool phaseBefore, bool nextFlipLeftBefore)
    {
        phaseB = phaseBefore;
        nextFlipToLeft = nextFlipLeftBefore;
        PlayAction(action);
    }

    /// <summary>Original opcode 0x202 preparation pose; no player input or phase advance.</summary>
    public void PlayReady(bool toLeft)
    {
        nextFlipToLeft = toLeft;
        PlayAnimation((toLeft ? 2 : 3) + (phaseB ? 11 : 0));
    }

    /// <summary>
    /// Original goma state machine (SOMJ01 scene dispatch 0x202/203/204): every flip or
    /// roll advances direction; only rolls switch the upright/upside-down pose group.
    /// Left/right inputs remain separate judgement requirements in this keyboard adaptation.
    /// </summary>
    public void PlayAction(SealAction action)
    {
        Vector3 previousCenter = BodyCenter();
        int index;
        if (action == SealAction.Nod) index = phaseB ? 12 : 1;
        else if (action == SealAction.Roll)
        {
            index = (nextFlipToLeft ? 9 : 8) + (phaseB ? 0 : 11);
            nextFlipToLeft = !nextFlipToLeft;
            phaseB = !phaseB;
        }
        else
        {
            index = (nextFlipToLeft ? 4 : 5) + (phaseB ? 11 : 0);
            nextFlipToLeft = !nextFlipToLeft;
        }
        PlayAnimation(index);
        if (action == SealAction.Roll)
        {
            // Wii moves the actor and scrolls the scene during a roll. This project's fixed
            // stage retains the original poses but aligns the roll's start to the previous
            // body center. Remove that stage-only offset over its native translation keys.
            rollCompensation = previousCenter - BodyCenter();
            rollSettleFrames = LastTranslationFrame(data.animations[index]);
            Render();
        }
    }

    private Vector3 BodyCenter() => bodyParts.Count > 2
        ? transform.InverseTransformPoint(bodyParts[2].transform.position) : Vector3.zero;

    private static int LastTranslationFrame(Motion motion)
    {
        int frame = 0, last = 0;
        foreach (var key in motion.frames)
        {
            frame += key.duration;
            if (key.position != null && (key.position[0] != 0 || key.position[1] != 0)) last = frame;
        }
        return Math.Max(1, last);
    }

    /// <summary>Uses ROM animation indices, preserving native frame durations and transforms.</summary>
    public void PlayAnimation(int index, float playbackSpeed = 1f)
    {
        if (!initialized || index < 0 || index >= data.animations.Length) return;
        motionIndex = index;
        elapsedFrames = 0;
        rollCompensation = Vector3.zero;
        speed = Mathf.Max(0, playbackSpeed);
        motionStartedFrame = Time.frameCount;
        Render();
    }

    private void Update()
    {
        if (!initialized || paused) return;
        if (motionStartedFrame != Time.frameCount) AdvanceFrames(Time.unscaledDeltaTime * FrameRate);
    }

    /// <summary>Advances native 60 Hz frame units, also usable by deterministic editor checks.</summary>
    public void AdvanceFrames(float frames)
    {
        if (!initialized || paused) return;
        elapsedFrames += Mathf.Max(0, frames) * speed;
        faceFrames += Mathf.Max(0, frames);
        Render();
    }

    public static Key FrameAt(Motion motion, float frame, bool loop = false)
    {
        if (motion == null || motion.frames == null || motion.frames.Length == 0) return null;
        if (loop && motion.totalDuration > 0) frame %= motion.totalDuration;
        frame = Mathf.Max(0, frame);
        foreach (var key in motion.frames)
        {
            if (frame < key.duration) return key;
            frame -= key.duration;
        }
        return motion.frames[motion.frames.Length - 1];
    }

    private void Render()
    {
        var key = FrameAt(data.animations[motionIndex], elapsedFrames);
        if (key == null || key.sprite < 0 || key.sprite >= data.sprites.Length) return;
        ApplyKey(bodyRoot, key);
        if (rollCompensation != Vector3.zero && rollSettleFrames > 0)
            bodyRoot.localPosition += rollCompensation * (1 - Mathf.SmoothStep(0, 1, elapsedFrames / rollSettleFrames));
        var pose = data.sprites[key.sprite];
        RenderParts(pose.parts, bodyRoot, bodyParts, key.opacity / 255f, 10, 10);

        // The engine links the face to part 3. Its invisible anchor supplies its complete
        // transform; anchor opacity/UV flips deliberately do not propagate to the face.
        bool hasAnchor = pose.parts != null && pose.parts.Length > 3;
        faceAnchor.gameObject.SetActive(hasAnchor);
        if (!hasAnchor) return;
        ApplyPartTransform(faceAnchor, pose.parts[3]);
        var faceKey = FrameAt(data.animations[23], faceFrames, true);
        ApplyKey(faceRoot, faceKey);
        RenderParts(data.sprites[faceKey.sprite].parts, faceRoot, faceParts, key.opacity / 255f * faceKey.opacity / 255f, 41, 1);
    }

    private static void ApplyKey(Transform target, Key key)
    {
        target.localPosition = key.position != null && key.position.Length >= 2
            ? new Vector3(key.position[0] / PixelsPerUnit, -key.position[1] / PixelsPerUnit, 0) : Vector3.zero;
        target.localRotation = Quaternion.Euler(0, 0, -key.rotation);
        target.localScale = new Vector3(key.scale[0], key.scale[1], 1);
    }

    public static Vector2 PartCenter(Part part)
    {
        return new Vector2(
            (part.position[0] - 512 + part.texture[2] * part.scale[0] * .5f) / PixelsPerUnit,
            -(part.position[1] - 512 + part.texture[3] * part.scale[1] * .5f) / PixelsPerUnit);
    }

    private static void ApplyPartTransform(Transform target, Part part)
    {
        target.localPosition = PartCenter(part);
        target.localRotation = Quaternion.Euler(0, 0, -part.rotation);
        target.localScale = new Vector3(part.scale[0], part.scale[1], 1);
    }

    private static void RenderParts(Part[] parts, Transform parent, List<SpriteRenderer> renderers, float opacity, int sortBase, int sortStep)
    {
        int count = parts != null ? parts.Length : 0;
        while (renderers.Count < count)
            renderers.Add(Child("Part " + renderers.Count, parent).gameObject.AddComponent<SpriteRenderer>());
        for (int i = 0; i < renderers.Count; ++i)
        {
            var renderer = renderers[i];
            renderer.enabled = i < count;
            if (i >= count) continue;
            var part = parts[i];
            ApplyPartTransform(renderer.transform, part);
            var rect = new Rect(part.texture[0], atlas.height - part.texture[1] - part.texture[3], part.texture[2], part.texture[3]);
            if (!spriteCache.TryGetValue(rect, out var sprite) || sprite == null)
            {
                sprite = Sprite.Create(atlas, rect, new Vector2(.5f, .5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprite.name = "Wii goma " + rect;
                spriteCache[rect] = sprite;
            }
            renderer.sprite = sprite;
            renderer.flipX = part.flip[0] != 0;
            renderer.flipY = part.flip[1] != 0;
            renderer.color = new Color(1, 1, 1, opacity * part.opacity / 255f);
            renderer.sortingOrder = sortBase + i * sortStep;
        }
    }

    private static Transform Child(string label, Transform parent)
    {
        var child = new GameObject(label).transform;
        child.SetParent(parent, false);
        return child;
    }

    private void OnDestroy()
    {
        if (bodyRoot != null)
        {
            if (Application.isPlaying) Destroy(bodyRoot.gameObject);
            else DestroyImmediate(bodyRoot.gameObject);
        }
        if (oldAnimator != null) oldAnimator.enabled = oldAnimatorEnabled;
        if (oldRenderers != null) for (int i = 0; i < oldRenderers.Length; ++i)
            if (oldRenderers[i] != null) oldRenderers[i].enabled = oldRendererEnabled[i];
    }
}
