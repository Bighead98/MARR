using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using System.Collections;


public class R2CResetter : Resetter
{

    public float targetRealRotation = 0;

    float requiredRotateSteerAngle = 0;//steering angle，rotate the physical plane and avatar together

    float requiredRotateAngle = 0;//normal rotation angle, only rotate avatar

    float rotateDir =1.0f;
    float speedRatio;
    private float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    public override void InitializeReset() 
    {

        var currDir = Utilities.FlattenedPos2D(redirectionManager.currDirReal);
        var currPos = Utilities.FlattenedPos2D(redirectionManager.currPosReal);

        var obstaclePolygons = redirectionManager.globalConfiguration.obstaclePolygons;
        Vector2 center = new Vector2(0.0f,0.0f);
        bool flag = true;
        rotateDir =1.0f;
        targetRealRotation = 90.0f;
        Vector2 bet = (center - currPos);
        foreach (var obstacle in obstaclePolygons) {
            for (int i = 0; i < obstacle.Count; i++)
            {
                var p = obstacle[i];
                var q = obstacle[(i + 1) % obstacle.Count];
                
                if (IfCollideWithPoint(currPos, bet, p))
                {
                    flag=false;
                }

                if (Vector3.Cross(q - p, currPos - p).magnitude / (q - p).magnitude <= redirectionManager.globalConfiguration.RESET_TRIGGER_BUFFER//distance
                    && Vector2.Dot(q - p, currPos - p) >= 0 && Vector2.Dot(p - q, currPos - q) >= 0//range
                    )
                {                    
                    //if collide with border
                    if (Mathf.Abs(Cross(q - p, bet)) > 1e-3 && Mathf.Sign(Cross(q - p, bet)) != Mathf.Sign(Cross(q - p, currPos - p)))
                    {
                        flag=false;
                    }
                }
            }
        }
        if(redirectionManager.user_trigger)
            flag=false;
        if(flag){
            var centerDir = center - Utilities.FlattenedPos2D(redirectionManager.currPosReal);

            rotateDir = -(int)Mathf.Sign(Utilities.GetSignedAngle(redirectionManager.currDirReal, Utilities.UnFlatten(centerDir)));

            //rotate by simulatedWalker
            targetRealRotation = 360 - Vector2.Angle(centerDir, currDir);

        }
        else{
            targetRealRotation += (180.0f - redirectionManager.resetter.collisionangle);

        }
        requiredRotateSteerAngle = 360 - targetRealRotation;
            
        requiredRotateAngle = targetRealRotation;

        speedRatio = requiredRotateSteerAngle / requiredRotateAngle;
        SetHUD((int)rotateDir);
        //rotate clockwise by default
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
        redirectionManager.simulatedWalker.RotateInPlace(rotateAngle * rotateDir);
    }

    public override bool IsResetRequired()
    {
        return IfCollisionHappens();
    }
}