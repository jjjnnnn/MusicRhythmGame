using SonicBloom.Koreo;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class KoreController : MonoBehaviour
{
    [SerializeField][EventID] private string TrackEventName;//�¼�����
    private Animator m_animator;
    

    // Start is called before the first frame update
    void Start()
    {
        m_animator = GetComponent<Animator>();
        Koreographer.Instance.RegisterForEvents(TrackEventName, AniFunction);
    }

    private void AniFunction(KoreographyEvent e)
    {
        print("����");
        m_animator.SetTrigger("diantou");
    }

}
