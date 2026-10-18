using System;
using SonicBloom.Koreo;
using UnityEngine;

/// <summary>Plays the partner seals from the same sample-aligned chart as the player.</summary>
public class xuRenOtherKoreo : MonoBehaviour
{
    [SerializeField, EventID] private string[] TrackEventName = { "haibaoEventID" };
    public string[] eventTextName = { "diantou", "left", "right", "roll" };
    public string[] aniName = { "diantou", "left", "right", "roll" };
    private Animator animator;
    private Koreographer conductor;
    private void Start()
    {
        animator = GetComponent<Animator>();
        conductor = Koreographer.Instance;
        if (conductor == null || TrackEventName == null) return;
        foreach (string track in TrackEventName)
            if (!string.IsNullOrEmpty(track)) conductor.RegisterForEvents(track, OnBeat);
    }
    private void OnBeat(KoreographyEvent beat)
    {
        if (isActiveAndEnabled && RhythmGame.TryGetAction(beat.GetTextValue(), out var action))
            RhythmGame.PlayAction(animator, aniName, (int)action);
    }
    private void OnDestroy() { if (conductor != null) conductor.UnregisterForAllEvents(this); }
}
