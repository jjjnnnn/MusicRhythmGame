using UnityEngine;
using SonicBloom.Koreo;

public class GameController : MonoBehaviour
{

    [Header("Koreography Setup")]
    public Koreography koreography; // 指向Koreography的引用
    [EventID]public string eventID; // 在Koreography中配置的事件ID

    [Header("Gameplay")]
    public Animator characterAnimator; // 角色动画控制器引用
    public GameObject missTextPrefab; // Miss提示预制
    public GameObject perfectTextPrefab; // Perfect提示预制
    public Transform textSpawnPoint; // 提示文字的生成位置
    private int hitWindowRangeInMS = 100; // 有效的命中时间窗（毫秒）
    private KoreographyEvent currentEvent; // 当前待处理的Koreography事件

    void Start()
    {
        // 注册Koreography事件
        Koreographer.Instance.RegisterForEventsWithTime(eventID, OnKoreographyEvent);
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
        // 检查玩家输入
        if (Input.anyKeyDown)
        {
            characterAnimator.SetTrigger("diantou"); // 播放点头动画
            CheckPlayerInput();
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
                Debug.Log("MISS");
            }

            // 将当前事件设为null，避免重复判断
            currentEvent = null;
        }
    }

    void OnKoreographyEvent(KoreographyEvent koreoEvent, int sampleTime, int sampleDelta, DeltaSlice deltaSlice)
    {
        // 当Koreography事件触发时，记录当前事件
        currentEvent = koreoEvent;
    }

    void ShowText(GameObject textPrefab)
    {
        // 实例化飘字提示
        Instantiate(textPrefab, textSpawnPoint.position, Quaternion.identity, transform);
    }
}