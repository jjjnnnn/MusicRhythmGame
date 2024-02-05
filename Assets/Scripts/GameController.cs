using SonicBloom.Koreo;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameController : MonoBehaviour
{
    [Tooltip("使用的轨道事件id")]
    [EventID] public string eventID;

    [Tooltip("将输入检测为命中的毫秒数(早晚期),即玩家击打节奏的反应时间")]
    [Range(8f, 150f)] public float hitWindowRangeInMS = 80;

    


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
