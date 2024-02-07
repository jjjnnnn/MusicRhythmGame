using SonicBloom.Koreo;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class noteObject : MonoBehaviour
{
    private KoreographyEvent trackEvent;
    //private GameController gameController;
    


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
        int endTime = trackEvent.EndSample;

        //int hitTime = gameController.HitWindowSampleWidth;
        return true;
    }
}
