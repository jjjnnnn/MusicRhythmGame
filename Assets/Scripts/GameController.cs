using UnityEngine;
using SonicBloom.Koreo;

public class GameController : MonoBehaviour
{

    [Header("Koreography Setup")]
    public Koreography koreography; // ָ��Koreography������
    [EventID]public string eventID; // ��Koreography�����õ��¼�ID

    [Header("Gameplay")]
    public Animator characterAnimator; // ��ɫ��������������
    public GameObject missTextPrefab; // Miss��ʾԤ��
    public GameObject perfectTextPrefab; // Perfect��ʾԤ��
    public Transform textSpawnPoint; // ��ʾ���ֵ�����λ��
    private int hitWindowRangeInMS = 100; // ��Ч������ʱ�䴰�����룩
    private KoreographyEvent currentEvent; // ��ǰ�������Koreography�¼�

    void Start()
    {
        // ע��Koreography�¼�
        Koreographer.Instance.RegisterForEventsWithTime(eventID, OnKoreographyEvent);
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
        // ����������
        if (Input.anyKeyDown)
        {
            characterAnimator.SetTrigger("diantou"); // ���ŵ�ͷ����
            CheckPlayerInput();
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
                Debug.Log("MISS");
            }

            // ����ǰ�¼���Ϊnull�������ظ��ж�
            currentEvent = null;
        }
    }

    void OnKoreographyEvent(KoreographyEvent koreoEvent, int sampleTime, int sampleDelta, DeltaSlice deltaSlice)
    {
        // ��Koreography�¼�����ʱ����¼��ǰ�¼�
        currentEvent = koreoEvent;
    }

    void ShowText(GameObject textPrefab)
    {
        // ʵ����Ʈ����ʾ
        Instantiate(textPrefab, textSpawnPoint.position, Quaternion.identity, transform);
    }
}