using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>The seal stage stays in the scene; this canvas only supplies game controls and feedback.</summary>
public sealed class SealRhythmHud : MonoBehaviour
{
    private static readonly Color Navy = new Color32(20, 47, 66, 255);
    private static readonly Color Panel = new Color32(21, 51, 69, 242);
    private static readonly Color Cream = new Color32(255, 248, 227, 255);
    private static readonly Color Teal = new Color32(92, 223, 193, 255);
    private static readonly Color Muted = new Color32(175, 204, 211, 255);
    private static readonly Color Amber = new Color32(255, 207, 130, 255);

    private RhythmGame game;
    private Canvas canvas;
    private RectTransform canvasRect;
    private Font font;
    private bool ownsFont;
    private Sprite roundedSprite;
    private Texture2D roundedTexture;
    private GameObject ownedEventSystem;
    private GameObject readyPanel, pausePanel, resultsPanel, errorPanel, shade;
    private RectTransform readyRect, pauseRect, resultsRect, errorRect;
    private Text scoreText, comboText, accuracyText, modeText, timeText, statusText;
    private Text assistText, offsetText, countdownText, cueText, gradeText, resultScoreText, resultStatsText, bestText, errorText;
    private RectTransform progressFill, cueFill;
    private Button pauseButton;
    private readonly Button[] actionButtons = new Button[3];
    private GameObject cue, countdown;
    private CanvasGroup judgementGroup;
    private Text judgementText, timingText;
    private float judgementShownAt = -10f;
    private bool built;
    private bool stateKnown;
    private SealGameState displayedState;
    private float lastWidth = -1;

    public void Initialize(RhythmGame rhythmGame)
    {
        game = rhythmGame;
        if (!built)
            Build();
        Refresh();
    }

