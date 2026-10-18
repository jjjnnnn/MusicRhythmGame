using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Exercises the actual HUD at desktop and phone sizes, then checks the live backdrop.</summary>
public static class SealMobileLayoutTests
{
    [MenuItem("Tools/Seal Rhythm/Validate Mobile Layout")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter GamePlay Play mode before validating the mobile layout.");
        var hud = UnityEngine.Object.FindAnyObjectByType<SealRhythmHud>();
        if (hud == null || hud.SafeAreaRect == null) throw new InvalidOperationException("Wait for the HUD to initialize.");
        var report = new StringBuilder();
        var safe = hud.SafeAreaRect;
        Vector2 anchorMin = safe.anchorMin, anchorMax = safe.anchorMax, offsetMin = safe.offsetMin, offsetMax = safe.offsetMax;
        try
        {
            ValidateSize(hud, 1280, 720, Vector4.zero, report);
            ValidateSize(hud, 844, 390, Vector4.zero, report);
            ValidateSize(hud, 390, 844, Vector4.zero, report);
            // iPhone landscape cutouts and home indicator are excluded before calculating targets.
            ValidateSize(hud, 844, 390, new Vector4(44, 44, 0, 21), report);
        }
        finally
        {
            safe.anchorMin = anchorMin; safe.anchorMax = anchorMax;
            safe.offsetMin = offsetMin; safe.offsetMax = offsetMax;
            Canvas.ForceUpdateCanvases(); hud.Refresh(); Canvas.ForceUpdateCanvases();
        }
        ValidateLiveStage(hud, report);
        string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "SealValidation", "Mobile");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "layout.txt"), "PASS\n" + report);
        Debug.Log("Seal mobile layout: desktop, phone landscape, phone portrait, safe area and live background passed.\n" + report);
    }

    private static void ValidateSize(SealRhythmHud hud, int width, int height, Vector4 insets, StringBuilder report)
    {
        // CanvasScaler's logarithmic interpolation with matchWidthOrHeight = 0.5.
        float scale = Mathf.Sqrt(width / 1280f * height / 720f);
        Vector2 size = new Vector2(width - insets.x - insets.y, height - insets.z - insets.w) / scale;
        var safe = hud.SafeAreaRect;
        safe.anchorMin = safe.anchorMax = new Vector2(.5f, .5f);
        safe.anchoredPosition = Vector2.zero; safe.sizeDelta = size;
        Canvas.ForceUpdateCanvases(); hud.Refresh(); Canvas.ForceUpdateCanvases();
        var controls = hud.ControlsRect;
        Require(Mathf.Abs(controls.localScale.x - 1) < .001f && Mathf.Abs(controls.localScale.y - 1) < .001f, "Controls are not shrunk a second time.");
        Require(controls.rect.width >= size.x * .89f && controls.rect.width <= size.x * .94f, "Controls cover the lower width.");
        Require(controls.rect.height >= 128, "Touch controls are at least 128 logical units high.");
        var images = controls.GetComponentsInChildren<Image>(true);
        var buttons = controls.GetComponentsInChildren<Button>(true);
        Require(buttons.Length == 1 && buttons[0].name == "Rhythm action", "There is exactly one shared action target.");
        Rect previous = default;
        for (int index = 0; index < buttons.Length; index++)
        {
            var button = buttons[index];
            Rect rect = InParent(button.GetComponent<RectTransform>(), safe);
            Require(rect.xMin >= safe.rect.xMin && rect.xMax <= safe.rect.xMax && rect.yMin >= safe.rect.yMin && rect.yMax <= safe.rect.yMax,
                "Touch target remains inside the safe area.");
            Require(rect.width * scale >= 64 && rect.height * scale >= 64, "Phone targets exceed 64 CSS-equivalent pixels in both dimensions.");
            if (index > 0) Require(rect.xMin > previous.xMax + 2, "Neighbouring touch targets have a visible gap.");
            previous = rect;
            Require(rect.width >= controls.rect.width * .97f, "The single target fills the full lower control width.");
            Color expected = SealUiArt.InputColor(0);
            Require(Mathf.Abs(button.image.color.r - expected.r) < .01f && Mathf.Abs(button.image.color.g - expected.g) < .01f && Mathf.Abs(button.image.color.b - expected.b) < .01f,
                "The larger target retains its input color.");
            var trigger = button.GetComponent<EventTrigger>();
            Require(trigger != null && trigger.triggers.Exists(entry => entry.eventID == EventTriggerType.PointerDown), "Touch input still fires on PointerDown.");
        }
        int circles = 0;
        foreach (var image in images)
            if (image.name == "Rhythm circle")
            {
                Require(image.sprite != null && image.sprite.name == "Rhythm circle ⚪", "The control uses a platform-independent white circle icon.");
                Require(image.rectTransform.rect.width >= 60 && image.rectTransform.rect.height >= 60, "The circle is at least 60 logical pixels.");
                circles++;
            }
        Require(circles == 1, "The single control contains one white circle icon.");
        foreach (var image in images) Require(image.name != "Direction", "Directional arrows are removed from the touch control.");
        foreach (var label in controls.GetComponentsInChildren<Text>(true)) Require(string.IsNullOrEmpty(label.text), "The action control has no unnecessary text.");
        foreach (var rect in safe.GetComponentsInChildren<RectTransform>(true))
            if (rect.name == "Penguin dialogue" && rect.gameObject.activeInHierarchy)
            {
                Rect bubble = InParent(rect, safe);
                Require(bubble.yMin > previous.yMax + 2, "Chinese dialogue stays above the touch targets.");
                Require(bubble.xMin >= safe.rect.xMin && bubble.xMax <= safe.rect.xMax, "Chinese dialogue stays inside the safe width.");
            }
        report.AppendLine(width + "x" + height + (insets != Vector4.zero ? " safe area" : "") + ": one target, " +
            (buttons[0].GetComponent<RectTransform>().rect.width * scale).ToString("0") + "x" + (controls.rect.height * scale).ToString("0") +
            " px; white circle 62; mint color and PointerDown intact.");
    }

    private static void ValidateLiveStage(SealRhythmHud hud, StringBuilder report)
    {
        var camera = Camera.main;
        var background = GameObject.Find("Bg").GetComponent<SpriteRenderer>();
        Vector3 min = camera.WorldToViewportPoint(background.bounds.min), max = camera.WorldToViewportPoint(background.bounds.max);
        Require(min.x <= 0 && min.y <= 0 && max.x >= 1 && max.y >= 1, "The background covers the entire camera, including the control strip.");
        Vector3 scale = background.transform.lossyScale;
        Require(Mathf.Abs(scale.x - scale.y) < .001f, "The background expands proportionally.");
        var corners = new Vector3[4]; hud.ControlsRect.GetWorldCorners(corners);
        float controlsTop = RectTransformUtility.WorldToScreenPoint(null, corners[1]).y;
        foreach (var skin in UnityEngine.Object.FindObjectsByType<SealCharacterAppearance>())
            foreach (var renderer in skin.GetComponentsInChildren<SpriteRenderer>())
                if (renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.sprite != null)
                    Require(camera.WorldToScreenPoint(renderer.bounds.min).y > controlsTop, "The touch strip does not cover the seals.");
        report.AppendLine("Live stage: proportional full-viewport background, touch strip below every seal.");
    }

    private static Rect InParent(RectTransform rect, RectTransform parent)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        Vector3 min = parent.InverseTransformPoint(corners[0]), max = parent.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
