using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Paper-and-ice UI with generated artwork and a sample-clock beat lane.</summary>
public sealed class SealRhythmHud : MonoBehaviour
{
    private static readonly Color Ink = new Color32(38, 63, 72, 255);
    private static readonly Color Muted = new Color32(105, 129, 133, 255);
    private static readonly Color Coral = new Color32(207, 105, 88, 255);
    private RhythmGame game;
    private Canvas canvas;
    private RectTransform canvasRect, controls;
    private SealUiArt art;
    private SealBeatLane lane;
    private Font font;
    private bool ownsFont;
    private GameObject ownedEventSystem, shade, countdown;
    private GameObject readyPanel, pausePanel, resultsPanel, errorPanel;
    private RectTransform readyRect, pauseRect, resultsRect, errorRect, laneRect;
    private Text scoreText, comboText, accuracyText, timeText, assistText, offsetText, countdownText;
    private Text gradeText, resultScoreText, resultStatsText, bestText, errorText;
    private RectTransform progressFill;
    private Button pauseButton;
    private Image pauseIcon;
    private readonly Button[] actionButtons = new Button[3];
    private readonly float[] pressedAt = { -10, -10, -10 };
    private bool built, stateKnown;
    private SealGameState displayedState;
    private float lastWidth = -1;

    public void Initialize(RhythmGame rhythmGame)
    {
        game = rhythmGame;
        if (!built) Build();
        Refresh();
    }
    public void PulseInput(int input)
    {
        if (input >= 0 && input < pressedAt.Length) pressedAt[input] = Time.unscaledTime;
    }
    public void Refresh()
    {
        if (!built || game == null) return;
        var state = game.State;
        bool playing = state == SealGameState.Playing, ready = state == SealGameState.Ready;
        bool paused = state == SealGameState.Paused, counting = state == SealGameState.CountIn;
        bool finished = state == SealGameState.Results;
        if (!stateKnown || displayedState != state)
        {
            stateKnown = true; displayedState = state;
            readyPanel.SetActive(ready); pausePanel.SetActive(paused); resultsPanel.SetActive(finished);
            errorPanel.SetActive(state == SealGameState.Error);
            shade.SetActive(ready || paused || finished || state == SealGameState.Error);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
        var judge = game.Judge;
        int score = judge != null ? judge.Score : 0;
        double accuracy = judge != null ? judge.Accuracy : 0;
        double denominator = judge != null ? judge.ResolvedCount + judge.StrayCount * .25 : 0;
        double live = denominator > 0 ? (judge.PerfectCount + judge.GoodCount * .65) / denominator : 0;
        SetText(scoreText, score.ToString("D6"));
        SetText(comboText, (judge != null ? judge.Combo : 0) + " 连击");
        SetText(accuracyText, denominator > 0 ? (live * 100).ToString("0.0") + "%" : "—");
        SetText(timeText, FormatTime(game.SongTime) + " / " + FormatTime(game.SongDuration));
        SetFill(progressFill, game.Progress);
        pauseButton.interactable = playing || counting || paused;
        pauseIcon.sprite = paused ? art.Play : art.Pause;
        for (int i = 0; i < 3; i++)
        {
            actionButtons[i].interactable = ready || playing;
            bool pressed = Time.unscaledTime - pressedAt[i] < .12f;
            actionButtons[i].image.sprite = art.Key;
            actionButtons[i].image.color = Color.Lerp(SealUiArt.InputColor(i), Color.white, pressed ? .20f : 0);
            actionButtons[i].transform.localScale = Vector3.one * (pressed ? .94f : 1);
        }
        SetText(assistText, "节拍强调  " + (game.AssistEnabled ? "开" : "关"));
        int offset = Mathf.RoundToInt(game.TimingOffsetMs);
        SetText(offsetText, "校准  " + (offset > 0 ? "+" : "") + offset + " ms");
        countdown.SetActive(counting); SetText(countdownText, game.CountdownLabel);
        lane.SetVisible(ready || playing || counting); lane.Refresh();
        if (finished)
        {
            SetText(gradeText, accuracy >= .9 ? "好极了！" : accuracy >= .7 ? "演出完成" : "再来一次？");
            SetText(resultScoreText, score.ToString("D6"));
            SetText(resultStatsText, judge == null ? "" : "Perfect  " + judge.PerfectCount + "    Good  " + judge.GoodCount + "    Miss  " + judge.MissCount +
                "\n" + (accuracy * 100).ToString("0.0") + "%    ·    最长连击 " + judge.MaxCombo);
            SetText(bestText, game.IsPractice ? "练习" : "最佳  " + game.BestScore.ToString("D6"));
        }
        if (state == SealGameState.Error) SetText(errorText, game.StatusMessage);
        float width = canvasRect.rect.width;
        if (Mathf.Abs(width - lastWidth) > 1) ApplyResponsiveLayout(width);
    }
    public void ShowJudgement(string label, string timing, bool success)
    {
        if (built) lane.ShowJudgement(label, timing, success);
    }
    private void Build()
    {
        font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Arial" }, 24);
        ownsFont = font != null;
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        art = new SealUiArt();
        var go = new GameObject("Seal Rhythm HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
        canvasRect = go.GetComponent<RectTransform>();
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
        if (EventSystem.current == null) ownedEventSystem = new GameObject("Seal Rhythm EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        BuildHeader(); BuildControls();
        shade = ImageObject("Menu shade", canvasRect, new Color(.20f, .28f, .31f, .16f)).gameObject;
        Stretch(shade.GetComponent<RectTransform>(), 0, 0, 136, 118);
        BuildReady(); BuildPaused(); BuildResults(); BuildError();
        var count = ImageObject("Count in", canvasRect, Color.white, art.Key);
        countdown = count.gameObject; Place(count.rectTransform, new Vector2(.5f, .65f), Vector2.zero, new Vector2(88, 88));
        countdownText = Label("Count in number", count.transform, "", 42, Ink);
        Stretch(countdownText.rectTransform, 8, 8, 4, 10);
        countdown.SetActive(false); built = true;
        Canvas.ForceUpdateCanvases(); ApplyResponsiveLayout(canvasRect.rect.width);
    }
    private void BuildHeader()
    {
        var surface = ImageObject("Score and rhythm paper", canvasRect, new Color32(255, 249, 236, 246));
        var header = surface.rectTransform;
        PlaceStretchX(header, 12, new Vector2(0, 1), -69, 138);
        scoreText = Label("Score", header, "000000", 25, Ink, TextAnchor.MiddleLeft);
        Place(scoreText.rectTransform, new Vector2(0, 1), new Vector2(28, -25), new Vector2(132, 36), new Vector2(0, .5f));
        comboText = Label("Combo", header, "0 连击", 17, Muted, TextAnchor.MiddleLeft);
        Place(comboText.rectTransform, new Vector2(0, 1), new Vector2(168, -25), new Vector2(130, 30), new Vector2(0, .5f));
        accuracyText = Label("Accuracy", header, "—", 17, Muted, TextAnchor.MiddleLeft);
        Place(accuracyText.rectTransform, new Vector2(0, 1), new Vector2(304, -25), new Vector2(100, 30), new Vector2(0, .5f));
        timeText = Label("Time", header, "0:00 / 1:46", 16, Muted, TextAnchor.MiddleRight);
        Place(timeText.rectTransform, new Vector2(1, 1), new Vector2(-94, -25), new Vector2(170, 32), new Vector2(1, .5f));
        pauseButton = MakeButton("Pause", header, "", () => game.TogglePause());
        pauseButton.image.color = Color.clear;
        Place(pauseButton.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(-46, -29), new Vector2(47, 43));
        pauseIcon = Icon("Pause icon", pauseButton.transform, art.Pause, 20);
        laneRect = RectObject("Beat lane", header);
        Place(laneRect, new Vector2(.5f, 1), new Vector2(0, -84), new Vector2(640, 86));
        lane = laneRect.gameObject.AddComponent<SealBeatLane>();
        lane.Initialize(game, laneRect, font, art.Tick, art.Seal, art.Left, art.Down, art.Right, art.Ring);
        var track = ImageObject("Song progress track", header, new Color(Ink.r, Ink.g, Ink.b, .12f));
        PlaceStretchX(track.rectTransform, 26, Vector2.zero, 8, 2);
        var fill = ImageObject("Song progress", track.transform, Coral);
        Stretch(fill.rectTransform, 0, 0, 0, 0); progressFill = fill.rectTransform;
    }
    private void BuildControls()
    {
        controls = RectObject("Arrow controls", canvasRect);
        Place(controls, new Vector2(.5f, 0), new Vector2(0, 62), new Vector2(344, 84));
        int[] order = { 1, 0, 2 }; Sprite[] arrows = { art.Down, art.Left, art.Right };
        for (int i = 0; i < 3; i++)
        {
            int input = order[i]; var button = MakeButton("Action " + input, controls, "", null);
            button.image.color = SealUiArt.InputColor(input);
            Place(button.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2((i - 1) * 112, 0), new Vector2(96, 76));
            Icon("Direction", button.transform, arrows[input], 35);
            var trigger = button.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            entry.callback.AddListener(data =>
            {
                if (button.interactable && data is PointerEventData pointer && pointer.button == PointerEventData.InputButton.Left) game.SubmitInput(input);
            });
            trigger.triggers.Add(entry); actionButtons[input] = button;
        }
    }
    private void BuildReady()
    {
        readyRect = MakeCard("Ready", 290); readyPanel = readyRect.gameObject;
        var seal = Icon("Seal emblem", readyRect, art.Seal, 42); seal.rectTransform.anchoredPosition = new Vector2(0, 104);
        CardLabel(readyRect, "Title", "海豹节奏队", 32, Ink, 66, 50);
        PlaceCardButton(MakeButton("Start show", readyRect, "开始", () => game.StartGame(false), true), 0, 2, 132, 50);
        PlaceCardButton(MakeButton("Start practice", readyRect, "练习", () => game.StartGame(true)), 1, 2, 132, 50);
        var assist = MakeButton("Toggle assist", readyRect, "", () => game.ToggleAssist());
        Place(assist.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(116, -226), new Vector2(158, 42));
        assistText = assist.GetComponentInChildren<Text>(); assistText.fontSize = 15;
        offsetText = Label("Input offset", readyRect, "", 15, Muted);
        Place(offsetText.rectTransform, new Vector2(1, 1), new Vector2(-126, -226), new Vector2(128, 34));
        var minus = MakeButton("Offset minus", readyRect, "−", () => game.SetTimingOffset(game.TimingOffsetMs - 10));
        Place(minus.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(-210, -226), new Vector2(34, 38));
        var plus = MakeButton("Offset plus", readyRect, "+", () => game.SetTimingOffset(game.TimingOffsetMs + 10));
        Place(plus.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(-44, -226), new Vector2(34, 38));
    }
    private void BuildPaused()
    {
        pauseRect = MakeCard("Paused", 218); pausePanel = pauseRect.gameObject;
        CardLabel(pauseRect, "Paused title", "暂停", 30, Ink, 32, 50);
        PlaceCardButton(MakeButton("Resume", pauseRect, "继续", () => game.TogglePause(), true), 0, 3, 123, 48);
        PlaceCardButton(MakeButton("Restart paused", pauseRect, "重来", () => game.RestartGame()), 1, 3, 123, 48);
        PlaceCardButton(MakeButton("Title paused", pauseRect, "返回", () => game.ReturnToTitle()), 2, 3, 123, 48);
    }
    private void BuildResults()
    {
        resultsRect = MakeCard("Results", 332); resultsPanel = resultsRect.gameObject;
        gradeText = CardLabel(resultsRect, "Grade", "", 30, Ink, 24, 46);
        resultScoreText = CardLabel(resultsRect, "Result score", "", 38, Coral, 76, 52);
        resultStatsText = CardLabel(resultsRect, "Result counts", "", 17, Muted, 142, 60);
        bestText = CardLabel(resultsRect, "Best score", "", 15, Muted, 210, 28);
        PlaceCardButton(MakeButton("Replay", resultsRect, "再来", () => game.RestartGame(), true), 0, 2, 262, 46);
        PlaceCardButton(MakeButton("Title results", resultsRect, "返回", () => game.ReturnToTitle()), 1, 2, 262, 46);
    }
    private void BuildError()
    {
        errorRect = MakeCard("Error", 230); errorPanel = errorRect.gameObject;
        CardLabel(errorRect, "Error title", "无法开始", 28, Coral, 22, 46);
        errorText = CardLabel(errorRect, "Error details", "", 17, Ink, 76, 64);
        PlaceCardButton(MakeButton("Retry scene", errorRect, "重试", ReloadCurrentScene, true), 0, 1, 162, 46);
    }
    private static void ReloadCurrentScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#else
        UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
#endif
    }
    private void ApplyResponsiveLayout(float width)
    {
        if (width <= 0) return; lastWidth = width;
        float cardWidth = Mathf.Min(510, width - 36);
        foreach (var card in new[] { readyRect, pauseRect, resultsRect, errorRect }) card.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cardWidth);
        laneRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(680, width - 100));
        accuracyText.gameObject.SetActive(width >= 720); timeText.gameObject.SetActive(width >= 620);
        controls.localScale = Vector3.one * Mathf.Min(1, (width - 24) / 344);
    }
    private RectTransform MakeCard(string name, float height)
    {
        var card = ImageObject(name, canvasRect, Color.white, art.Paper); card.raycastTarget = true;
        Place(card.rectTransform, new Vector2(.5f, .55f), Vector2.zero, new Vector2(510, height)); return card.rectTransform;
    }
    private Text CardLabel(RectTransform parent, string name, string text, int size, Color color, float top, float height)
    {
        var label = Label(name, parent, text, size, color); PlaceStretchX(label.rectTransform, 26, new Vector2(0, 1), -top - height / 2, height); return label;
    }
    private static void PlaceCardButton(Button button, int index, int count, float top, float height)
    {
        var rect = button.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(.08f + index * .84f / count, 1); rect.anchorMax = new Vector2(.08f + (index + 1) * .84f / count, 1);
        rect.offsetMin = new Vector2(index == 0 ? 0 : 5, -top - height); rect.offsetMax = new Vector2(index == count - 1 ? 0 : -5, -top);
    }
    private Button MakeButton(string name, Transform parent, string text, Action action, bool primary = false)
    {
        var image = ImageObject(name, parent, Color.white, primary ? art.PressedKey : art.Key); image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = new Color(1, .97f, .92f); colors.pressedColor = new Color(.88f, .90f, .90f);
        colors.disabledColor = new Color(1, 1, 1, .46f); colors.fadeDuration = .06f; button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        if (action != null) button.onClick.AddListener(() => { action(); if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); });
        var label = Label(name + " label", button.transform, text, 20, Ink); Stretch(label.rectTransform, 8, 8, 4, 10); return button;
    }
    private Image Icon(string name, Transform parent, Sprite sprite, float size)
    {
        var icon = ImageObject(name, parent, Color.white, sprite, false); icon.preserveAspect = true;
        Place(icon.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(size, size)); return icon;
    }
    private Text Label(string name, Transform parent, string value, int size, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        var label = RectObject(name, parent).gameObject.AddComponent<Text>(); label.font = font; label.fontSize = size; label.color = color; label.alignment = alignment;
        label.text = value; label.raycastTarget = false; label.resizeTextForBestFit = true; label.resizeTextMinSize = Mathf.Max(11, size - 5); label.resizeTextMaxSize = size;
        label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate; return label;
    }
    private static Image ImageObject(string name, Transform parent, Color color, Sprite sprite = null, bool sliced = true)
    {
        var image = RectObject(name, parent).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; image.sprite = sprite;
        if (sprite != null && sliced && sprite.border != Vector4.zero)
        { image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 3; }
        return image;
    }
    private static RectTransform RectObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go.GetComponent<RectTransform>();
    }
    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
    {
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot ?? new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size;
    }
    private static void PlaceStretchX(RectTransform rect, float inset, Vector2 anchor, float y, float height)
    {
        rect.anchorMin = new Vector2(0, anchor.y); rect.anchorMax = new Vector2(1, anchor.y); rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(0, y); rect.sizeDelta = new Vector2(-inset * 2, height);
    }
    private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
    }
    private static void SetFill(RectTransform rect, float value) { rect.anchorMax = new Vector2(Mathf.Clamp01(value), 1); rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static void SetText(Text label, string value) { if (label.text != value) label.text = value ?? ""; }
    private static string FormatTime(float seconds) { int whole = Mathf.Max(0, Mathf.FloorToInt(seconds)); return whole / 60 + ":" + (whole % 60).ToString("D2"); }
    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject); if (ownedEventSystem != null) Destroy(ownedEventSystem);
        art?.Dispose(); if (ownsFont && font != null) Destroy(font);
    }
}
