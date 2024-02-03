using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SonicBloom.Koreo;

public class xuRenKoreo : MonoBehaviour
{

    [SerializeField][EventID] private string TrackEventName;//�¼�����

    private Animator _animator;


    // Start is called before the first frame update
    void Start()
    {
        _animator=GetComponent<Animator>();

        Koreographer.Instance.RegisterForEvents(TrackEventName, Event);


    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void Event(KoreographyEvent koreoEvent)
    {
        Debug.Log("ִ�и��¼�");
        _animator.SetTrigger("diantou");
        
    }


}
