using System;
using SonicBloom.Koreo;
using UnityEngine;

/// <summary>Captain cues are independent of player judgement and replay on every attempt.</summary>
public class PeopleController : MonoBehaviour
{
    [SerializeField, EventID] private string[] TrackEventName = { "PeopleEventID" };
    public string[] eventTextName = { "characterMove", "characterDiantou", "characterSpeak01" };
    public string[] aniName = { "characterMove", "characterDiantou", "characterSpeak01" };
    private Animator animator;
    private Koreographer conductor;
    private void Start()
    {
        animator = GetComponent<Animator>();
        conductor = Koreographer.Instance;
        if (conductor == null || TrackEventName == null) return;
        foreach (string track in TrackEventName)
            if (!string.IsNullOrEmpty(track)) conductor.RegisterForEvents(track, OnCue);
    }
    private void OnCue(KoreographyEvent cue)
    {
        int index = Array.IndexOf(eventTextName, cue.GetTextValue());
        if (isActiveAndEnabled && animator != null && index >= 0 && index < aniName.Length)
            animator.SetTrigger(aniName[index]);
    }
    private void OnDestroy() { if (conductor != null) conductor.UnregisterForAllEvents(this); }
}
