using SonicBloom.Koreo;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameController : MonoBehaviour
{
    [Tooltip("ʹ�õĹ���¼�id")]
    [EventID] public string eventID;

    [Tooltip("��������Ϊ���еĺ�����(������),����һ������ķ�Ӧʱ��")]
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
