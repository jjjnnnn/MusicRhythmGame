using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SonicBloom.Koreo;
using System;
using Unity.VisualScripting;

public class playerController : MonoBehaviour
{

    [SerializeField][EventID] private string[] TrackEventName;//事件音轨
   
    [Range(8f, 150f)] public float hitWindowRangeInMS = 80;

    public int Score;

    private Animator _animator;
    private List<KoreographyEvent> trackEvents = new List<KoreographyEvent>();
    private int trackEventIndex = 0;

    private Koreography playingKoreo;
    private float hitWindowRangeInSamples;
    // Start is called before the first frame update
    void Start()
    {
        _animator=GetComponent<Animator>();
        playingKoreo = Koreographer.Instance.GetKoreographyAtIndex(0);
        


    }

    // Update is called once per frame
    void Update()
    {
        //更新采样点
        hitWindowRangeInSamples = (0.001f * hitWindowRangeInMS * playingKoreo.SampleRate);

        if (Input.GetKeyDown(KeyCode.S))
        {
            print("播放点头动画");
            _animator.SetTrigger("diantou");
            if(IsNoteHittable())
            {
                Score++;
            }

        }
        if (Input.GetKeyDown(KeyCode.A))
        {
            print("播放左动画");
            _animator.SetTrigger("left");
            if (IsNoteHittable())
            {
                Score++;
            }
        }
        if (Input.GetKeyDown(KeyCode.D))
        {
            print("播放右动画");
            _animator.SetTrigger("right");
            if (IsNoteHittable())
            {
                Score++;
            }
        }


    }

    /// <summary>
    /// 是否命中节奏点
    /// </summary>
    /// <returns>是否命中</returns>
    private bool IsNoteHittable()
    {
        int rhythmTime = trackEvents[trackEventIndex].StartSample;//节奏时间
        int curTime = playingKoreo.GetLatestSampleTime();//当前音乐节奏时间
        float hitWindow = hitWindowRangeInSamples;//采样时间点

        return (Math.Abs(curTime-rhythmTime)<=hitWindow);
    }

    private void CheckSpawnNext()
    {

        
    }
}
