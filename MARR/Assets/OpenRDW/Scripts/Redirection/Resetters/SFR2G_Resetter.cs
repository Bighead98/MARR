using UnityEngine;
using System.Collections;
using System.Collections.Generic;


//align to the vector calculated by artificial potential fileds, rotate to the side of the larger angle
public class SFR2G_Resetter : Resetter {

    float requiredRotateSteerAngle = 0;//steering angle，rotate the physical plane and avatar together

    float requiredRotateAngle = 0;//normal rotation angle, only rotate avatar

    float rotateDir;//rotation direction, positive if rotate clockwise

    float speedRatio;

    int steps;

    public Vector2 totalForce;

    APF_Redirector redirector;

    public override bool IsResetRequired()
    {
        return IfCollisionHappens();
    }
        
    public override void InitializeReset()
    {        
        if(redirectionManager.rrcagent.real_square_width == 5.0f)
            steps = 2;
        else if(redirectionManager.rrcagent.real_square_width == 10.0f)
            steps = 5;
        else if(redirectionManager.rrcagent.real_square_width == 20.0f)
            steps = 15;
        
        var redirectorTmp = redirectionManager.redirector;        
        CalculateForce();

        var currDir = Utilities.FlattenedDir2D(redirectionManager.currDirReal);
        var targetRealRotation = 360 - Vector2.Angle(totalForce, currDir);//required rotation angle in real world
        
        rotateDir = -(int)Mathf.Sign(Utilities.GetSignedAngle(redirectionManager.currDirReal, Utilities.UnFlatten(totalForce)));
        
        requiredRotateSteerAngle = 360 - targetRealRotation;
        
        requiredRotateAngle = targetRealRotation;

        speedRatio = requiredRotateSteerAngle / requiredRotateAngle;
        
        SetHUD((int)rotateDir);
  
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
    }
    
    public override void EndReset()
    {
        DestroyHUD();
    }

    public override void SimulatedWalkerUpdate()
    {
        // Act is if there's some dummy target a meter away from you requiring you to rotate        
        var rotateAngle = redirectionManager.GetDeltaTime() * redirectionManager.globalConfiguration.rotationSpeed;
        //finish specified rotation
        if (rotateAngle >= requiredRotateAngle)
        {
            rotateAngle = requiredRotateAngle;
            //Avoid accuracy error
            requiredRotateAngle = 0;
        }
        else {
            requiredRotateAngle -= rotateAngle;
        }
        redirectionManager.simulatedWalker.RotateInPlace(rotateAngle * rotateDir);
    }
    

    public void CalculateForce()
    {
        var obstaclePolygons = redirectionManager.globalConfiguration.obstaclePolygons;
        var trackingSpacePoints = redirectionManager.globalConfiguration.trackingSpacePoints;
        
        //get repulsive force and negative gradient
        GetRepulsiveForceAndNegativeGradient(obstaclePolygons, trackingSpacePoints);
    }

    public void GetRepulsiveForceAndNegativeGradient(List<List<Vector2>> obstaclePolygons, List<Vector2> trackingSpacePoints) {
        var nearestPosList = new List<Vector2>();
        var currPosReal = Utilities.FlattenedPos2D(redirectionManager.currPosReal);
        Vector2 ng = Vector2.zero;
        for(int step = 0; step<steps;step++){
            //physical borders' contributions
            for (int i = 0; i < trackingSpacePoints.Count; i++) {
                var p = trackingSpacePoints[i];
                var q = trackingSpacePoints[(i + 1) % trackingSpacePoints.Count];
                var nearestPos = Utilities.GetNearestPos(currPosReal, new List<Vector2> { p, q });            
                nearestPosList.Add(nearestPos);
            }

            //obstacle contribution
            foreach (var obstacle in obstaclePolygons) {
                var nearestPos = Utilities.GetNearestPos(currPosReal, obstacle);
                nearestPosList.Add(nearestPos);
            }

            //consider avatar as point obstacles
            foreach (var user in redirectionManager.globalConfiguration.redirectedAvatars) {
                var uId = user.GetComponent<MovementManager>().avatarId;
                //ignore self
                if (uId == redirectionManager.movementManager.avatarId)
                    continue;
                var nearestPos = user.GetComponent<RedirectionManager>().currPosReal;
                nearestPosList.Add(Utilities.FlattenedPos2D(nearestPos));
            }

            ng = Vector2.zero;
            foreach (var obPos in nearestPosList) {

                //get gradient contributions
                var gDelta = -Mathf.Pow(Mathf.Pow(currPosReal.x - obPos.x, 2) + Mathf.Pow(currPosReal.y - obPos.y, 2), -3f / 2) * (currPosReal - obPos);
                
                ng += -gDelta;//negtive gradient
            }
            ng = ng.normalized;
            // if(step==0)
            //     Debug.Log(ng);
            // if(step==steps-1)
            //     Debug.Log(ng);
            currPosReal += ng * 0.1f;
        }
        totalForce = ng;
    }
}
