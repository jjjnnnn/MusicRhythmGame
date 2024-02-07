using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SonicBloom.Koreo;

public class xuRenOtherKoreo : MonoBehaviour
{

    [SerializeField][EventID] private string[] TrackEventName;//事件音轨

    private Animator _animator;

    

    // Start is called before the first frame update
    void Start()
    {
        _animator=GetComponent<Animator>();

        Koreographer.Instance.RegisterForEvents(TrackEventName[0], DiantouEvent);
        Koreographer.Instance.RegisterForEvents(TrackEventName[1], LeftEvent);
        Koreographer.Instance.RegisterForEvents(TrackEventName[2], RightEvent);
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    /// <summary>
    /// 点头事件
    /// </summary>
    /// <param name="koreoEvent"></param>
    private void DiantouEvent(KoreographyEvent koreoEvent)
    {
        Debug.Log("其他海豹执行点头该事件");
        _animator.SetTrigger("diantou");
        
    }
    /// <summary>
    /// 左转
    /// </summary>
    /// <param name="koreoEvent"></param>
    private void LeftEvent(KoreographyEvent koreoEvent)
    {
        Debug.Log("其他海豹执行左转该事件");
        _animator.SetTrigger("left");
    }
    /// <summary>
    /// 右转
    /// </summary>
    /// <param name="koreoEvent"></param>
    private void RightEvent(KoreographyEvent koreoEvent)
    {
        Debug.Log("其他海豹执行右转该事件");
        _animator.SetTrigger("right");
    }


}
