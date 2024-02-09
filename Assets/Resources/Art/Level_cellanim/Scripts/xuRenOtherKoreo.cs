using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SonicBloom.Koreo;
using UnityEditor.EditorTools;

public class xuRenOtherKoreo : MonoBehaviour
{

    [Tooltip("事件音轨")][SerializeField][EventID] private string[] TrackEventName;//事件音轨
    [Tooltip("事件文本名字")] public string[] eventTextName;

    
    [Header("动画控制")]
    private Animator _animator;
    [Tooltip("动画名字")] public string[] aniName;


    // Start is called before the first frame update
    void Start()
    {
        _animator=GetComponent<Animator>();

        Koreographer.Instance.RegisterForEvents(TrackEventName[0], HaibaoEvent);
        //Koreographer.Instance.RegisterForEvents(TrackEventName[1], LeftEvent);
        //Koreographer.Instance.RegisterForEvents(TrackEventName[2], RightEvent);
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    /// <summary>
    /// 点头事件
    /// </summary>
    /// <param name="koreoEvent"></param>
    private void HaibaoEvent(KoreographyEvent koreoEvent)
    {
        if(koreoEvent.GetTextValue()== eventTextName[0])
        {
            Debug.Log("其他海豹执行点头该事件");
            _animator.SetTrigger(aniName[0]);
        }
        else if(koreoEvent.GetTextValue() == eventTextName[1])
        {
            Debug.Log("其他海豹执行左转该事件");
            _animator.SetTrigger(aniName[1]);
        }
        else if(koreoEvent.GetTextValue() == eventTextName[2])
        {
            Debug.Log("其他海豹执行右转该事件");
            _animator.SetTrigger(aniName[2]);
        }
        
    }
    


}