    public void Refresh()
    {
        if (!built || game == null)
            return;

        var state = game.State;
        bool playing = state == SealGameState.Playing;
        bool ready = state == SealGameState.Ready;
        bool paused = state == SealGameState.Paused;
        bool counting = state == SealGameState.CountIn;
        bool finished = state == SealGameState.Results;

        if (!stateKnown || displayedState != state)
        {
            stateKnown = true;
            displayedState = state;
            readyPanel.SetActive(ready);
            pausePanel.SetActive(paused);
            resultsPanel.SetActive(finished);
            errorPanel.SetActive(state == SealGameState.Error);
            shade.SetActive(ready || paused || finished || state == SealGameState.Error);
            countdown.SetActive(counting);
            judgementGroup.alpha = 0;
            judgementShownAt = -10f;
            // Space belongs to the rhythm input, never to the last clicked menu button.
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        var judge = game.Judge;
        int score = judge != null ? judge.Score : 0;
        double accuracy = judge != null ? judge.Accuracy : 0;
        double accuracyDenominator = judge != null ? judge.ResolvedCount + judge.StrayCount * .25d : 0;
        double liveAccuracy = judge != null && accuracyDenominator > 0
            ? (judge.PerfectCount + judge.GoodCount * .65d) / accuracyDenominator
            : 0;
        SetText(scoreText, score.ToString("D6"));
        SetText(comboText, (judge != null ? judge.Combo : 0) + " 连击");
        SetText(accuracyText, accuracyDenominator > 0 ? (liveAccuracy * 100d).ToString("0.0") + "% 准确率" : "— 准确率");
        SetText(modeText, game.IsPractice ? "节奏练习" : "正式演出");
        SetText(timeText, FormatTime(game.SongTime) + " / " + FormatTime(game.SongDuration));
        SetText(statusText, game.StatusMessage ?? string.Empty);
        statusText.gameObject.SetActive(!playing);
        SetFill(progressFill, game.Progress);
        pauseButton.interactable = playing || paused || counting;
        SetText(pauseButton.GetComponentInChildren<Text>(), paused ? "继续" : "暂停  Esc");
        for (int i = 0; i < actionButtons.Length; ++i)
            actionButtons[i].interactable = ready || playing;

        SetText(assistText, "节拍辅助  " + (game.AssistEnabled ? "开" : "关"));
        int offset = Mathf.RoundToInt(game.TimingOffsetMs);
        SetText(offsetText, "输入偏移  " + (offset > 0 ? "+" : string.Empty) + offset + " ms");
        countdown.SetActive(counting);
        SetText(countdownText, game.CountdownLabel ?? string.Empty);
        bool showCue = game.AssistEnabled && playing && game.HasUpcomingNote;
        cue.SetActive(showCue);
        if (showCue)
        {
            SetText(cueText, "下一拍   " + game.NextActionLabel);
            SetFill(cueFill, game.NextActionProgress);
        }

        if (finished)
        {
            SetText(gradeText, accuracy >= .9f ? "好极了！" : accuracy >= .7f ? "合格！" : "再练练！");
            gradeText.color = accuracy >= .7f ? Teal : Amber;
            SetText(resultScoreText, score.ToString("D6") + " 分   ·   " + (accuracy * 100f).ToString("0.0") + "%");
            SetText(resultStatsText, judge == null ? string.Empty :
                "Perfect  " + judge.PerfectCount + "     Good  " + judge.GoodCount + "     Miss  " + judge.MissCount +
                "\n多按  " + judge.StrayCount + "     最高连击  " + judge.MaxCombo);
            SetText(bestText, game.IsPractice ? "练习模式 · 不计入个人最佳" : "个人最佳  " + game.BestScore.ToString("D6"));
        }
        if (state == SealGameState.Error)
            SetText(errorText, game.StatusMessage ?? "关卡暂时无法开始，请返回后重试。");

        float age = Time.unscaledTime - judgementShownAt;
        judgementGroup.alpha = playing && age < .8f ? 1f - Mathf.InverseLerp(.5f, .8f, age) : 0f;
        float width = canvasRect.rect.width;
        if (Mathf.Abs(width - lastWidth) > 1f)
            ApplyResponsiveLayout(width);
    }

    public void ShowJudgement(string label, string timing, bool success)
    {
        if (!built)
            return;
        judgementShownAt = Time.unscaledTime;
        SetText(judgementText, label ?? string.Empty);
        SetText(timingText, timing ?? string.Empty);
        judgementText.color = success ? Teal : Amber;
        judgementGroup.alpha = 1;
    }

    private void Build()
    {
        font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 24);
        ownsFont = font != null;
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        roundedSprite = CreateRoundedSprite();

        var canvasObject = new GameObject("Seal Rhythm HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasRect = canvasObject.GetComponent<RectTransform>();
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        if (EventSystem.current == null)
            ownedEventSystem = new GameObject("Seal Rhythm EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        BuildHeader();
        BuildControls();
        shade = ImageObject("Menu shade", canvasRect, new Color(0.03f, .09f, .13f, .28f), false).gameObject;
        Stretch(shade.GetComponent<RectTransform>(), 0, 0, 0, 122);
        // Menus use their own card surfaces; the surrounding stage remains visible.
        shade.GetComponent<Image>().raycastTarget = false;
        BuildReady();
        BuildPaused();
        BuildResults();
        BuildError();

        countdown = ImageObject("Count in", canvasRect, Panel, true).gameObject;
        Place(countdown.GetComponent<RectTransform>(), new Vector2(.5f, .62f), Vector2.zero, new Vector2(220, 114));
        countdownText = Label("Count in label", countdown.transform, string.Empty, 52, Cream, TextAnchor.MiddleCenter);
        Stretch(countdownText.rectTransform, 8, 8, 8, 8);
        countdown.GetComponent<Image>().raycastTarget = false;
        countdown.SetActive(false);

        var judgement = RectObject("Judgement", canvasRect);
        Place(judgement, new Vector2(.78f, .66f), Vector2.zero, new Vector2(360, 80));
        judgementGroup = judgement.gameObject.AddComponent<CanvasGroup>();
        judgementGroup.blocksRaycasts = false;
        judgementGroup.interactable = false;
        judgementGroup.alpha = 0;
        judgementText = Label("Judgement label", judgement, string.Empty, 35, Teal, TextAnchor.MiddleCenter);
        Stretch(judgementText.rectTransform, 0, 0, 0, 30);
        AddShadow(judgementText, new Vector2(1, -2));
        timingText = Label("Timing", judgement, string.Empty, 18, Cream, TextAnchor.MiddleCenter);
        Place(timingText.rectTransform, new Vector2(.5f, 0), new Vector2(0, 12), new Vector2(500, 28));
        AddShadow(timingText, new Vector2(1, -1));
        built = true;
        Canvas.ForceUpdateCanvases();
        ApplyResponsiveLayout(canvasRect.rect.width);
    }

    private void BuildHeader()
    {
        var header = ImageObject("Score bar", canvasRect, Panel, false);
        header.raycastTarget = false;
        PlaceStretchX(header.rectTransform, 0, new Vector2(0, 1), 0, -40, 80);
        var scoreLabel = Label("Score caption", header.transform, "得 分", 13, Muted, TextAnchor.MiddleLeft);
        Place(scoreLabel.rectTransform, new Vector2(0, .5f), new Vector2(28, 18), new Vector2(160, 20), new Vector2(0, .5f));
        scoreText = Label("Score", header.transform, "000000", 30, Cream, TextAnchor.MiddleLeft);
        Place(scoreText.rectTransform, new Vector2(0, .5f), new Vector2(26, -8), new Vector2(170, 40), new Vector2(0, .5f));
        comboText = Label("Combo", header.transform, "0 连击", 25, Teal, TextAnchor.MiddleLeft);
        Place(comboText.rectTransform, new Vector2(.22f, .5f), new Vector2(0, 0), new Vector2(180, 40), new Vector2(0, .5f));
        accuracyText = Label("Accuracy", header.transform, "0.0% 准确率", 21, Cream, TextAnchor.MiddleLeft);
        Place(accuracyText.rectTransform, new Vector2(.43f, .5f), Vector2.zero, new Vector2(215, 40), new Vector2(0, .5f));
        modeText = Label("Mode", header.transform, "正式演出", 17, Muted, TextAnchor.MiddleRight);
        Place(modeText.rectTransform, new Vector2(1, .5f), new Vector2(-156, 13), new Vector2(170, 26), new Vector2(1, .5f));
        timeText = Label("Song time", header.transform, "0:00 / 0:00", 16, Muted, TextAnchor.MiddleRight);
        Place(timeText.rectTransform, new Vector2(1, .5f), new Vector2(-156, -15), new Vector2(180, 25), new Vector2(1, .5f));
        pauseButton = MakeButton("Pause", header.transform, "暂停  Esc", () => game.TogglePause(), false);
        Place(pauseButton.GetComponent<RectTransform>(), new Vector2(1, .5f), new Vector2(-78, 0), new Vector2(116, 44));

        var progress = ImageObject("Song progress track", canvasRect, new Color32(35, 80, 96, 255), false);
        PlaceStretchX(progress.rectTransform, 0, new Vector2(0, 1), 0, -82, 4);
        var fill = ImageObject("Song progress", progress.transform, Teal, false);
        Stretch(fill.rectTransform, 0, 0, 0, 0);
        fill.rectTransform.anchorMax = new Vector2(0, 1);
        progressFill = fill.rectTransform;
        progress.raycastTarget = fill.raycastTarget = false;

        statusText = Label("Status", canvasRect, string.Empty, 17, Navy, TextAnchor.MiddleCenter);
        PlaceStretchX(statusText.rectTransform, 24, new Vector2(0, 1), 0, -107, 34);
        AddShadow(statusText, new Vector2(0, -1), new Color(1, 1, 1, .6f));
    }

    private void BuildControls()
    {
        var footer = ImageObject("Action bar", canvasRect, Panel, false);
        footer.raycastTarget = false;
        PlaceStretchX(footer.rectTransform, 0, Vector2.zero, 0, 59, 118);
        var caption = Label("Controls caption", footer.transform, "听口令，跟上海豹的节奏", 15, Muted, TextAnchor.MiddleCenter);
        PlaceStretchX(caption.rectTransform, 16, new Vector2(0, 1), 0, -15, 26);

        string[] labels = { "点头   S / ↓ / 空格", "左翻身 / 翻滚   A / ←", "右翻身 / 翻滚   D / →" };
        int[] visualOrder = { 1, 0, 2 };
        for (int i = 0; i < visualOrder.Length; ++i)
        {
            int action = visualOrder[i];
            var button = MakeButton("Action " + action, footer.transform, labels[action], null, action == 0);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(i / 3f, 0);
            rect.anchorMax = new Vector2((i + 1) / 3f, 0);
            rect.pivot = new Vector2(.5f, 0);
            rect.offsetMin = new Vector2(i == 0 ? 24 : 7, 18);
            rect.offsetMax = new Vector2(i == 2 ? -24 : -7, 78);
            button.GetComponentInChildren<Text>().fontSize = 22;
            // Press on contact, so a long touch never adds release-time latency.
            var trigger = button.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            entry.callback.AddListener(data =>
            {
                var pointer = data as PointerEventData;
                if (button.interactable && pointer != null && pointer.button == PointerEventData.InputButton.Left)
                    game.SubmitInput(action);
            });
            trigger.triggers.Add(entry);
            actionButtons[action] = button;
        }

        cue = ImageObject("Upcoming cue", canvasRect, Panel, true).gameObject;
        Place(cue.GetComponent<RectTransform>(), new Vector2(.5f, 0), new Vector2(0, 147), new Vector2(330, 42));
        cue.GetComponent<Image>().raycastTarget = false;
        var cueTrack = ImageObject("Cue track", cue.transform, new Color32(46, 89, 101, 255), false);
        cueTrack.rectTransform.anchorMin = new Vector2(0, 0);
        cueTrack.rectTransform.anchorMax = new Vector2(1, 0);
        cueTrack.rectTransform.pivot = new Vector2(.5f, 0);
        cueTrack.rectTransform.offsetMin = new Vector2(12, 0);
        cueTrack.rectTransform.offsetMax = new Vector2(-12, 3);
        cueTrack.raycastTarget = false;
        var cueProgress = ImageObject("Cue progress", cueTrack.transform, Teal, false);
        Stretch(cueProgress.rectTransform, 0, 0, 0, 0);
        cueProgress.raycastTarget = false;
        cueFill = cueProgress.rectTransform;
        cueText = Label("Upcoming action", cue.transform, string.Empty, 20, Cream, TextAnchor.MiddleCenter);
        Stretch(cueText.rectTransform, 10, 10, 1, 4);
        cue.SetActive(false);
    }

    private void BuildReady()
    {
        readyRect = MakeCard("Ready", 330);
        readyPanel = readyRect.gameObject;
        CardLabel(readyRect, "Ready eyebrow", "RHYTHM HEAVEN  /  SEAL TEAM", 13, Teal, 20, 24);
        CardLabel(readyRect, "Title", "海豹节奏队", 38, Cream, 46, 55);
        CardLabel(readyRect, "Instructions", "听口令，看同伴；左右键随节拍翻身或翻滚，下键点头。", 21, Cream, 105, 32);
        CardLabel(readyRect, "Keys", "S / ↓ / 空格 点头     A / ← 向左     D / → 向右", 17, Muted, 140, 28);
        var play = MakeButton("Start show", readyRect, "正式演出", () => game.StartGame(false), true);
        PlaceCardButton(play, 0, 2, 185, 49);
        var practice = MakeButton("Start practice", readyRect, "节奏练习", () => game.StartGame(true), false);
        PlaceCardButton(practice, 1, 2, 185, 49);

        var assist = MakeButton("Toggle assist", readyRect, string.Empty, () => game.ToggleAssist(), false);
        var assistRect = assist.GetComponent<RectTransform>();
        assistRect.anchorMin = new Vector2(.07f, 1);
        assistRect.anchorMax = new Vector2(.35f, 1);
        assistRect.pivot = new Vector2(.5f, 1);
        assistRect.offsetMin = new Vector2(0, -286);
        assistRect.offsetMax = new Vector2(0, -246);
        assistText = assist.GetComponentInChildren<Text>();
        assistText.fontSize = 17;

        offsetText = Label("Input offset", readyRect, string.Empty, 17, Cream, TextAnchor.MiddleCenter);
        var offsetRect = offsetText.rectTransform;
        offsetRect.anchorMin = new Vector2(.44f, 1);
        offsetRect.anchorMax = new Vector2(.86f, 1);
        offsetRect.offsetMin = new Vector2(0, -280);
        offsetRect.offsetMax = new Vector2(0, -246);
        var earlier = MakeButton("Offset minus", readyRect, "−", () => game.SetTimingOffset(game.TimingOffsetMs - 10), false);
        Place(earlier.GetComponent<RectTransform>(), new Vector2(.41f, 1), new Vector2(0, -266), new Vector2(40, 40));
        var later = MakeButton("Offset plus", readyRect, "+", () => game.SetTimingOffset(game.TimingOffsetMs + 10), false);
        Place(later.GetComponent<RectTransform>(), new Vector2(.89f, 1), new Vector2(0, -266), new Vector2(40, 40));
        CardLabel(readyRect, "Offset help", "每次调整 10 ms · 正数表示输入较晚 · 先试试下方动作按钮", 14, Muted, 294, 23);
    }

    private void BuildPaused()
    {
        pauseRect = MakeCard("Paused", 238);
        pausePanel = pauseRect.gameObject;
        CardLabel(pauseRect, "Paused title", "休息一下", 36, Cream, 24, 54);
        CardLabel(pauseRect, "Paused help", "准备好后继续，音乐和判定会从暂停处恢复。", 19, Muted, 82, 35);
        var resume = MakeButton("Resume", pauseRect, "继续演出", () => game.TogglePause(), true);
        PlaceCardButton(resume, 0, 3, 143, 52);
        var restart = MakeButton("Restart paused", pauseRect, "重新开始", () => game.RestartGame(), false);
        PlaceCardButton(restart, 1, 3, 143, 52);
        var title = MakeButton("Title paused", pauseRect, "返回标题", () => game.ReturnToTitle(), false);
        PlaceCardButton(title, 2, 3, 143, 52);
    }

    private void BuildResults()
    {
        resultsRect = MakeCard("Results", 352);
        resultsPanel = resultsRect.gameObject;
        CardLabel(resultsRect, "Result eyebrow", "演 出 结 束", 14, Muted, 18, 25);
        gradeText = CardLabel(resultsRect, "Grade", string.Empty, 38, Teal, 43, 54);
        resultScoreText = CardLabel(resultsRect, "Result score", string.Empty, 25, Cream, 100, 36);
        resultStatsText = CardLabel(resultsRect, "Result counts", string.Empty, 20, Cream, 144, 68);
        resultStatsText.lineSpacing = 1.25f;
        bestText = CardLabel(resultsRect, "Best score", string.Empty, 17, Muted, 221, 30);
        var restart = MakeButton("Replay", resultsRect, "再来一次", () => game.RestartGame(), true);
        PlaceCardButton(restart, 0, 2, 277, 49);
        var title = MakeButton("Title results", resultsRect, "返回标题", () => game.ReturnToTitle(), false);
        PlaceCardButton(title, 1, 2, 277, 49);
    }

    private void BuildError()
    {
        errorRect = MakeCard("Error", 256);
        errorPanel = errorRect.gameObject;
        CardLabel(errorRect, "Error title", "暂时无法演出", 32, Amber, 24, 52);
        errorText = CardLabel(errorRect, "Error details", string.Empty, 18, Cream, 82, 74);
        var title = MakeButton("Retry scene", errorRect, "重新加载关卡", ReloadCurrentScene, true);
        PlaceCardButton(title, 0, 1, 184, 48);
    }

    private static void ReloadCurrentScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
#if UNITY_EDITOR
        // The project can also be played directly before its build scene list is configured.
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path,
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#else
        UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
#endif
    }

    private void ApplyResponsiveLayout(float width)
    {
        if (width <= 0)
            return;
        lastWidth = width;
        float cardWidth = Mathf.Min(840, width - 36);
        readyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cardWidth);
        pauseRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cardWidth);
        resultsRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cardWidth);
        errorRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cardWidth);
        bool compact = width < 1050;
        modeText.gameObject.SetActive(!compact);
        timeText.gameObject.SetActive(!compact);
        comboText.fontSize = compact ? 21 : 25;
        accuracyText.fontSize = compact ? 18 : 21;
        // Fit the status and menus inside 4:3 and narrower game windows.
        if (width < 790)
        {
            comboText.rectTransform.anchoredPosition = new Vector2(15, 0);
            accuracyText.rectTransform.anchoredPosition = new Vector2(35, 0);
            scoreText.fontSize = 25;
            pauseButton.GetComponent<RectTransform>().sizeDelta = new Vector2(90, 44);
            pauseButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-57, 0);
        }
        else
        {
            comboText.rectTransform.anchoredPosition = Vector2.zero;
            accuracyText.rectTransform.anchoredPosition = Vector2.zero;
            scoreText.fontSize = 30;
            pauseButton.GetComponent<RectTransform>().sizeDelta = new Vector2(116, 44);
            pauseButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-78, 0);
        }
    }

    private RectTransform MakeCard(string name, float height)
    {
        var card = ImageObject(name, canvasRect, Panel, true);
        Place(card.rectTransform, new Vector2(.5f, 1), new Vector2(0, -125), new Vector2(840, height), new Vector2(.5f, 1));
        var edge = ImageObject("Accent", card.transform, Teal, false);
        Place(edge.rectTransform, new Vector2(.5f, 1), new Vector2(0, -1), new Vector2(68, 3), new Vector2(.5f, 1));
        edge.raycastTarget = false;
        return card.rectTransform;
    }

    private Text CardLabel(RectTransform parent, string name, string value, int size, Color color, float top, float height)
    {
        var label = Label(name, parent, value, size, color, TextAnchor.MiddleCenter);
        PlaceStretchX(label.rectTransform, 24, new Vector2(0, 1), 0, -top - height / 2, height);
        return label;
    }

    private void PlaceCardButton(Button button, int index, int count, float top, float height)
    {
        var rect = button.GetComponent<RectTransform>();
        float left = .07f + index * (.86f / count);
        float right = .07f + (index + 1) * (.86f / count);
        rect.anchorMin = new Vector2(left, 1);
        rect.anchorMax = new Vector2(right, 1);
        rect.pivot = new Vector2(.5f, 1);
        rect.offsetMin = new Vector2(index == 0 ? 0 : 6, -top - height);
        rect.offsetMax = new Vector2(index == count - 1 ? 0 : -6, -top);
    }

    private Button MakeButton(string name, Transform parent, string value, Action action, bool primary)
    {
        var image = ImageObject(name, parent, primary ? Teal : new Color32(44, 78, 95, 255), true);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(.87f, 1, .97f, 1);
        colors.pressedColor = new Color(.64f, .82f, .78f, 1);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(.55f, .61f, .63f, .55f);
        colors.fadeDuration = .07f;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        if (action != null)
            button.onClick.AddListener(() =>
            {
                action();
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(null);
            });
        var label = Label(name + " label", button.transform, value, 21, primary ? Navy : Cream, TextAnchor.MiddleCenter);
        Stretch(label.rectTransform, 8, 8, 3, 3);
        return button;
    }

    private Text Label(string name, Transform parent, string value, int size, Color color, TextAnchor alignment)
    {
        var rect = RectObject(name, parent);
        var label = rect.gameObject.AddComponent<Text>();
        label.font = font;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.text = value;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = Mathf.Max(11, size - 7);
        label.resizeTextMaxSize = size;
        return label;
    }

    private Image ImageObject(string name, Transform parent, Color color, bool rounded)
    {
        var rect = RectObject(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        if (rounded)
        {
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
        }
        return image;
    }

    private static RectTransform RectObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot ?? new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void PlaceStretchX(RectTransform rect, float inset, Vector2 anchor, float x, float y, float height)
    {
        rect.anchorMin = new Vector2(0, anchor.y);
        rect.anchorMax = new Vector2(1, anchor.y);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(-inset * 2, height);
    }

    private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetFill(RectTransform rect, float value)
    {
        rect.anchorMax = new Vector2(Mathf.Clamp01(value), 1);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void SetText(Text label, string value)
    {
        if (label.text != value)
            label.text = value;
    }

    private static string FormatTime(float seconds)
    {
        int whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return (whole / 60) + ":" + (whole % 60).ToString("D2");
    }

    private static void AddShadow(Text label, Vector2 distance, Color? color = null)
    {
        var shadow = label.gameObject.AddComponent<Shadow>();
        shadow.effectDistance = distance;
        shadow.effectColor = color ?? new Color(0.03f, .1f, .14f, .8f);
    }

    private Sprite CreateRoundedSprite()
    {
        const int size = 32;
        const float radius = 10;
        roundedTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        roundedTexture.name = "Seal HUD rounded corners";
        roundedTexture.filterMode = FilterMode.Bilinear;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; ++y)
        for (int x = 0; x < size; ++x)
        {
            float dx = Mathf.Max(radius - x - .5f, x + .5f - (size - radius));
            float dy = Mathf.Max(radius - y - .5f, y + .5f - (size - radius));
            float alpha = 1 - Mathf.Clamp01(new Vector2(Mathf.Max(0, dx), Mathf.Max(0, dy)).magnitude - radius + .5f);
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
        }
        roundedTexture.SetPixels32(pixels);
        roundedTexture.Apply(false, true);
        return Sprite.Create(roundedTexture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect,
            new Vector4(11, 11, 11, 11));
    }

    private void OnDestroy()
    {
        if (canvas != null)
            Destroy(canvas.gameObject);
        if (ownedEventSystem != null)
            Destroy(ownedEventSystem);
        if (roundedSprite != null)
            Destroy(roundedSprite);
        if (roundedTexture != null)
            Destroy(roundedTexture);
        if (ownsFont && font != null)
            Destroy(font);
    }
}
