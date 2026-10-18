using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>ImageGen atlas, kept intact; sprite rectangles select the finished artwork.</summary>
public sealed class SealUiArt : IDisposable
{
    private readonly List<Sprite> owned = new List<Sprite>();
    private readonly Texture2D texture;
    public Sprite Key { get; }
    public Sprite PressedKey { get; }
    public Sprite Paper { get; }
    public Sprite Track { get; }
    public Sprite Left { get; }
    public Sprite Right { get; }
    public Sprite Down { get; }
    public Sprite Pause { get; }
    public Sprite Play { get; }
    public Sprite Restart { get; }
    public Sprite Home { get; }
    public Sprite Settings { get; }
    public Sprite Tick { get; }
    public Sprite Seal { get; }
    public Sprite Ring { get; }
    public Sprite Sparkle { get; }

    // Shared by the scrolling notes and their matching physical controls.
    public static Color InputColor(int input) => input == 1 ? new Color32(234, 137, 120, 255) :
        input == 2 ? new Color32(129, 181, 220, 255) : new Color32(119, 200, 173, 255);

    public SealUiArt()
    {
        texture = Resources.Load<Texture2D>("UI/SealPaper/seal-ui-atlas");
        if (texture == null) throw new InvalidOperationException("Missing seal UI atlas.");
        Key = Cut("Key", 82, 108, 204, 205, 70);
        PressedKey = Cut("Pressed key", 383, 106, 200, 207, 70);
        Paper = Cut("Paper", 645, 70, 265, 255, 70);
        Track = Cut("Track", 969, 106, 203, 212, 32);
        Left = Cut("Left", 89, 412, 190, 178);
        Right = Cut("Right", 389, 412, 188, 178);
        Down = Cut("Down", 673, 404, 192, 194);
        Pause = Cut("Pause", 1000, 412, 150, 180);
        Play = Cut("Play", 105, 688, 163, 184);
        Restart = Cut("Restart", 387, 672, 196, 208);
        Home = Cut("Home", 664, 686, 216, 196);
        Settings = Cut("Settings", 985, 676, 195, 202);
        Tick = Cut("Tick", 145, 955, 78, 222);
        Seal = Cut("Seal", 371, 959, 222, 221);
        Ring = Cut("Ring", 664, 958, 224, 220);
        Sparkle = Cut("Sparkle", 1006, 983, 154, 164);
    }

    private Sprite Cut(string name, float x, float top, float width, float height, float border = 0)
    {
        const float sourceSize = 1254;
        float scale = texture.width / sourceSize;
        var rect = new Rect(x * scale, texture.height - (top + height) * scale, width * scale, height * scale);
        var sprite = Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100 * scale, 0,
            SpriteMeshType.FullRect, Vector4.one * border * scale);
        sprite.name = "Seal UI " + name;
        owned.Add(sprite);
        return sprite;
    }

    public void Dispose()
    {
        foreach (var sprite in owned) if (sprite != null) UnityEngine.Object.Destroy(sprite);
        owned.Clear();
    }
}
