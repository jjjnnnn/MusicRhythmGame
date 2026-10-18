using System;
using System.Collections.Generic;
using SonicBloom.Koreo;
using SonicBloom.Koreo.Players;
using SealRhythm;
using UnityEngine;
using UnityEngine.UI;

public enum SealGameState { Ready, CountIn, Playing, Paused, Results, Error }

/// <summary>Full-chart judgement and a restartable, audio-synchronized level lifecycle.</summary>
[DefaultExecutionOrder(-100)]
public class RhythmGame : MonoBehaviour
{
    [EventID] public string koreoEventID = "haibaoEventID";
    public string[] eventTextName = { "diantou", "left", "right" };
    public Animator characterAnimator;
    public string[] aniName = { "diantou", "left", "right" };
    [Range(60, 200)] public int hitWindowRangeInMS = 120;
    [Range(20, 100)] public int perfectWindowInMS = 55;
    public float beatsPerMinute = 133f;
    // Keep existing serialized references while the new HUD supplies feedback.
    public GameObject missTextPrefab, perfectTextPrefab;
    public Transform uiCanvasTransform;
    public Text scoreText;
    public KeyCode[] keys = { KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow };

    public SealGameState State { get; private set; } = SealGameState.Ready;
    public bool IsPractice { get; private set; }
    public bool AssistEnabled { get; private set; }
    public float TimingOffsetMs { get; private set; }
    public string StatusMessage { get; private set; } = "听口令，和伙伴一起打拍子。最右边的海豹是你！";
    public SealRhythmJudge Judge { get; private set; }
    public int BestScore { get; private set; }
    public float SongTime => songSample / (float)sampleRate;
    public float SongDuration => durationSamples / (float)sampleRate;
    public float Progress => durationSamples > 0 ? Mathf.Clamp01(songSample / (float)durationSamples) : 0;
    public string CountdownLabel => State == SealGameState.CountIn ? Mathf.Clamp(4 - countBeat, 1, 4).ToString() : "";
    public bool HasUpcomingNote => Judge != null && Judge.NextNoteIndex < Judge.TotalNotes && Judge.Notes[Judge.NextNoteIndex].Sample - AdjustedSample <= sampleRate * 1.25f;
    public string NextActionLabel => HasUpcomingNote ? ActionLabel(Judge.Notes[Judge.NextNoteIndex].Action) : "听节奏，准备好";
    public float NextActionProgress => HasUpcomingNote ? 1f - Mathf.Clamp01((Judge.Notes[Judge.NextNoteIndex].Sample - AdjustedSample) / (sampleRate * 1.25f)) : 0;

    private const string BestKey = "SealRhythm.BestScore.v1";
    private Koreography song;
    private SimpleMusicPlayer musicPlayer;
    private AudioSource music, feedbackAudio;
    private AudioClip[] tones;
    private SealRhythmHud hud;
    private readonly List<RhythmNote> chart = new List<RhythmNote>();
    private readonly Queue<int> inputs = new Queue<int>();
    private Animator[] actors;
    private int sampleRate = 44100, songSample, durationSamples, countBeat;
    private float countTimer;
    private SealGameState resumeState;
    private double playbackStartedAt, feedbackAt;
    private bool initialized;
    private Camera stageCamera;
    private Bounds stageBounds;
    private float framedAspect;
    private int AdjustedSample => songSample - Mathf.RoundToInt(TimingOffsetMs * sampleRate / 1000f);

    private void Awake()
    {
        musicPlayer = FindAnyObjectByType<SimpleMusicPlayer>();
        AssistEnabled = PlayerPrefs.GetInt("SealRhythm.Assist", 1) == 1;
        TimingOffsetMs = PlayerPrefs.GetFloat("SealRhythm.Offset", 0);
        BestScore = PlayerPrefs.GetInt(BestKey, 0);
    }

    private void Start()
    {
        hud = gameObject.AddComponent<SealRhythmHud>();
        hud.Initialize(this);
        if (scoreText != null) scoreText.gameObject.SetActive(false);
        actors = FindObjectsByType<Animator>();
        stageCamera = Camera.main;
        var background = GameObject.Find("Bg");
        if (background != null && background.TryGetComponent<SpriteRenderer>(out var renderer))
        { stageBounds = renderer.bounds; FrameStage(); }
        if (characterAnimator == null && GameObject.Find("player") != null)
            characterAnimator = GameObject.Find("player").GetComponent<Animator>();
        song = Resources.Load<Koreography>("Art/Level_cellanim/Kore/haibao");
        if (musicPlayer == null || song == null || song.SourceClip == null)
        { Fail("找不到音乐或 Koreography，请打开 GamePlay 场景。"); return; }
        music = musicPlayer.GetComponent<AudioSource>();
        if (music == null) { Fail("音乐播放器缺少 AudioSource。"); return; }
        music.loop = false;
        music.playOnAwake = false;
        musicPlayer.LoadSong(song, 0, false);
        sampleRate = song.SourceClip.frequency;
        durationSamples = song.SourceClip.samples;
        var track = song.GetTrackByID(koreoEventID);
        if (track != null) foreach (var note in track.GetAllEvents())
        {
            int action = Array.IndexOf(eventTextName, note.GetTextValue());
            if (action >= 0 && action < 3 && note.StartSample >= 0 && note.StartSample < durationSamples)
                chart.Add(new RhythmNote(note.StartSample, (SealAction)action));
        }
        if (chart.Count == 0) { Fail("海豹轨道没有有效节拍，请检查 haibaoEventID。"); return; }
        chart.Sort((a, b) => a.Sample.CompareTo(b.Sample));
        CreateFeedbackAudio();
        BuildJudge(false);
        initialized = true;
        hud.Refresh();
    }

