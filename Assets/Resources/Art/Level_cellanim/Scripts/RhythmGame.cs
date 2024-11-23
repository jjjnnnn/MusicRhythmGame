using SonicBloom.Koreo;
using UnityEngine;
using UnityEngine.UI;


public class RhythmGame : MonoBehaviour
{
    [Header("插件事件")]
    private int eventSampleTime;
    [EventID]public string koreoEventID;
    private KoreographyEvent currentEvent;
    [Tooltip("事件文本名字")]public string[] eventTextName;

    [Header("动画控制")]
    public Animator characterAnimator; // 角色动画控制器引用
    [Tooltip("动画名字")] public string[] aniName;

    [Header("游戏难度调整")]
    public int hitWindowRangeInMS = 100; // 有效的命中时间窗（毫秒）

    [Header("UI控制")]
    [Tooltip("Miss提示预制")] public GameObject missTextPrefab; // Miss提示预制
    [Tooltip("Perfect提示预制")] public GameObject perfectTextPrefab; // Perfect提示预制
    [Tooltip("提示文字的生成位置")] public Transform uiCanvasTransform; // 提示文字的生成位置
    [Tooltip("分数文本")] public Text scoreText;
    int score=0;//当前得分数

    [Header("游戏输入")]
    public KeyCode[] keys; 

    void Start()
    {
        // 假设"SKeyEventID"是你用来表示某个Koreography事件的ID
        Koreographer.Instance.RegisterForEvents(koreoEventID, OnSKoreographyEvent);
    }

    void OnDestroy()
    {
        //Koreographer.Instance.UnregisterForEvents(koreoEventID, OnSKoreographyEvent);
    }

    // 这个回调将被触发，当Koreographer到达我们定义的S键事件时
    void OnSKoreographyEvent(KoreographyEvent koreoEvent)
    {
        // 在这里执行当事件发生时的逻辑，可以记录下事件发生的时间
        eventSampleTime = koreoEvent.StartSample;

        //同步当前事件
        currentEvent = koreoEvent;
        
    }
    void Update()
    {
        scoreText.text = string.Format("当前得分：" + score);
        PlayerInput(keys[0], eventTextName[0], aniName[0]);
        PlayerInput(keys[1], eventTextName[1], aniName[1]);
        PlayerInput(keys[2], eventTextName[2], aniName[2]);

    }

    private void PlayerInput(KeyCode button,string eventTextName,string aniName)
    {
        int currentSampleTime = Koreographer.Instance.GetMusicSampleTime();// 获得当前音乐时间（转换为采样点）
        if (Input.GetKeyDown(button))
        {

            characterAnimator.SetTrigger(aniName);

            // 判断玩家按下按键的时机是否接近某个事件点
            if (IsCloseToEvent(currentSampleTime, eventSampleTime) && currentEvent.GetTextValue() == eventTextName)
            {
                // 玩家成功按在了节奏上
                Debug.Log("成功");
                ShowText(perfectTextPrefab);
                score++;
            }
        }
    }

    private void ShowText(GameObject textPrefab)
    {
        GameObject textObject = Instantiate(textPrefab, uiCanvasTransform);
        textObject.transform.localPosition = Vector3.zero; // 或者其它指定位置
        Destroy(textObject, 0.3f); // N秒后销毁这个文本对象
    }

    // 检查玩家的按键时间是否足够接近事件发生的时间
    bool IsCloseToEvent(int sampleTime, int eventSampleTime)
    {
        int currentTime = Koreographer.Instance.GetMusicSampleTime(); // 当前的采样时间
        // 获取音频的采样率，通常是 44100Hz (但是可以是其他值)
        int sampleRate = Koreographer.GetSampleRate();

        // 采样率与hitWindowRangeInMS结合，计算有效的命中窗口采样数
        int hitWindowInSamples = (int)(sampleRate * (hitWindowRangeInMS / 1000f));

        if (Mathf.Abs(currentTime - eventSampleTime) <= hitWindowInSamples)
        {
            return true;
        }
        return false;
    }
}