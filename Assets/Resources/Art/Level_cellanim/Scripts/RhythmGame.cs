using SonicBloom.Koreo;
using UnityEngine;
using UnityEngine.UI;


public class RhythmGame : MonoBehaviour
{
    [Header("����¼�")]
    private int eventSampleTime;
    [EventID]public string koreoEventID;
    private KoreographyEvent currentEvent;
    [Tooltip("�¼��ı�����")]public string[] eventTextName;

    [Header("��������")]
    public Animator characterAnimator; // ��ɫ��������������
    [Tooltip("��������")] public string[] aniName;

    [Header("��Ϸ�Ѷȵ���")]
    public int hitWindowRangeInMS = 100; // ��Ч������ʱ�䴰�����룩

    [Header("UI����")]
    [Tooltip("Miss��ʾԤ��")] public GameObject missTextPrefab; // Miss��ʾԤ��
    [Tooltip("Perfect��ʾԤ��")] public GameObject perfectTextPrefab; // Perfect��ʾԤ��
    [Tooltip("��ʾ���ֵ�����λ��")] public Transform uiCanvasTransform; // ��ʾ���ֵ�����λ��
    [Tooltip("�����ı�")] public Text scoreText;
    int score=0;//��ǰ�÷���

    [Header("��Ϸ����")]
    public KeyCode[] keys; 

    void Start()
    {
        // ����"SKeyEventID"����������ʾĳ��Koreography�¼���ID
        Koreographer.Instance.RegisterForEvents(koreoEventID, OnSKoreographyEvent);
    }

    void OnDestroy()
    {
        //Koreographer.Instance.UnregisterForEvents(koreoEventID, OnSKoreographyEvent);
    }

    // ����ص�������������Koreographer�������Ƕ����S���¼�ʱ
    void OnSKoreographyEvent(KoreographyEvent koreoEvent)
    {
        // ������ִ�е��¼�����ʱ���߼������Լ�¼���¼�������ʱ��
        eventSampleTime = koreoEvent.StartSample;

        //ͬ����ǰ�¼�
        currentEvent = koreoEvent;
        
    }
    void Update()
    {
        scoreText.text = string.Format("��ǰ�÷֣�" + score);
        PlayerInput(keys[0], eventTextName[0], aniName[0]);
        PlayerInput(keys[1], eventTextName[1], aniName[1]);
        PlayerInput(keys[2], eventTextName[2], aniName[2]);

    }

    private void PlayerInput(KeyCode button,string eventTextName,string aniName)
    {
        int currentSampleTime = Koreographer.Instance.GetMusicSampleTime();// ��õ�ǰ����ʱ�䣨ת��Ϊ�����㣩
        if (Input.GetKeyDown(button))
        {

            characterAnimator.SetTrigger(aniName);

            // �ж���Ұ��°�����ʱ���Ƿ�ӽ�ĳ���¼���
            if (IsCloseToEvent(currentSampleTime, eventSampleTime) && currentEvent.GetTextValue() == eventTextName)
            {
                // ��ҳɹ������˽�����
                Debug.Log("�ɹ�");
                ShowText(perfectTextPrefab);
                score++;
            }
        }
    }

    private void ShowText(GameObject textPrefab)
    {
        GameObject textObject = Instantiate(textPrefab, uiCanvasTransform);
        textObject.transform.localPosition = Vector3.zero; // ��������ָ��λ��
        Destroy(textObject, 0.3f); // N�����������ı�����
    }

    // �����ҵİ���ʱ���Ƿ��㹻�ӽ��¼�������ʱ��
    bool IsCloseToEvent(int sampleTime, int eventSampleTime)
    {
        int currentTime = Koreographer.Instance.GetMusicSampleTime(); // ��ǰ�Ĳ���ʱ��
        // ��ȡ��Ƶ�Ĳ����ʣ�ͨ���� 44100Hz (���ǿ���������ֵ)
        int sampleRate = Koreographer.GetSampleRate();

        // ��������hitWindowRangeInMS��ϣ�������Ч�����д��ڲ�����
        int hitWindowInSamples = (int)(sampleRate * (hitWindowRangeInMS / 1000f));

        if (Mathf.Abs(currentTime - eventSampleTime) <= hitWindowInSamples)
        {
            return true;
        }
        return false;
    }
}