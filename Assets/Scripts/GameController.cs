using UnityEngine;
using SonicBloom.Koreo;
using UnityEngine.UI;
using System.Collections.Generic;
using SonicBloom.Koreo.Demos;
using System;

public class GameController : MonoBehaviour
{

    [Header("����¼�")]
    public Koreography koreography; // ָ��Koreography������
    [EventID]public string eventID; // ��Koreography�����õ��¼�ID
    List<KoreographyEvent> koreoEvents = new List<KoreographyEvent>();//��ͨ���������¼��б�
    private int sampleTime;


    [Header("��Ϸ����")]
    public Animator characterAnimator; // ��ɫ��������������
    public int hitWindowRangeInMS = 100; // ��Ч������ʱ�䴰�����룩
    private KoreographyEvent currentEvent; // ��ǰ�������Koreography�¼�
    private float lastHitTime = -1.0f; // ��һ�λ��е�ʱ�䣬���ڱ����ظ�����ͬһ�����


    [Header("�ݴ��¼���")]
    public float hitThreshold = 0.1f; // �ݴ�ʱ��֡��������������ǡ�0.1��
    [Header("�����¼�����")]
    public string[] eventsName;

    [Header("����ʱ���")]
    public float timing; // ���е�ʱ��㣬�����Ը�����ʼ���ʱ�����ʾ

    [Header("���ֲ�����")]
    public AudioSource audioSource;

    [Header("UI")]
    public GameObject missTextPrefab; // Miss��ʾԤ��
    public GameObject perfectTextPrefab; // Perfect��ʾԤ��
    public Transform uiCanvasTransform; // ��ʾ���ֵ�����λ��


    int pendingEventIdx = 0;
    void Start()
    {

        // ע��Koreography�¼�
        Koreographer.Instance.RegisterForEvents(eventID, OnKoreographyEvent);

        // ��ȡ�༭�����������diantou�¼��������������������
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
        // ��Koreography�¼�����ʱ����¼��ǰ�¼�
        currentEvent = koreoEvent;
        sampleTime = koreoEvent.StartSample;
    }
    void OnDestroy()
    {
        // ȡ���¼�ע��
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
                    Debug.Log("��ͷ����");
                }
            }

            
        }


        
    }

    
    void CheckPlayerInput()
    {
        int currentTime = Koreographer.Instance.GetMusicSampleTime(); // ��ǰ�Ĳ���ʱ��

        if (currentEvent != null)
        {
            // ��ǰKoreographyEvent�Ĳ���ʱ���
            int eventSampleTime = currentEvent.StartSample;

            // ��ȡ��Ƶ�Ĳ����ʣ�ͨ���� 44100Hz (���ǿ���������ֵ)
            int sampleRate = Koreographer.GetSampleRate();

            // ��������hitWindowRangeInMS��ϣ�������Ч�����д��ڲ�����
            int hitWindowInSamples = (int)(sampleRate * (hitWindowRangeInMS / 1000f));

            if (Mathf.Abs(currentTime - eventSampleTime) <= hitWindowInSamples)
            {
                
                // �ڽ��������
                //characterAnimator.SetTrigger("diantou"); // ���ŵ�ͷ����
                ShowText(perfectTextPrefab); // ��ʾPerfect
                Debug.Log("����");
                
            }
            else
            {
                // û���ڽ��������
                //characterAnimator.SetTrigger("diantou"); // ���ŵ�ͷ����
                ShowText(missTextPrefab); // ��ʾMiss
                Debug.Log("δ����");
            }

            // ����ǰ�¼���Ϊnull�������ظ��ж�
            currentEvent = null;
        }
    }

    /// <summary>
    /// ��ǰʱ���Ƿ��ǵ�ǰ�����
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    bool IsTimeForRhythmPoint(KoreographyEvent point)
    {
        int currentTime = Koreographer.Instance.GetMusicSampleTime(); // ��ǰ�Ĳ���ʱ��

        if (currentEvent != null)
        {
            // ��ǰKoreographyEvent�Ĳ���ʱ���
            int eventSampleTime = currentEvent.StartSample;

            // ��ȡ��Ƶ�Ĳ����ʣ�ͨ���� 44100Hz (���ǿ���������ֵ)
            int sampleRate = Koreographer.GetSampleRate();

            // ��������hitWindowRangeInMS��ϣ�������Ч�����д��ڲ�����
            int hitWindowInSamples = (int)(sampleRate * (hitWindowRangeInMS / 1000f));

            if (Mathf.Abs(currentTime - eventSampleTime) <= hitWindowInSamples)
            {
                return true;
            }
        }
        return false;// ��ǰʱ�䲻����Ч����ʱ�䷶Χ��
    }

    /// <summary>
    /// ��ȡ��ǰ����ʱ��
    /// </summary>
    /// <returns></returns>
    public float GetSongTime()
    {
        return audioSource.time;
    }

  

    void ShowText(GameObject textPrefab)
    {
        // ʵ����Ʈ����ʾ
        GameObject textObject = Instantiate(textPrefab, uiCanvasTransform);
        textObject.transform.localPosition = Vector3.zero; // ��������ָ��λ��

        // �ٶ�����ı�Ԥ�������� Animation ����� Animator ������
        // ��������ö���������ɺ��Զ����ٻ����ض���
        // �����ֻ����Ҫ�ı���ʾһ��ʱ�����ʧ������������һЩ�ӳ������߼�
        Destroy(textObject, 0.3f); // ���磬2�����������ı�����
    }

    // ����һ��н����ʱ����
 




}