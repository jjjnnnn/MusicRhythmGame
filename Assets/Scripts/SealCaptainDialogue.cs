using UnityEngine;
using UnityEngine.UI;

/// <summary>Readable captain speech, kept beside the conductor and clear of the beat lane.</summary>
public sealed class SealCaptainDialogue : MonoBehaviour
{
    private static readonly Color Ink = new Color32(38, 63, 72, 255);
    private RhythmGame game;
    private RectTransform parentRect, bubble, tail;
    private Text label;
    private Canvas ownerCanvas;
    private Camera stageCamera;
    private Transform captain;
    private Vector3 captainAnchor;
    private bool hasCaptainAnchor;

    public bool IsVisible => bubble != null && bubble.gameObject.activeSelf;
    public string CurrentText => label != null ? label.text : "";

    public void Initialize(RhythmGame rhythmGame, RectTransform parent, Font font, Sprite panelSprite)
    {
        game = rhythmGame;
        parentRect = parent;
        ownerCanvas = parent.GetComponentInParent<Canvas>();
        stageCamera = Camera.main;
        if (bubble == null)
        {
            bubble = CreateRect("Penguin dialogue", parent);
            bubble.anchorMin = bubble.anchorMax = new Vector2(.5f, .5f);
            bubble.pivot = new Vector2(.5f, .5f);
            bubble.sizeDelta = new Vector2(330, 124);

            tail = CreateRect("Speech tail", bubble);
            tail.anchorMin = tail.anchorMax = new Vector2(0, .5f);
            tail.sizeDelta = new Vector2(40, 34);
            tail.anchoredPosition = new Vector2(2, -10);
            tail.gameObject.AddComponent<SealCaptainDialogueTail>().raycastTarget = false;

            var panelRect = CreateRect("Speech paper", bubble);
            Stretch(panelRect, 0, 0, 0, 0);
            var panel = panelRect.gameObject.AddComponent<Image>();
            panel.sprite = panelSprite;
            panel.color = new Color(1, 1, 1, .98f);
            panel.raycastTarget = false;
            if (panelSprite != null && panelSprite.border != Vector4.zero)
            {
                panel.type = Image.Type.Sliced;
                panel.pixelsPerUnitMultiplier = 3;
            }

            var textRect = CreateRect("Captain Chinese dialogue", bubble);
            Stretch(textRect, 26, 26, 15, 19);
            label = textRect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = 26;
            label.fontStyle = FontStyle.Bold;
            label.color = Ink;
            label.alignment = TextAnchor.MiddleCenter;
            label.lineSpacing = 1.12f;
            label.supportRichText = false;
            label.resizeTextForBestFit = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
        }
        Refresh();
    }

    public void Refresh()
    {
        if (bubble == null || game == null || parentRect == null) return;
        bool allowedState = game.State == SealGameState.Ready || game.State == SealGameState.CountIn || game.State == SealGameState.Playing;
        bool visible = allowedState && game.CaptainDialogueVisible && !string.IsNullOrEmpty(game.CaptainDialogueText);
        if (bubble.gameObject.activeSelf != visible) bubble.gameObject.SetActive(visible);
        if (!visible) return;
        if (label.text != game.CaptainDialogueText) label.text = game.CaptainDialogueText;
        PositionBubble();
    }

    private void PositionBubble()
    {
        var area = parentRect.rect;
        float width = Mathf.Min(330, Mathf.Max(220, area.width - 48));
        float height = width < 300 ? 148 : 124;
        bubble.sizeDelta = new Vector2(width, height);
        Vector2 position = new Vector2(300, 55);
        Transform currentCaptain = game.CaptainTransform;
        if (currentCaptain != captain)
        {
            captain = currentCaptain;
            hasCaptainAnchor = false;
            if (captain != null)
            {
                // Initialization runs while the captain is neutral. Keep that shoulder
                // anchor fixed so wider conducting poses do not push the speech around.
                bool hasBounds = false;
                Bounds bounds = default;
                foreach (var renderer in captain.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.sprite == null) continue;
                    if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                if (hasBounds)
                {
                    var shoulder = new Vector3(bounds.max.x, Mathf.Lerp(bounds.min.y, bounds.max.y, .70f), bounds.center.z);
                    captainAnchor = captain.InverseTransformPoint(shoulder);
                    hasCaptainAnchor = true;
                }
            }
        }
        if (stageCamera == null) stageCamera = Camera.main;
        if (hasCaptainAnchor && captain != null && stageCamera != null)
        {
            // Shoulder height keeps speech close to the face without covering either the face or the lane.
            Vector3 point = captain.TransformPoint(captainAnchor);
            Vector3 screen = stageCamera.WorldToScreenPoint(point);
            Camera uiCamera = ownerCanvas != null && ownerCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? ownerCanvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screen, uiCamera, out var local))
                position = local + new Vector2(width * .5f + 18, 0);
        }

        // The title card occupies the centre before the count-in. Use the spare space beside it.
        bool readyBesideMenu = area.width >= 1254;
        if (game.State == SealGameState.Ready)
        {
            if (readyBesideMenu) position.x = Mathf.Max(position.x, Mathf.Min(510, area.width - 36) * .5f + width * .5f + 20);
            else
            {
                // The menu is centred at 55% height and is 290 units tall;
                // the controls end 104 units above the canvas bottom.
                float menuBottom = area.yMin + area.height * .55f - 145;
                float controlsTop = area.yMin + 104;
                position = new Vector2(area.center.x, (controlsTop + menuBottom) * .5f);
            }
        }
        float minimumX = area.xMin + width * .5f + 22;
        float maximumX = area.xMax - width * .5f - 22;
        float minimumY = area.yMin + height * .5f + 116;
        float maximumY = area.yMax - height * .5f - 148;
        position.x = Mathf.Clamp(position.x, minimumX, maximumX);
        position.y = Mathf.Clamp(position.y, Mathf.Min(minimumY, maximumY), maximumY);
        bubble.anchoredPosition = position - area.center;
        tail.gameObject.SetActive(game.State != SealGameState.Ready || readyBesideMenu);
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private void OnDestroy()
    {
        if (bubble == null) return;
        if (Application.isPlaying) Destroy(bubble.gameObject);
        else DestroyImmediate(bubble.gameObject);
    }
}

/// <summary>A small ink-edged tail connects the existing paper artwork to its speaker.</summary>
internal sealed class SealCaptainDialogueTail : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        AddTriangle(mesh, new Vector2(-20, -4), new Vector2(20, 14), new Vector2(20, -15), new Color32(38, 63, 72, 255));
        AddTriangle(mesh, new Vector2(-13, -4), new Vector2(20, 9), new Vector2(20, -10), new Color32(255, 249, 236, 255));
    }

    private static void AddTriangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int first = mesh.currentVertCount;
        mesh.AddVert(a, color, Vector2.zero);
        mesh.AddVert(b, color, Vector2.zero);
        mesh.AddVert(c, color, Vector2.zero);
        mesh.AddTriangle(first, first + 1, first + 2);
    }
}
