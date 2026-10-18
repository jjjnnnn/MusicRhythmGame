using System;
using SonicBloom.Koreo;
using UnityEngine;

/// <summary>Plays the partner seals from the same sample-aligned chart as the player.</summary>
public class xuRenOtherKoreo : MonoBehaviour
{
    [SerializeField, EventID] private string[] TrackEventName = { "haibaoEventID" };
    public string[] eventTextName = { "diantou", "left", "right" };
    public string[] aniName = { "diantou", "left", "right" };
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
        if (isActiveAndEnabled) RhythmGame.PlayAction(animator, aniName, Array.IndexOf(eventTextName, beat.GetTextValue()));
    }
    private void OnDestroy() { if (conductor != null) conductor.UnregisterForAllEvents(this); }
}