    private void Update()
    {
        if (!initialized) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) { TogglePause(); return; }
        if (Input.GetKeyDown(KeyCode.R) && (State == SealGameState.Paused || State == SealGameState.Results)) { RestartGame(); return; }
        if (State == SealGameState.Ready && Input.GetKeyDown(KeyCode.Return)) { StartGame(false); return; }
        if (State == SealGameState.Results && Input.GetKeyDown(KeyCode.Return)) { RestartGame(); return; }
        if (State == SealGameState.CountIn)
        {
            countTimer += Time.unscaledDeltaTime;
            int beat = Mathf.FloorToInt(countTimer / (60f / beatsPerMinute));
            if (beat >= 4)
            {
                musicPlayer.Play(); playbackStartedAt = AudioSettings.dspTime;
                State = SealGameState.Playing;
                StatusMessage = IsPractice ? "练习：跟着提示完成前 32 拍。" : "听口令，保持节奏！";
            }
            else if (beat != countBeat) { countBeat = beat; PlayTone(3); }
        }
        if (State == SealGameState.Ready || State == SealGameState.Playing)
        {
            if (Pressed(0, KeyCode.S, KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.Space)) SubmitAction(0);
            if (Pressed(1, KeyCode.A, KeyCode.LeftArrow)) SubmitAction(1);
            if (Pressed(2, KeyCode.D, KeyCode.RightArrow)) SubmitAction(2);
        }
    }

    // Judge after Koreographer.Update: keyboard and pointer inputs share the same audio sample clock.
    private void LateUpdate()
    {
        if (stageCamera != null && Mathf.Abs(stageCamera.aspect - framedAspect) > .001f) FrameStage();
        if (State == SealGameState.Playing)
        {
            songSample = Math.Max(songSample, song.GetLatestSampleTime());
            while (inputs.Count > 0) Judge.Submit((SealAction)inputs.Dequeue(), AdjustedSample);
            Judge.Advance(AdjustedSample);
            bool ended = !music.isPlaying && AudioSettings.dspTime - playbackStartedAt > .3;
            if (songSample >= durationSamples || ended) FinishGame();
        }
        if (hud != null) hud.Refresh();
    }

    public void StartGame(bool practice)
    {
        if (!initialized) return;
        musicPlayer.Stop(); musicPlayer.LoadSong(song, 0, false); song.ResetTimings(); music.pitch = 1;
        IsPractice = practice; BuildJudge(practice); songSample = 0; inputs.Clear(); ResetActors();
        countTimer = 0; countBeat = 0; State = SealGameState.CountIn;
        StatusMessage = "预备，跟着四拍倒数进入音乐"; PlayTone(3);
    }
    private void BuildJudge(bool practice)
    {
        if (Judge != null) Judge.Judged -= OnJudged;
        durationSamples = practice ? Mathf.Min(song.SourceClip.samples, Mathf.RoundToInt(sampleRate * 32 * 60f / beatsPerMinute)) : song.SourceClip.samples;
        Judge = new SealRhythmJudge(chart.FindAll(n => n.Sample < durationSamples - sampleRate * .15f), sampleRate,
            perfectWindowInMS, practice ? Math.Max(180, hitWindowRangeInMS) : hitWindowRangeInMS);
        Judge.Judged += OnJudged;
    }
    public void SubmitAction(int action)
    {
        if (action < 0 || action > 2 || (State != SealGameState.Playing && State != SealGameState.Ready)) return;
        PlayAction(characterAnimator, aniName, action); PlayTone(action);
        if (State == SealGameState.Playing) inputs.Enqueue(action);
    }
    public void TogglePause()
    {
        if (State == SealGameState.Playing || State == SealGameState.CountIn)
        {
            resumeState = State;
            if (State == SealGameState.Playing) musicPlayer.Pause();
            inputs.Clear(); State = SealGameState.Paused; SetActorSpeed(0);
        }
        else if (State == SealGameState.Paused)
        {
            SetActorSpeed(1); State = resumeState;
            if (State == SealGameState.Playing) { musicPlayer.Play(); playbackStartedAt = AudioSettings.dspTime; }
        }
    }
    public void RestartGame() { StartGame(IsPractice); }
    public void ReturnToTitle()
    {
        if (!initialized) return;
        musicPlayer.Stop(); song.ResetTimings(); inputs.Clear(); songSample = 0;
        IsPractice = false; BuildJudge(false); ResetActors(); State = SealGameState.Ready;
        StatusMessage = "听口令，和伙伴一起打拍子。最右边的海豹是你！";
    }
    public void ToggleAssist()
    {
        AssistEnabled = !AssistEnabled; PlayerPrefs.SetInt("SealRhythm.Assist", AssistEnabled ? 1 : 0);
    }
    // Positive offset compensates for input arriving late relative to the music.
    public void SetTimingOffset(float milliseconds)
    {
        TimingOffsetMs = Mathf.Clamp(milliseconds, -250, 250); PlayerPrefs.SetFloat("SealRhythm.Offset", TimingOffsetMs);
    }
    private void FinishGame()
    {
        Judge.Advance(int.MaxValue); musicPlayer.Stop(); songSample = durationSamples; inputs.Clear();
        State = SealGameState.Results; StatusMessage = IsPractice ? "练习完成，可以挑战整首演出了！" : "演出结束！";
        if (!IsPractice && Judge.Score > BestScore) { BestScore = Judge.Score; PlayerPrefs.SetInt(BestKey, BestScore); }
        PlayerPrefs.Save();
    }
    private void OnJudged(Judgement j)
    {
        bool success = j.Grade == HitGrade.Perfect || j.Grade == HitGrade.Good;
        string label = j.Grade == HitGrade.Perfect ? "PERFECT!" : j.Grade == HitGrade.Good ? "GOOD" : j.Grade == HitGrade.Stray ? "多按了" : "MISS";
        string timing = success ? (Math.Abs(j.OffsetMilliseconds) < 10 ? "正中节拍" : (j.OffsetMilliseconds < 0 ? "早了 " : "晚了 ") + Math.Abs(j.OffsetMilliseconds).ToString("0") + " ms") : "稳住，听下一拍";
        if (hud != null) hud.ShowJudgement(label, timing, success);
        if (!success && AudioSettings.dspTime - feedbackAt > .1) { PlayTone(4); feedbackAt = AudioSettings.dspTime; }
    }
    private bool Pressed(int index, KeyCode letter, KeyCode arrow) => Input.GetKeyDown(letter) || Input.GetKeyDown(arrow) || (keys != null && index < keys.Length && Input.GetKeyDown(keys[index]));
    public static string ActionLabel(SealAction a) => a == SealAction.Nod ? "点头 ↓" : a == SealAction.Left ? "左拍 ←" : "右拍 →";
    public static void PlayAction(Animator animator, string[] names, int index)
    {
        if (animator == null || names == null || index < 0 || index >= names.Length || !animator.isActiveAndEnabled) return;
        int state = Animator.StringToHash("Base Layer." + names[index]);
        if (animator.HasState(0, state)) animator.Play(state, 0, 0); else animator.SetTrigger(names[index]);
    }
    private void ResetActors()
    {
        SetActorSpeed(1);
        if (actors != null) foreach (var actor in actors) if (actor != null) { actor.Rebind(); actor.Update(0); }
    }
    private void SetActorSpeed(float speed) { if (actors != null) foreach (var actor in actors) if (actor != null) actor.speed = speed; }
    private void FrameStage()
    {
        if (stageCamera == null || stageBounds.size == Vector3.zero) return;
        framedAspect = stageCamera.aspect;
        stageCamera.orthographicSize = Mathf.Max(stageBounds.extents.y / .70f, stageBounds.extents.x / Mathf.Max(.1f, framedAspect));
        stageCamera.transform.position = new Vector3(stageBounds.center.x, stageBounds.center.y, stageCamera.transform.position.z);
        stageCamera.clearFlags = CameraClearFlags.SolidColor;
        stageCamera.backgroundColor = new Color(.94f, .99f, 1f);
    }
    private void OnApplicationFocus(bool focus) { if (!focus && (State == SealGameState.Playing || State == SealGameState.CountIn)) TogglePause(); }
    private void Fail(string message) { State = SealGameState.Error; StatusMessage = message; Debug.LogError(message); }
    private void CreateFeedbackAudio()
    {
        feedbackAudio = gameObject.AddComponent<AudioSource>(); feedbackAudio.playOnAwake = false; feedbackAudio.volume = .22f;
        tones = new AudioClip[5]; float[] frequencies = { 660, 520, 780, 1000, 150 };
        for (int i = 0; i < tones.Length; i++)
        {
            int length = i == 4 ? 3600 : 2000; var data = new float[length];
            for (int n = 0; n < length; n++) data[n] = Mathf.Sin(2 * Mathf.PI * frequencies[i] * n / 44100f) * Mathf.Sin(Mathf.PI * n / length) * Mathf.Exp(-5f * n / length);
            tones[i] = AudioClip.Create("Seal feedback " + i, length, 1, 44100, false); tones[i].SetData(data, 0);
        }
    }
    private void PlayTone(int index) { if (feedbackAudio != null && tones != null) feedbackAudio.PlayOneShot(tones[index]); }
    private void OnDestroy()
    {
        if (Judge != null) Judge.Judged -= OnJudged; SetActorSpeed(1);
        if (tones != null) foreach (var tone in tones) if (tone != null) Destroy(tone);
    }
}
