using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;


public class ResetTrainAgent : Agent
{
    public GlobalConfiguration globalConfiguration;
    public float real_square_width;
    public float reset_angle = 0;
    public bool reset_flag;

    public int waypoint_count;
    public float distance;
    public float t,s;
    public int id;
    public int user_reset;
    public int boundary_reset;

    public RedirectionManager rm;
    private void FixedUpdate()
    {
        if(!reset_flag)
            distance+=0.02f/real_square_width;
    }
    public void set(){
        while(transform.Find("avatarRoot")!=null){
            var t = transform.Find("avatarRoot");
            t.tag = "Boundary"+id;
            t.name += id;
        }
        
    }
    public override void Initialize()
    {
        globalConfiguration = GetComponentInParent<GlobalConfiguration>();
        distance = 0.0f;
        t = 0.0f;
        s = 0.0f;
        user_reset = 0;
        boundary_reset = 0;
    }

    public override void OnEpisodeBegin()
    {
        distance = 0.0f;

        t = 0.0f;
        s = 0.0f;
        user_reset = 0;
        boundary_reset = 0;

        rm = GetComponent<RedirectionManager>();

        string name = this.gameObject.name;
        id = name[name.Length - 1] -'0' - 1;

        for(int i=0;i<5;i++)
            transform.Find("Tracking Space").Find("Plane").Find("Boundary").GetChild(i).tag = "Boundary"+id;

        while(transform.Find("avatarRoot")!=null){
            var t = transform.Find("avatarRoot");
            t.tag = "Boundary"+id;
            t.name += id;
        }
        
    }

    public override void CollectObservations(Unity.MLAgents.Sensors.VectorSensor sensor)
    {
        sensor.AddObservation(remap(rm.currPosReal.x,-real_square_width/2,+real_square_width/2,-1.0f,1.0f));
        sensor.AddObservation(remap(rm.currPosReal.z,-real_square_width/2,+real_square_width/2,-1.0f,1.0f));
        sensor.AddObservation(remap(rm.simulatedHead.localRotation.eulerAngles.y,0.0f,360.0f,-1.0f,1.0f));
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if(!rm.inReset && reset_flag){
            boundary_reset++;
            if(rm.user_trigger){
                user_reset++;
            }
            globalConfiguration.m_AgentGroup.AddGroupReward(-1);
            float a = Mathf.Clamp(actions.ContinuousActions[0],-1.0f,1.0f);
            reset_angle = remap(a,-1.0f,1.0f,0.0f,180.0f);
            reset_angle += (180.0f - rm.resetter.collisionangle);
            var resetter = rm.GetComponent<RRCResetter>();
            resetter.targetRealRotation=reset_angle;
            rm.OnResetTrigger();
        }
        
    }

    public void ActionWork(){
        reset_flag=true;
        globalConfiguration.m_AgentGroup.AddGroupReward(distance);
        distance=0.0f;
        Academy.Instance.EnvironmentStep();
        t++;
    }
    public float remap(float val, float in1, float in2, float out1, float out2)  //리맵하는 함수
    {
        return out1 + (val - in1) * (out2 - out1) / (in2 - in1);
    }
    //개발자(사용자)가 직접 명령을 내릴때 호출하는 메소드(주로 테스트용도 또는 모방학습에 사용)
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = Input.GetAxis("Horizontal");
    }

}
