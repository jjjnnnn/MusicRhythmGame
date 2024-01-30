using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AniController : MonoBehaviour
{
    private Animator ani;

    // Start is called before the first frame update
    void Start()
    {
        ani = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.S))
        {
            print("���Ž��ද��");
            ani.SetTrigger("diantou");
        }
        if(Input.GetKeyDown(KeyCode.A))
        {
            print("�����󶯻�");
            ani.SetTrigger("left");
        }
        if (Input.GetKeyDown(KeyCode.D))
        {
            print("�����Ҷ���");
            ani.SetTrigger("right");
        }
    }
}
