using SonicBloom.Koreo;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class noteObject : MonoBehaviour
{
    private KoreographyEvent trackEvent;
    


    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    
    public bool IsNoteHittable()
    {
        int noteTime = trackEvent.StartSample;//note开始时间
        int curTime;
        int hitWindow;
        return true;
    }
}
