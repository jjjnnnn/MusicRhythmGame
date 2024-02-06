using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SonicBloom.Koreo;
using System;
using Unity.VisualScripting;

public class playerController : MonoBehaviour
{

    [SerializeField][EventID] private string[] TrackEventName;//�¼�����
   
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
        //���²�����
        hitWindowRangeInSamples = (0.001f * hitWindowRangeInMS * playingKoreo.SampleRate);

        if (Input.GetKeyDown(KeyCode.S))
        {
            print("���ŵ�ͷ����");
            _animator.SetTrigger("diantou");
            if(IsNoteHittable())
            {
                Score++;
            }

        }
        if (Input.GetKeyDown(KeyCode.A))
        {
            print("�����󶯻�");
            _animator.SetTrigger("left");
            if (IsNoteHittable())
            {
                Score++;
            }
        }
        if (Input.GetKeyDown(KeyCode.D))
        {
            print("�����Ҷ���");
            _animator.SetTrigger("right");
            if (IsNoteHittable())
            {
                Score++;
            }
        }


    }

    /// <summary>
    /// �Ƿ����н����
    /// </summary>
    /// <returns>�Ƿ�����</returns>
    private bool IsNoteHittable()
    {
        int rhythmTime = trackEvents[trackEventIndex].StartSample;//����ʱ��
        int curTime = playingKoreo.GetLatestSampleTime();//��ǰ���ֽ���ʱ��
        float hitWindow = hitWindowRangeInSamples;//����ʱ���

        return (Math.Abs(curTime-rhythmTime)<=hitWindow);
    }

    private void CheckSpawnNext()
    {

        
    }
}
