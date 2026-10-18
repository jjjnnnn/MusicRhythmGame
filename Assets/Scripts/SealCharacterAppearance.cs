using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws the new Arctic Band artwork while the original controller continues to own
/// action timing, direction and upright/upside-down state. This component never submits input.
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class SealCharacterAppearance : MonoBehaviour
{
    private const float SourceWidth = 1774f;
    private const float PixelsPerUnit = 280f;
    private const float Ground = -.20f;
    private const float Centre = .36f;
    private readonly List<Sprite> ownedSprites = new List<Sprite>();
    private readonly Sprite[] poses = new Sprite[4];
    private SealOriginalAnimation original;
    private Transform visual;
    private SpriteRenderer spriteRenderer;
    private bool initialized;

    public bool IsPlayer { get; private set; }

    public static SealCharacterAppearance Attach(Animator actor, bool isPlayer)
    {
        if (actor == null || !actor.TryGetComponent<SealOriginalAnimation>(out var controller) || !controller.IsReady)
            return null;
        var appearance = actor.GetComponent<SealCharacterAppearance>();
        if (appearance == null) appearance = actor.gameObject.AddComponent<SealCharacterAppearance>();
        return appearance.Initialize(controller, isPlayer) ? appearance : null;
    }

    private bool Initialize(SealOriginalAnimation controller, bool isPlayer)
    {
        if (initialized) return true;
        var texture = Resources.Load<Texture2D>("Characters/ArcticBand/seals");
        if (texture == null) return false;
        original = controller;
        IsPlayer = isPlayer;

        // Padded, alpha-visible artwork bounds in the intact generated atlas. Keeping one
        // pixel scale and a common ground pivot avoids pose-dependent size or foot jumps.
        Rect[] rectangles = isPlayer
            ? new[] { new Rect(73, 495, 341, 330), new Rect(510, 562, 349, 268),
                new Rect(942, 501, 325, 332), new Rect(1377, 500, 328, 333) }
            : new[] { new Rect(73, 72, 341, 330), new Rect(511, 141, 348, 266),
                new Rect(942, 77, 325, 331), new Rect(1376, 76, 328, 332) };
        float scale = texture.width / SourceWidth;
        for (int i = 0; i < rectangles.Length; ++i)
        {
            var source = rectangles[i];
            var rect = new Rect(source.x * scale, texture.height - source.yMax * scale,
                source.width * scale, source.height * scale);
            var pivot = new Vector2(.5f, (Centre - Ground) * PixelsPerUnit / source.height);
            poses[i] = Sprite.Create(texture, rect, pivot, PixelsPerUnit * scale, 0, SpriteMeshType.FullRect);
            poses[i].name = (isPlayer ? "Blue player seal " : "Ivory partner seal ") + i;
            ownedSprites.Add(poses[i]);
        }

        visual = new GameObject(isPlayer ? "Blue player appearance" : "Ivory partner appearance").transform;
        visual.SetParent(transform, false);
        spriteRenderer = visual.gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = 50;
        spriteRenderer.color = Color.white;
        initialized = true;
        original.SetAppearanceVisible(false);
        Render();
        return true;
    }

    private void LateUpdate()
    {
        if (initialized && original != null) Render();
    }

    private void Render()
    {
        int motion = original.CurrentMotion;
        float duration = Mathf.Max(1, original.CurrentMotionDuration);
        float frame = Mathf.Clamp(original.CurrentMotionFrame, 0, duration);
        float orientation = original.IsUpsideDown ? 180 : 0;
        float rotation = orientation, x = 0, lift = 0, scaleX = 1, scaleY = 1;
        int pose = 0;

        if (motion == 1 || motion == 12)
        {
            // The native press recovers at frame 9 and holds neutral to frame 20.
            if (frame < 9)
            {
                pose = 1;
                float press = Mathf.Sin(Mathf.PI * frame / 9);
                scaleX += .025f * press;
                scaleY -= .035f * press;
            }
        }
        else if (motion == 2 || motion == 3 || motion == 13 || motion == 14)
        {
            int direction = motion == 2 || motion == 13 ? -1 : 1;
            if (frame < 9) pose = 1;
            else if (frame < 42)
            {
                pose = direction < 0 ? 2 : 3;
                rotation -= direction * 3;
            }
        }
        else if (motion == 4 || motion == 5 || motion == 15 || motion == 16)
        {
            int direction = motion == 4 || motion == 15 ? -1 : 1;
            if (frame >= 3 && frame < 38)
            {
                pose = direction < 0 ? 2 : 3;
                float rock = Mathf.Sin(Mathf.PI * (frame - 3) / 35);
                rotation -= direction * 5 * rock;
                x = direction * .045f * rock;
                lift = .018f * rock;
            }
        }
        else if (motion == 8 || motion == 9 || motion == 19 || motion == 20)
        {
            int direction = motion == 8 || motion == 19 ? 1 : -1;
            // Native rolls complete their translation in nine 60 Hz frames; the remaining
            // keys settle into the new phase. Keep that timing even when inputs arrive fast.
            if (frame < 9)
            {
                float t = Mathf.SmoothStep(0, 1, frame / 9);
                float start = original.IsUpsideDown ? 0 : 180;
                rotation = start - direction * 180 * t;
                x = direction * .10f * Mathf.Sin(Mathf.PI * t);
                lift = .08f * Mathf.Sin(Mathf.PI * t);
                scaleX = scaleY = 1 - .025f * Mathf.Sin(Mathf.PI * t);
            }
            else if (frame < duration - 8)
            {
                pose = direction < 0 ? 2 : 3;
                float settle = Mathf.Sin(Mathf.PI * (frame - 9) / Mathf.Max(1, duration - 17));
                rotation -= direction * 3 * settle;
            }
        }

        spriteRenderer.sprite = poses[pose];
        visual.localPosition = new Vector3(x, Centre + lift, 0);
        visual.localRotation = Quaternion.Euler(0, 0, rotation);
        visual.localScale = new Vector3(scaleX, scaleY, 1);
    }

    private void OnDestroy()
    {
        if (original != null) original.SetAppearanceVisible(true);
        if (visual != null) Release(visual.gameObject);
        foreach (var sprite in ownedSprites) if (sprite != null) Release(sprite);
        ownedSprites.Clear();
    }

    private static void Release(Object value)
    {
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }
}
