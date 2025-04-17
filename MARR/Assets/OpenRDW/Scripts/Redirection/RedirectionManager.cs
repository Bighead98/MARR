using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public class RedirectionManager : MonoBehaviour {
    public static readonly float MaxSamePosTime = 50000;//the max time(in seconds) the avatar can stand on the same position, exceeds this value will make data invalid (stuck in one place)

    public enum RedirectorChoice { None, S2C, S2O, Zigzag, ThomasAPF, MessingerAPF, DynamicAPF, DeepLearning, PassiveHapticAPF };
    public enum ResetterChoice { None, TwoOneTurn, APF, RRC, OPT, R2C, R2G, SFR2G };

    public int user_reset_num;
    public int boundary_reset_num;

    [Tooltip("The game object that is being physically tracked (probably user's head)")]
    public Transform headTransform;

    [Tooltip("Subtle Redirection Controller")]
    public RedirectorChoice redirectorChoice;

    [Tooltip("Overt Redirection Controller")]
    public ResetterChoice resetterChoice;

    // Experiment Variables
    [HideInInspector]
    public System.Type redirectorType = null;
    [HideInInspector]
    public System.Type resetterType = null;

    public int reset_num = 0;
    public int user_reset = 0;
    //record the time standing on the same position
    private float samePosTime;

    [HideInInspector]
    public GlobalConfiguration globalConfiguration;

    [HideInInspector]
    public Transform body;
    [HideInInspector]
    public Transform trackingSpace;
    [HideInInspector]
    public Transform simulatedHead;

    public ResetTrainAgent rrcagent;

    [HideInInspector]
    public Redirector redirector;
    [HideInInspector]
    public Resetter resetter;
    [HideInInspector]
    public TrailDrawer trailDrawer;
    [HideInInspector]
    public MovementManager movementManager;
    [HideInInspector]
    public SimulatedWalker simulatedWalker;
    [HideInInspector]
    public KeyboardController keyboardController;
    [HideInInspector]
    public HeadFollower bodyHeadFollower;

    [HideInInspector]
    public float priority;

    [HideInInspector]
    public Vector3 currPos, currPosReal, prevPos, prevPosReal;
    [HideInInspector]
    public Vector3 currDir, currDirReal, prevDir, prevDirReal;
    [HideInInspector]
    public Vector3 deltaPos;//the vector of the previous position to the current position
    [HideInInspector]
    public float deltaDir;//horizontal angle change in degrees (positive if rotate clockwise)
    [HideInInspector]
    public Transform targetWaypoint;

    [HideInInspector]
    public bool inReset = false;

    [HideInInspector]
    public bool ifJustEndReset = false;//if just finishes reset, if true, execute redirection once then judge if reset, Prevent infinite loops

    [HideInInspector]
    public float redirectionTime;//total time passed when using subtle redirection

    [HideInInspector]
    public float walkDist = 0;//walked virtual distance

    public bool user_trigger;
    public bool trigger;

    private NetworkManager networkManager;
    void Awake()
    {
        redirectorType = RedirectorChoiceToRedirector(redirectorChoice);
        resetterType = ResetterChoiceToResetter(resetterChoice);
        
        rrcagent = GetComponent<ResetTrainAgent>();
        rrcagent.set();
        globalConfiguration = GetComponentInParent<GlobalConfiguration>();
        networkManager = globalConfiguration.GetComponentInChildren<NetworkManager>(true);

        body = transform.Find("Body");
        trackingSpace = transform.Find("Tracking Space");
        simulatedHead = GetSimulatedAvatarHead();

        movementManager = this.gameObject.GetComponent<MovementManager>();                      

        GetRedirector();
        GetResetter();

        trailDrawer = GetComponent<TrailDrawer>();
        simulatedWalker = simulatedHead.GetComponent<SimulatedWalker>();
        keyboardController = simulatedHead.GetComponent<KeyboardController>();
        
        bodyHeadFollower = body.GetComponent<HeadFollower>();
        
        SetReferenceForResetter();                                                     

        if (globalConfiguration.movementController != GlobalConfiguration.MovementController.HMD)
        {
            headTransform = simulatedHead;
        }
        else {
            //hide avatar body
            //body.gameObject.SetActive(false);
        }


        // Resetter needs ResetTrigger to be initialized before initializing itself
        if (resetter != null)
            resetter.Initialize();


        samePosTime = 0;
        user_trigger=false;
        trigger=false;

    }

    //modify these trhee functions when adding a new redirector
    public System.Type RedirectorChoiceToRedirector(RedirectorChoice redirectorChoice)
    {
        switch (redirectorChoice)
        {
            case RedirectorChoice.None:
                return typeof(NullRedirector);
            case RedirectorChoice.S2C:
                return typeof(S2CRedirector);
            case RedirectorChoice.S2O:
                return typeof(S2ORedirector);
            case RedirectorChoice.Zigzag:
                return typeof(ZigZagRedirector);
            case RedirectorChoice.ThomasAPF:
                return typeof(ThomasAPF_Redirector);
            case RedirectorChoice.MessingerAPF:
                return typeof(MessingerAPF_Redirector);
            case RedirectorChoice.DynamicAPF:
                return typeof(DynamicAPF_Redirector);
            case RedirectorChoice.DeepLearning:
                return typeof(DeepLearning_Redirector);
            case RedirectorChoice.PassiveHapticAPF:
                return typeof(PassiveHapticAPF_Redirector);
        }
        return typeof(NullRedirector);
    }
    public static RedirectorChoice RedirectorToRedirectorChoice(System.Type redirector)
    {
        if (redirector.Equals(typeof(NullRedirector)))
            return RedirectorChoice.None;
        else if (redirector.Equals(typeof(S2CRedirector)))
            return RedirectorChoice.S2C;
        else if (redirector.Equals(typeof(S2ORedirector)))
            return RedirectorChoice.S2O;
        else if (redirector.Equals(typeof(ZigZagRedirector)))
            return RedirectorChoice.Zigzag;
        else if (redirector.Equals(typeof(ThomasAPF_Redirector)))
            return RedirectorChoice.ThomasAPF;
        else if (redirector.Equals(typeof(MessingerAPF_Redirector)))
            return RedirectorChoice.MessingerAPF;
        else if (redirector.Equals(typeof(DynamicAPF_Redirector)))
            return RedirectorChoice.DynamicAPF;
        else if (redirector.Equals(typeof(DeepLearning_Redirector)))
            return RedirectorChoice.DeepLearning;
        else if (redirector.Equals(typeof(PassiveHapticAPF_Redirector)))
            return RedirectorChoice.PassiveHapticAPF;
        return RedirectorChoice.None;
    }
    public static System.Type DecodeRedirector(string s)
    {
        switch (s.ToLower())
        {
            case "null":
                return typeof(NullRedirector);
            case "s2c":
                return typeof(S2CRedirector);
            case "s2o":
                return typeof(S2ORedirector);
            case "zigzag":
                return typeof(ZigZagRedirector);
            case "thomasapf":
                return typeof(ThomasAPF_Redirector);
            case "messingerapf":
                return typeof(MessingerAPF_Redirector);
            case "dynamicapf":
                return typeof(DynamicAPF_Redirector);
            case "deeplearning":
                return typeof(DeepLearning_Redirector);
            case "passivehapticapf":
                return typeof(PassiveHapticAPF_Redirector);
            default:
                return typeof(NullRedirector);
        }
    }
    //modify these trhee functions when adding a new resetter
    public static System.Type ResetterChoiceToResetter(ResetterChoice resetterChoice)
    {
        switch (resetterChoice)
        {
            case ResetterChoice.None:
                return typeof(NullResetter);
            case ResetterChoice.TwoOneTurn:
                return typeof(TwoOneTurnResetter);
            case ResetterChoice.APF:
                return typeof(APF_Resetter);
            case ResetterChoice.RRC:
                return typeof(RRCResetter);
            case ResetterChoice.OPT:
                return typeof(OPTResetter);
            case ResetterChoice.R2C:
                return typeof(R2CResetter);
            case ResetterChoice.R2G:
                return typeof(R2G_Resetter);
            case ResetterChoice.SFR2G:
                return typeof(SFR2G_Resetter);
        }
        return typeof(NullResetter);
    }
    public static ResetterChoice ResetterToResetChoice(System.Type reset)
    {
        if (reset.Equals(typeof(NullResetter)))
            return ResetterChoice.None;
        else if (reset.Equals(typeof(TwoOneTurnResetter)))
            return ResetterChoice.TwoOneTurn;
        else if (reset.Equals(typeof(APF_Resetter)))
            return ResetterChoice.APF;
        else if (reset.Equals(typeof(RRCResetter)))
            return ResetterChoice.RRC;
        else if (reset.Equals(typeof(OPTResetter)))
            return ResetterChoice.OPT;
        else if (reset.Equals(typeof(R2CResetter)))
            return ResetterChoice.R2C;
        else if (reset.Equals(typeof(R2G_Resetter)))
            return ResetterChoice.R2G;
        else if (reset.Equals(typeof(SFR2G_Resetter)))
            return ResetterChoice.SFR2G;
        return ResetterChoice.None;
    }
    public static System.Type DecodeResetter(string s)
    {
        switch (s.ToLower())
        {
            case "null":
                return typeof(NullResetter);
            case "twooneturn":
                return typeof(TwoOneTurnResetter);
            case "apf":
                return typeof(APF_Resetter);
            case "rrc":
                return typeof(RRCResetter);
            case "opt":
                return typeof(OPTResetter);
            case "r2c":
                return typeof(R2CResetter);
            case "r2g":
                return typeof(R2G_Resetter);
            case "sfr2g":
                return typeof(SFR2G_Resetter);
            default:
                return typeof(NullResetter);
        }
    }

    public Transform GetSimulatedAvatarHead() {
        return transform.Find("Simulated Avatar").Find("Head");
    }
    public bool IfWaitTooLong() {
        return  samePosTime > MaxSamePosTime;
    }
    
    public void Initialize() {        
        samePosTime = 0;
        redirectionTime = 0;
        UpdatePreviousUserState();
        UpdateCurrentUserState();
        inReset = false;
        ifJustEndReset = true;
    }
    public void UpdateRedirectionTime() {
        if (!inReset)
            redirectionTime += globalConfiguration.GetDeltaTime();
    }

    //make one step redirection: redirect or reset
    public void MakeOneStepRedirection() {
        UpdateCurrentUserState();

        //invalidData
        if (movementManager.ifInvalid)
            return;
        //do not redirect other avatar's transform during networking mode
        if (globalConfiguration.networkingMode && movementManager.avatarId != networkManager.avatarId)
            return;

        if (currPos.Equals(prevPos))
        {
            //used in auto simulation mode and there are unfinished waypoints
            if (globalConfiguration.movementController == GlobalConfiguration.MovementController.AutoPilot && !movementManager.ifMissionComplete)
            {
                //accumulated time for standing on the same position
                samePosTime += 1.0f / globalConfiguration.targetFPS;
            }
        }
        else {
            samePosTime = 0;//clear accumulated time
        }        

        CalculateStateChanges();               
        
        if (resetter != null && !inReset && resetter.IsResetRequired()&& !ifJustEndReset)
        {
            if(trigger)
                boundary_reset_num++;
            if(user_trigger)
                user_reset_num++;
            //Debug.LogWarning("Reset Aid Helped!");
            if(resetterChoice==ResetterChoice.RRC){
                rrcagent.ActionWork();
            }
            else
                OnResetTrigger();
            
        }
        
        if (inReset)
        {
            if (resetter != null)
            {
                resetter.InjectResetting();
            }
        }
        else
        {                        
            if (redirector != null)
            {
                redirector.InjectRedirection();
            }
            ifJustEndReset = false;
            rrcagent.reset_flag=false;            
        }        

        UpdatePreviousUserState();

        UpdateBodyPose();        
    }

    void UpdateBodyPose()
    {
        body.position = Utilities.FlattenedPos3D(headTransform.position);
        body.rotation = Quaternion.LookRotation(Utilities.FlattenedDir3D(headTransform.forward), Vector3.up);
    }

    void SetReferenceForRedirector()
    {
        if (redirector != null)
            redirector.redirectionManager = this;
    }

    void SetReferenceForResetter()
    {
        if (resetter != null)
            resetter.redirectionManager = this;
        
    }

    void SetReferenceForSimulationManager()
    {
        if (movementManager != null)
        {
            movementManager.redirectionManager = this;
        }
    }

    void GetRedirector()
    {
        redirector = this.gameObject.GetComponent<Redirector>();
        if (redirector == null)
            this.gameObject.AddComponent<NullRedirector>();
        redirector = this.gameObject.GetComponent<Redirector>();
    }

    void GetResetter()
    {
        resetter = this.gameObject.GetComponent<Resetter>();
        if (resetter == null)
            this.gameObject.AddComponent<NullResetter>();
        resetter = this.gameObject.GetComponent<Resetter>();
    }


    void GetTrailDrawer()
    {
        trailDrawer = this.gameObject.GetComponent<TrailDrawer>();
    }

    void GetSimulationManager()
    {
        movementManager = this.gameObject.GetComponent<MovementManager>();
    }

    void GetSimulatedWalker()
    {
        simulatedWalker = simulatedHead.GetComponent<SimulatedWalker>();
    }

    void GetKeyboardController()
    {
        keyboardController = simulatedHead.GetComponent<KeyboardController>();
    }

    void GetBodyHeadFollower()
    {
        bodyHeadFollower = body.GetComponent<HeadFollower>();
    }

    void GetBody()
    {
        body = transform.Find("Body");
    }

    void GetTrackedSpace()
    {
        trackingSpace = transform.Find("Tracking Space");
    }

    void GetSimulatedHead()
    {
        simulatedHead = transform.Find("Simulated User").Find("Head");
    }

    void GetTargetWaypoint()
    {
        targetWaypoint = transform.Find("Target Waypoint").gameObject.transform;
    }

    public void UpdateCurrentUserState()
    {    
        currPos = Utilities.FlattenedPos3D(headTransform.position);//only consider head position
        currPosReal = GetPosReal(currPos);        
        currDir = Utilities.FlattenedDir3D(headTransform.forward);
        currDirReal = GetDirReal(currDir);
        walkDist += (Utilities.FlattenedPos2D(currPos) - Utilities.FlattenedPos2D(prevPos)).magnitude;

        //Debug.Log("walkDist: " + walkDist);
        //Debug.Log("current velocity: " + (currPos - prevPos).magnitude / GetDeltaTime());
    }

    void UpdatePreviousUserState()
    {
        prevPos = Utilities.FlattenedPos3D(headTransform.position);
        prevPosReal = GetPosReal(prevPos);
        prevDir = Utilities.FlattenedDir3D(headTransform.forward);
        prevDirReal = GetDirReal(prevDir);
    }
    public Vector3 GetPosReal(Vector3 pos) {
        return Utilities.GetRelativePosition(pos, trackingSpace.transform);
    }
    public Vector3 GetDirReal(Vector3 dir)
    {
        return Utilities.FlattenedDir3D(Utilities.GetRelativeDirection(dir, transform));
    }

    void CalculateStateChanges()
    {
        deltaPos = currPos - prevPos;
        deltaDir = Utilities.GetSignedAngle(prevDir, currDir);
        //Debug.Log(string.Format("prevDir:{0}, currDir:{1}, deltaDir:{2}", prevDir.ToString("f3"), currDir.ToString("f3"), deltaDir));
    }

    public void OnResetTrigger()
    {            
        resetter.InitializeReset();
        inReset = true;
        //Debug.Log(reset_num);
        //Debug.Log("OnResetTrigger");
        //record one reset operation
        globalConfiguration.statisticsLogger.Event_Reset_Triggered(movementManager.avatarId);
        trigger=false;
        user_trigger=false;
    }

    public void OnResetEnd()
    {                
        resetter.EndReset();
        inReset = false;
        ifJustEndReset = true;

        float v_area = 0.0f;
        float t_area = 0.0f;

        Vector3 tb = new Vector3(headTransform.position.x, 0.5f, headTransform.position.z);
        for(float i=-180.0f;i<180.0f;i+=1.0f){
            Vector3 a = new Vector3(1.0f,0.0f,0.0f);
            a = (Quaternion.Euler(0,headTransform.rotation.eulerAngles.y-90+i,0)) * a;
            RaycastHit[] hit = Physics.RaycastAll(tb, a);
            float d = 1000.0f;
            for(int j=0;j<hit.Length;j++){
                if(hit[j].transform.tag=="Boundary"+movementManager.avatarId){
                    if(d>hit[j].distance)
                        d=hit[j].distance;
                }
            }
            // Debug.DrawRay(tb, a*d, Color.green);
            float angles = (1.0f) * Mathf.Deg2Rad;
            if(i>=-22.5f && i<22.5f)
                v_area+= 1.0f/2.0f * d * d * angles;
            t_area+=1.0f/2.0f * d * d * angles;
            
        }
        // Debug.Log("Vis"+v_area+"total"+t_area);
        globalConfiguration.m_AgentGroup.AddGroupReward(rrcagent.remap(v_area,0.0f,
        t_area,0.0f,1.0f));
    }

    public void RemoveRedirector()
    {
        redirector = gameObject.GetComponent<Redirector>();
        if (redirector != null)
            Destroy(redirector);
        redirector = null;        
    }

    public void UpdateRedirector(System.Type redirectorType)
    {
        RemoveRedirector();
        redirector = (Redirector) gameObject.AddComponent(redirectorType);
        SetReferenceForRedirector();
    }

    public void RemoveResetter()
    {
        resetter = gameObject.GetComponent<Resetter>();
        if (resetter != null)
            Destroy(resetter);
        resetter = null;
    }

    public void UpdateResetter(System.Type resetterType)
    {
        RemoveResetter();
        if (resetterType != null)
        {
            resetter = (Resetter) gameObject.AddComponent(resetterType);            
            SetReferenceForResetter();
            if (resetter != null)
                resetter.Initialize();
        }
    }
    public float GetDeltaTime() {
        return globalConfiguration.GetDeltaTime();
    }
}
