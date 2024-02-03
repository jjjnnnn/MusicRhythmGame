using SonicBloom.Koreo;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class KoreController : MonoBehaviour
{
    [SerializeField][EventID] private string TrackEventName;//ÊÂ¼þÒô¹ì
    private Animator m_animator;
    

    // Start is called before the first frame update
    void Start()
    {
        m_animator = GetComponent<Animator>();
        Koreographer.Instance.RegisterForEvents(TrackEventName, AniFunction);
    }

    private void AniFunction(KoreographyEvent e)
    {
        print("²¥·Å");
        m_animator.SetTrigger("diantou");
    }

}
