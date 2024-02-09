using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SonicBloom.Koreo;
using UnityEditor.EditorTools;

public class xuRenOtherKoreo : MonoBehaviour
{

    [Tooltip("�¼�����")][SerializeField][EventID] private string[] TrackEventName;//�¼�����
    [Tooltip("�¼��ı�����")] public string[] eventTextName;

    
    [Header("��������")]
    private Animator _animator;
    [Tooltip("��������")] public string[] aniName;


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
    /// ��ͷ�¼�
    /// </summary>
    /// <param name="koreoEvent"></param>
    private void HaibaoEvent(KoreographyEvent koreoEvent)
    {
        if(koreoEvent.GetTextValue()== eventTextName[0])
        {
            Debug.Log("��������ִ�е�ͷ���¼�");
            _animator.SetTrigger(aniName[0]);
        }
        else if(koreoEvent.GetTextValue() == eventTextName[1])
        {
            Debug.Log("��������ִ����ת���¼�");
            _animator.SetTrigger(aniName[1]);
        }
        else if(koreoEvent.GetTextValue() == eventTextName[2])
        {
            Debug.Log("��������ִ����ת���¼�");
            _animator.SetTrigger(aniName[2]);
        }
        
    }
    


}
