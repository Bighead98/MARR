using UnityEngine;
using System.Collections;

public class RRCResetter : Resetter
{

    public float targetRealRotation = 0;

    float requiredRotateSteerAngle = 0;//steering angle，rotate the physical plane and avatar together

    float requiredRotateAngle = 0;//normal rotation angle, only rotate avatar

    float speedRatio;
    
    public override void InitializeReset() 
    {
        requiredRotateSteerAngle = 360 - targetRealRotation;
            
        requiredRotateAngle = targetRealRotation;

        speedRatio = requiredRotateSteerAngle / requiredRotateAngle;
        //rotate clockwise by default
        SetHUD(1);
    }

    public override void InjectResetting() 
    {
        var steerRotation = speedRatio * redirectionManager.deltaDir;      

        if (Mathf.Abs(requiredRotateSteerAngle) <= Mathf.Abs(steerRotation) || requiredRotateAngle == 0)
        {//meet the rotation requirement
            InjectRotation(requiredRotateSteerAngle);

            //reset end
            redirectionManager.OnResetEnd();
            requiredRotateSteerAngle = 0;

        }
        else
        {//rotate the rotation calculated by ratio
            InjectRotation(steerRotation);
            requiredRotateSteerAngle -= Mathf.Abs(steerRotation);            
        }
        //Debug.Log("requiredRotateAngle:" + requiredRotateAngle + "; overallInjectedRotation:" + overallInjectedRotation);
    }

    public override void EndReset() 
    { 
        DestroyHUD();
    }


    public override void SimulatedWalkerUpdate() 
    { 
        var rotateAngle = redirectionManager.GetDeltaTime() * redirectionManager.globalConfiguration.rotationSpeed;

        //finish rotating
        if (rotateAngle >= requiredRotateAngle)
        {
            rotateAngle = requiredRotateAngle;            
            requiredRotateAngle = 0;
        }
        else {
            requiredRotateAngle -= rotateAngle;
        }        
        redirectionManager.simulatedWalker.RotateInPlace(rotateAngle);
    }

    public override bool IsResetRequired()
    {
        return IfCollisionHappens();
    }
}
