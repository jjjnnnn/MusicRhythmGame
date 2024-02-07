using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SonicBloom.Koreo;

public class xuRenOtherKoreo : MonoBehaviour
{

    [SerializeField][EventID] private string[] TrackEventName;//�¼�����

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
    /// ��ͷ�¼�
    /// </summary>
    /// <param name="koreoEvent"></param>
    private void DiantouEvent(KoreographyEvent koreoEvent)
    {
        Debug.Log("��������ִ�е�ͷ���¼�");
        _animator.SetTrigger("diantou");
        
    }
    /// <summary>
    /// ��ת
    /// </summary>
    /// <param name="koreoEvent"></param>
    private void LeftEvent(KoreographyEvent koreoEvent)
    {
        Debug.Log("��������ִ����ת���¼�");
        _animator.SetTrigger("left");
    }
    /// <summary>
    /// ��ת
    /// </summary>
    /// <param name="koreoEvent"></param>
    private void RightEvent(KoreographyEvent koreoEvent)
    {
        Debug.Log("��������ִ����ת���¼�");
        _animator.SetTrigger("right");
    }


}
