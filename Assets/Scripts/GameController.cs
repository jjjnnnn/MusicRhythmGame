using UnityEngine;
using SonicBloom.Koreo;
using UnityEngine.UI;
using System.Collections.Generic;
using SonicBloom.Koreo.Demos;
using System;

public class GameController : MonoBehaviour
{

    [Header("插件事件")]
    public Koreography koreography; // 指向Koreography的引用
    [EventID]public string eventID; // 在Koreography中配置的事件ID
    List<KoreographyEvent> koreoEvents = new List<KoreographyEvent>();//此通道的所有事件列表
    private int sampleTime;


    [Header("游戏控制")]
    public Animator characterAnimator; // 角色动画控制器引用
    public int hitWindowRangeInMS = 100; // 有效的命中时间窗（毫秒）
    private KoreographyEvent currentEvent; // 当前待处理的Koreography事件
    private float lastHitTime = -1.0f; // 上一次击中的时间，用于避免重复击中同一节奏点


    [Header("容错事件率")]
    public float hitThreshold = 0.1f; // 容错时间帧，这里给的例子是±0.1秒
    [Header("歌曲事件名字")]
    public string[] eventsName;

    [Header("击中时间点")]
    public float timing; // 击中的时间点，比如以歌曲开始后的时间秒表示

    [Header("音乐播放器")]
    public AudioSource audioSource;

    [Header("UI")]
    public GameObject missTextPrefab; // Miss提示预制
    public GameObject perfectTextPrefab; // Perfect提示预制
    public Transform uiCanvasTransform; // 提示文字的生成位置


    int pendingEventIdx = 0;
    void Start()
    {

        // 注册Koreography事件
        Koreographer.Instance.RegisterForEvents(eventID, OnKoreographyEvent);

        // 获取编辑器音轨的所有diantou事件，并将其添加至集合中
        koreography = Koreographer.Instance.GetKoreographyAtIndex(0);
        // Grab all the events out of the Koreography.
        KoreographyTrackBase rhythmTrack = koreography.GetTrackByID(eventID);
        List<KoreographyEvent> rawEvents = rhythmTrack.GetAllEvents();
        for (int i = 0; i < rawEvents.Count; ++i)
        {
            koreoEvents.Add(rawEvents[i]);
        }
    }
    void OnKoreographyEvent(KoreographyEvent koreoEvent)
    {
        // 当Koreography事件触发时，记录当前事件
        currentEvent = koreoEvent;
        sampleTime = koreoEvent.StartSample;
    }
    void OnDestroy()
    {
        // 取消事件注册
        if (Koreographer.Instance != null)
        {
            Koreographer.Instance.UnregisterForAllEvents(this);
        }
    }

    void Update()
    {
        foreach(var point in koreoEvents)
        {
            if(IsTimeForRhythmPoint(point))
            {
                if (Input.GetKeyDown(KeyCode.S) && point.GetTextValue() == eventsName[0])
                {
                    characterAnimator.SetTrigger("diantou");
                    ShowText(perfectTextPrefab);
                }
                else if(Input.GetKeyDown(KeyCode.S) && point.GetTextValue() != eventsName[0])
                {
                    characterAnimator.SetTrigger("diantou");
                    ShowText(missTextPrefab);
                }
            }
            else
            {
                if(Input.GetKeyDown(KeyCode.S))
                {
                    characterAnimator.SetTrigger("diantou");
                    Debug.Log("点头动画");
                }
            }

            
        }


        
    }

    
    void CheckPlayerInput()
    {
        int currentTime = Koreographer.Instance.GetMusicSampleTime(); // 当前的采样时间

        if (currentEvent != null)
        {
            // 当前KoreographyEvent的采样时间点
            int eventSampleTime = currentEvent.StartSample;

            // 获取音频的采样率，通常是 44100Hz (但是可以是其他值)
            int sampleRate = Koreographer.GetSampleRate();

            // 采样率与hitWindowRangeInMS结合，计算有效的命中窗口采样数
            int hitWindowInSamples = (int)(sampleRate * (hitWindowRangeInMS / 1000f));

            if (Mathf.Abs(currentTime - eventSampleTime) <= hitWindowInSamples)
            {
                
                // 在节奏点命中
                //characterAnimator.SetTrigger("diantou"); // 播放点头动画
                ShowText(perfectTextPrefab); // 显示Perfect
                Debug.Log("完美");
                
            }
            else
            {
                // 没有在节奏点命中
                //characterAnimator.SetTrigger("diantou"); // 播放点头动画
                ShowText(missTextPrefab); // 显示Miss
                Debug.Log("未命中");
            }

            // 将当前事件设为null，避免重复判断
            currentEvent = null;
        }
    }

    /// <summary>
    /// 当前时间是否是当前节奏点
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    bool IsTimeForRhythmPoint(KoreographyEvent point)
    {
        int currentTime = Koreographer.Instance.GetMusicSampleTime(); // 当前的采样时间

        if (currentEvent != null)
        {
            // 当前KoreographyEvent的采样时间点
            int eventSampleTime = currentEvent.StartSample;

            // 获取音频的采样率，通常是 44100Hz (但是可以是其他值)
            int sampleRate = Koreographer.GetSampleRate();

            // 采样率与hitWindowRangeInMS结合，计算有效的命中窗口采样数
            int hitWindowInSamples = (int)(sampleRate * (hitWindowRangeInMS / 1000f));

            if (Mathf.Abs(currentTime - eventSampleTime) <= hitWindowInSamples)
            {
                return true;
            }
        }
        return false;// 当前时间不在有效击中时间范围内
    }

    /// <summary>
    /// 获取当前音乐时间
    /// </summary>
    /// <returns></returns>
    public float GetSongTime()
    {
        return audioSource.time;
    }

  

    void ShowText(GameObject textPrefab)
    {
        // 实例化飘字提示
        GameObject textObject = Instantiate(textPrefab, uiCanvasTransform);
        textObject.transform.localPosition = Vector3.zero; // 或者其它指定位置

        // 假定你的文本预制体中有 Animation 组件和 Animator 控制器
        // 你可以设置动画播放完成后自动销毁或隐藏对象
        // 如果你只是想要文本显示一段时间后消失，这里可以添加一些延迟销毁逻辑
        Destroy(textObject, 0.3f); // 例如，2秒后销毁这个文本对象
    }

    // 当玩家击中节奏点时调用
 




}