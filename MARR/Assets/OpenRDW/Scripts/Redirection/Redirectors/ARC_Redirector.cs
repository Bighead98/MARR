using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ARC_Redirector : Redirector {

    protected const float MOVEMENT_THRESHOLD = 0.2f; // meters per second. For 2. A linear movement rotation
    protected const float MAXIMUM_LINEAR_MOVEMENT_ROTATION_RATE = 15f;
    protected const float ROTATION_THRESHOLD = 1.5f; // degrees per second. For 3. An angular rotation
    protected const float MAXIMUM_ANGULAR_ROTATION_RATE = 30f;
    protected const float ANGLE_THRESHOLD_FOR_DAMPENING = 1f; // Angle threshold to apply dampening (degrees)
    protected const float DISTANCE_THRESHOLD_FOR_DAMPENING = 1.25f; // Distance threshold to apply dampening (meters)
    protected const float SMOOTHING_FACTOR = 0.125f; // Smoothing factor for redirection rotations


    protected const float TRANSLATIONALGAIN_MIN = 0.86f;
    protected const float TRANSLATIONALGAIN_MAX = 1.26f;

    protected const float CURVATURE_GAIN_CAP_DEGREES_PER_SECOND = 15;  // degrees per second
    protected const float ROTATION_GAIN_CAP_DEGREES_PER_SECOND = 30;  // degrees per second

    protected float CURVATURE_RADIUS;
    protected float MAX_CURVATURE_GAIN;
    protected float MIN_CURVATURE_GAIN;
    protected float previousMagnitude = 0f;

    protected Vector2 userPosition; // user localPosition
    protected Vector2 userDirection; // user local direction (localforward)


    protected List<float> List_ActualDistance3Way = new List<float>();
    protected List<float> List_VirtualDistance3Way = new List<float>();
    //private List<float> List_ActualDistance3Way_Pre = new List<float>();
    //private List<float> List_VirtualDistance3Way_Pre = new List<float>();

    protected List<float> List_ActualDistance20Way = new List<float>();
    protected List<float> List_VirtualDistance20Way = new List<float>();

    protected GameObject userTR_R;
    protected GameObject userTR_V;

    protected float dist_qq_sum_pre = 0.0f;

    // Start is called before the first frame update
    void Start()
    {
        initialize();
    }

    // Update is called once per frame
    void Update()
    {
        
    }



    protected virtual void initialize()
    {
        CURVATURE_RADIUS = globalConfiguration.CURVATURE_RADIUS;
        MAX_CURVATURE_GAIN = 1.0f / CURVATURE_RADIUS;
        MIN_CURVATURE_GAIN = -1.0f / CURVATURE_RADIUS;
        List<float> List_ActualDistance3Way = new List<float>();
        List<float> List_VirtualDistance3Way = new List<float>();
    //private List<float> List_ActualDistance3Way_Pre = new List<float>();
    //private List<float> List_VirtualDistance3Way_Pre = new List<float>();

        List<float> List_ActualDistance20Way = new List<float>();
        List<float> List_VirtualDistance20Way = new List<float>();
}

    public override void InjectRedirection()
    {
        //List<float> returnValue = new List<float>();
        Vector2 deltaPosition = redirectionManager.deltaPos;
        float speed = redirectionManager.deltaPos.magnitude / redirectionManager.GetDeltaTime();
        float deltaRotation = redirectionManager.deltaDir; // positive if rotate clockwise
        if (deltaPosition == Vector2.zero && deltaRotation == 0.0f)
        {
            //no Redirection
        }

        // define some variables for redirection
        //Transform2D realUserTransform = unit.GetRealUser().transform2D;
        //Transform2D virtualUserTransform = unit.GetVirtualUser().transform2D;
        userPosition = redirectionManager.currPosReal;
        userDirection = redirectionManager.currDir;

        GameObject userTR_R = getRealUserTransformObj();
        GameObject userTR_V = getVirtualUserTransformObj();

        Calc_NWay_Distances(userTR_R.transform, true, 3);
        Calc_NWay_Distances(userTR_V.transform, false, 3);
        Destroy(userTR_R);
        Destroy(userTR_V);
        float dist_qq_sum = 0.0f;

        if (List_ActualDistance3Way.Count == 3 && List_VirtualDistance3Way.Count == 3)
        {
            //dist(prox(p_phys), prox(p_virt))
            for (int i = 0; i < 3; i++)
            {
                dist_qq_sum += Mathf.Abs(List_ActualDistance3Way[i] - List_VirtualDistance3Way[i]);
            }

            //Debug.Log(dist_qq_sum);

        }
        else
        {
            Debug.LogError("ERROR : List_Distance3Way");
        }
        if (dist_qq_sum == 0.0f)
        {
            Debug.LogWarning("Noredirect");
            return;
            //returnValue.Add(1.0f); //no gain
        }

        //float translationGainMagnitude = Mathf.Clamp(Mathf.Abs(List_ActualDistance3Way[0]) / Mathf.Abs(List_VirtualDistance3Way[0]), TRANSLATIONALGAIN_MIN, TRANSLATIONALGAIN_MAX);
        float translationGainMagnitude = Mathf.Clamp(1 - Mathf.Abs(List_ActualDistance3Way[0]) / Mathf.Abs(List_VirtualDistance3Way[0]), redirectionManager.globalConfiguration.MIN_TRANS_GAIN,redirectionManager.globalConfiguration.MAX_TRANS_GAIN);
        //float translationGainMagnitude = Mathf.
        //Debug.Log("translationGainMagnitude " + translationGainMagnitude);
        //returnValue.Add(translationGainMagnitude);
        //return (GainType.Translation, returnValue);


        float misalignLeft = List_ActualDistance3Way[2] - List_VirtualDistance3Way[2]; // 음수일 수록 나쁨
        float misalignRight = List_ActualDistance3Way[1] - List_VirtualDistance3Way[1];
        float directionRotation = Mathf.Sign(deltaRotation); // If user is rotating to the left, directionRotation > 0. 그냥 부호임. 왼쪽: 1 or 오른쪽: -1.

        float curvatureGain = 0;
        float rotationGain = 0;
        //Debug.Log(misalignLeft + " " + misalignRight);

        var deltaTime = redirectionManager.GetDeltaTime();
        var maxRotationFromCurvatureGain = CURVATURE_GAIN_CAP_DEGREES_PER_SECOND * deltaTime;
        var maxRotationFromRotationGain = ROTATION_GAIN_CAP_DEGREES_PER_SECOND * deltaTime;

        var rotationFromCurvatureGain = Mathf.Rad2Deg * (redirectionManager.deltaPos.magnitude / redirectionManager.globalConfiguration.CURVATURE_RADIUS);


        float desiredRotateDirection = 0;

        if (misalignLeft > misalignRight) // If the target is to the left of the user,
        {
            desiredRotateDirection = 1;
            curvatureGain = Mathf.Min(rotationFromCurvatureGain * Mathf.Abs(misalignLeft), maxRotationFromCurvatureGain);
            //curvatureGain = HODGSON_MIN_CURVATURE_GAIN;
            //curvatureGain = Mathf.Min(1.0f, Mathf.Min(1.0f, Mathf.Abs(misalignLeft)) * HODGSON_MIN_CURVATURE_GAIN);
            //curvatureGain = Mathf.Max(-maxRotationFromCurvatureGain , MIN_CURVATURE_GAIN * redirectionManager.deltaPos.magnitude * Mathf.Rad2Deg);
            //curvatureGain = Mathf.Min(1.0f, Mathf.Min(1.0f, MIN_CURVATURE_GAIN * redirectionManager.deltaPos.magnitude * Mathf.Rad2Deg));

        }
        else
        {
            desiredRotateDirection = -1;
            curvatureGain = Mathf.Min(rotationFromCurvatureGain * Mathf.Abs(misalignRight), maxRotationFromCurvatureGain);
            //curvatureGain = HODGSON_MAX_CURVATURE_GAIN; //ARC 대부분 max값을 적용한다고하니, 그리고 우선 시뮬이니까 scalinFactor는 제외함
            //curvatureGain = Mathf.Min(1.0f, Mathf.Min(1.0f, Mathf.Abs(misalignRight)) * HODGSON_MAX_CURVATURE_GAIN);
            //curvatureGain = Mathf.Min(maxRotationFromCurvatureGain, MAX_CURVATURE_GAIN * redirectionManager.deltaPos.magnitude * Mathf.Rad2Deg);
            //curvatureGain = Mathf.Min(1.0f, Mathf.Min(1.0f,  MAX_CURVATURE_GAIN * redirectionManager.deltaPos.magnitude * Mathf.Rad2Deg));
        }
        //curvatureGain = desiredRotateDirection * Mathf.Min(rotationFromCurvatureGain, maxRotationFromCurvatureGain);
        //curvatureGain = desiredRotateDirection * curvatureGain;
        Debug.Log("curvatureGain : " + curvatureGain);
        float frameDiff = dist_qq_sum - dist_qq_sum_pre;
        Debug.Log("frameDiff : " + frameDiff);
        dist_qq_sum_pre = dist_qq_sum;
        float rotationGainDirection = 0;
        if (frameDiff > 0) // if user rotates away from the target (if their direction are opposite),
        {
            //rotationGain = Mathf.Max(globalConfiguration.MIN_ROT_GAIN * deltaRotation, -maxRotationFromRotationGain);
            //rotationGain = globalConfiguration.MIN_ROT_GAIN * deltaRotation;
            rotationGain = Mathf.Min(Mathf.Abs(deltaRotation * redirectionManager.globalConfiguration.MIN_ROT_GAIN), maxRotationFromRotationGain);
            rotationGainDirection = Mathf.Sign(deltaRotation);

        }
        else if (frameDiff < 0)
        {
            //rotationGain = Mathf.Min(globalConfiguration.MAX_ROT_GAIN * deltaRotation, maxRotationFromRotationGain); //MAX_ROTATION_GAIN
            //rotationGain = globalConfiguration.MAX_ROT_GAIN * deltaRotation;
            rotationGain = Mathf.Min(Mathf.Abs(deltaRotation * redirectionManager.globalConfiguration.MAX_ROT_GAIN), maxRotationFromRotationGain);
            rotationGainDirection = (-1) * Mathf.Sign(deltaRotation);
        }
        else
        {
            rotationGain = 1.0f;
        }

        // select the largest magnitude
        float rotationMagnitude = 0, curvatureMagnitude = 0;

        bool isCurvatureSelected = true;
        Debug.Log("speed : " + speed);
        if (speed > MOVEMENT_THRESHOLD)
        {
            curvatureMagnitude =  desiredRotateDirection * curvatureGain;
            Debug.Log("curvatureMagnitude : " + curvatureMagnitude);// 2. A linear movement rotation rate. 여기에 delta T를 곱해야 Rotation이 됨.
            //if (curvatureMagnitude > 0)
            //{
            //    curvatureMagnitude = Mathf.Clamp(curvatureMagnitude, 0, MAXIMUM_LINEAR_MOVEMENT_ROTATION_RATE);
            //}
            //else
            //{
            //    curvatureMagnitude = Mathf.Clamp(curvatureMagnitude, -MAXIMUM_LINEAR_MOVEMENT_ROTATION_RATE, 0);
            //}
        }
        else if (Mathf.Abs(deltaRotation) >= ROTATION_THRESHOLD)
        {
            rotationMagnitude = rotationGain; // 3. An angular rotation rate. 여기에 delta T를 곱해야 Rotation이 됨.
            Debug.Log("rotationMagnitude : " + rotationMagnitude);
            //if (rotationMagnitude > 0)
            //{
            //    rotationMagnitude = Mathf.Clamp(rotationMagnitude, 0, MAXIMUM_ANGULAR_ROTATION_RATE);
            //}
            //else
            //{
            //    rotationMagnitude = Mathf.Clamp(rotationMagnitude, -MAXIMUM_ANGULAR_ROTATION_RATE, 0);
            //}

            isCurvatureSelected = false;
        }
        else
        {
            // no Redirection 
            return;
        }

        //float selectedMagnitude = Mathf.Max(Mathf.Abs(rotationMagnitude), Mathf.Abs(curvatureMagnitude)); // selectedMagnitude is ABS(절대값)
        //bool isCurvatureSelected = Mathf.Abs(curvatureMagnitude) > Mathf.Abs(rotationMagnitude);

        //smoothing
        float finalRotation = (1.0f - SMOOTHING_FACTOR) * previousMagnitude + SMOOTHING_FACTOR * Mathf.Abs(rotationMagnitude);
        previousMagnitude = finalRotation;

        // apply final redirection
        if (!isCurvatureSelected)
        {
            //Debug.Log("AA");

            //float direction = directionRotation;
            //returnValue.Add(finalRotation * direction);
            //returnValue.Add(translationGainMagnitude);
            //InjectRotation(finalRotation);

            //InjectRotation(finalRotation * desiredRotateDirection);
            InjectRotation(finalRotation * rotationGainDirection);
            //return (GainType.Rotation, returnValue);
        }
        else
        {
            //Debug.Log("BB");

            //float direction = -Mathf.Sign(curvatureGain);
            //returnValue.Add(curvatureMagnitude * direction);
            //returnValue.Add(curvatureMagnitude);
            //returnValue.Add(translationGainMagnitude);

            InjectCurvature(curvatureMagnitude);


            //Debug.Log("translationGainMagnitude " + translationGainMagnitude);
            //return (GainType.Curvature, returnValue);
        }
        InjectTranslation(translationGainMagnitude * redirectionManager.deltaPos);


        

    }

    public GameObject getRealUserTransformObj()
    {
        GameObject userTR_R = new GameObject("userTR_R");
        userTR_R.transform.position = redirectionManager.currPos;
        userTR_R.transform.rotation = Quaternion.LookRotation( redirectionManager.currDir);

        return userTR_R;
    }

    public GameObject getRealUserTransformObj(Vector3 position, Quaternion rotation)
    {
        GameObject userTR_R = new GameObject("userTR_R");
        userTR_R.transform.position = position;
        userTR_R.transform.rotation = rotation;

        return userTR_R;
    }

    //public void delRealUserTransformObj()
    //{
    //    Destroy(userTR_R);
    //    return;
    //}

    //public void delVirtualUserTransformObj()
    //{
    //    Destroy(userTR_V);
    //    return;
    //}

    public GameObject getVirtualUserTransformObj()
    {

        GameObject userTR_V = new GameObject("userTR_V");
        userTR_V.transform.position = redirectionManager.currPos;
        userTR_V.transform.rotation = Quaternion.LookRotation(redirectionManager.currDir);

        return userTR_V;
    }

    public GameObject getVirtualUserTransformObj(Vector3 position, Quaternion rotation)
    {
        GameObject userTR_V = new GameObject("userTR_V");
        userTR_V.transform.position = position;
        userTR_V.transform.rotation = rotation;

        return userTR_V;
    }


    public void Calc_NWay_Distances(Transform _transform, bool bActual, int N_waycount, out List<float> List_Distance)
    {
        List_Distance = new List<float>();
        float distance = 0.0f;
        RaycastHit hit;
        List<Vector3> direction = new List<Vector3>();

        int layerMask;
        int totalUserCount = globalConfiguration.avatarNum;
        if (bActual)
        {
            layerMask = 1 << LayerMask.NameToLayer("PhysicalWall"); // Physical Wall
        }
        else
        {
            layerMask = 1 << LayerMask.NameToLayer("VirtualWall") ;
        }
        Debug.Log("layerMask : " + layerMask);

        List_Distance.Clear();

        if (N_waycount == 3)
        {
            direction.Add(_transform.forward);
            direction.Add(_transform.right);
            direction.Add(-_transform.right);

        }
        else if (N_waycount == 20)
        {
            for (int i = 0; i < 20; i++)
            {
                Vector3 result = Quaternion.AngleAxis(18 * i, Vector3.up) * _transform.forward;
                direction.Add(result);
            }
        }

        for (int i = 0; i < direction.Count; i++)
        {

            if (Physics.Raycast(_transform.position, direction[i], out hit, 1000.0f, layerMask))
            {
                if (bActual)
                {
                    if (hit.collider.gameObject.layer == LayerMask.NameToLayer("PhysicalWall"))
                    {
                        distance = hit.distance;
                        //Debug.Log(hit.collider.gameObject.name);
                        //Debug.DrawLine(_transform.position, _transform.position + direction[i], Color.red, Time.deltaTime);
                        //Debug.Log(hit.transform.gameObject.name);
                    }
                    else
                    {
                        if (hit.collider.gameObject.layer == LayerMask.NameToLayer("PhysicalUser"))
                        {
                            distance = hit.distance;
                            break;
                        }

                    }
                    Debug.DrawLine(_transform.position + Vector3.up * 0.1f, hit.point + Vector3.up * 0.1f, Color.green);
                }
                else
                {
                    if (hit.collider.gameObject.layer == LayerMask.NameToLayer("VirtualWall"))
                    {
                        distance = hit.distance;
                        //Debug.DrawLine(_transform.position, _transform.position + direction[i], Color.red, Time.deltaTime);
                    }
                    //else
                    //{
                    //    for (int j = 0; j < totalUserCount; j++)
                    //    {
                    //        if (hit.collider.gameObject.layer == LayerMask.NameToLayer("VirtualUser" + j))
                    //        {
                    //            distance = hit.distance;
                    //            break;
                    //        }
                    //    }
                    //}
                    Debug.Log("Virtual Ray hit : " + hit.transform.gameObject.name);
                    Debug.DrawLine(_transform.position + Vector3.up * 0.1f, hit.point + Vector3.up * 0.1f, Color.red);
                }

            }

            //if (N_waycount == 3)
            //{
            //    if (bActual)
            //    {
            //        List_Distance.Add(distance);
            //    }
            //    else
            //    {
            //        List_Distance.Add(distance);
            //    }
            //}
            //else if (N_waycount == 20)
            //{
            //    if (bActual)
            //    {
            //        List_Distance.Add(distance);
            //    }
            //    else
            //    {
            //        List_Distance.Add(distance);
            //    }
            //}
            List_Distance.Add(distance);
            distance = 0.0f;
        }
    }

    public void Calc_NWay_Distances(Transform _transform, bool bActual, int N_waycount)
    {
        float distance = 0.0f;
        RaycastHit hit;
        List<Vector3> direction = new List<Vector3>();

        int layerMask;
        int totalUserCount = globalConfiguration.avatarNum;
        if (bActual)
        {
            layerMask = 1 << LayerMask.NameToLayer("PhysicalWall"); // Physical Wall
        }
        else
        {
            layerMask = 1 << LayerMask.NameToLayer("VirtualWall");
        }
        Debug.Log("layerMask : " + layerMask);

        if (N_waycount == 3)
        {
            direction.Add(_transform.forward);
            direction.Add(_transform.right);
            direction.Add(-_transform.right);

            if (bActual)
            {
                List_ActualDistance3Way.Clear();
            }
            else
            {
                List_VirtualDistance3Way.Clear();
            }
        }
        else if (N_waycount == 20)
        {
            for (int i = 0; i < 20; i++)
            {
                Vector3 result = Quaternion.AngleAxis(18 * i, Vector3.up) * _transform.forward;
                direction.Add(result);
            }


            if (bActual)
            {
                List_ActualDistance20Way.Clear();
            }
            else
            {
                List_VirtualDistance20Way.Clear();
            }
        }

        for (int i = 0; i < direction.Count; i++)
        {
            if (Physics.Raycast(_transform.position, direction[i], out hit, 1000.0f, layerMask))
            {
                if (bActual)
                {
                    if (hit.collider.gameObject.layer == LayerMask.NameToLayer("PhysicalWall"))
                    {
                        distance = hit.distance;
                        //Debug.Log(hit.collider.gameObject.name);
                        //Debug.DrawLine(_transform.position, _transform.position + direction[i], Color.red, Time.deltaTime);
                        //Debug.Log(hit.transform.gameObject.name);
                    }
                    else
                    {
                        if (hit.collider.gameObject.layer == LayerMask.NameToLayer("PhysicalUser"))
                        {
                            distance = hit.distance;
                            break;
                        }

                    }
                    Debug.DrawLine(_transform.position  + Vector3.up * 0.1f, hit.point + Vector3.up * 0.1f, Color.green);
                }
                else
                {
                    if (hit.collider.gameObject.layer == LayerMask.NameToLayer("VirtualWall"))
                    {
                        distance = hit.distance;
                        //Debug.DrawLine(_transform.position, _transform.position + direction[i], Color.red, Time.deltaTime);
                    }
                    //else
                    //{
                    //    for (int j = 0; j < totalUserCount; j++)
                    //    {
                    //        if (hit.collider.gameObject.layer == LayerMask.NameToLayer("VirtualUser" + j))
                    //        {
                    //            distance = hit.distance;
                    //            break;
                    //        }
                    //    }
                    //}
                    Debug.DrawLine(_transform.position + Vector3.up * 0.1f, hit.point + Vector3.up * 0.1f, Color.red);
                }

            }

            if (N_waycount == 3)
            {
                if (bActual)
                {
                    List_ActualDistance3Way.Add(distance);
                }
                else
                {
                    List_VirtualDistance3Way.Add(distance);
                }
            }
            else if (N_waycount == 20)
            {
                if (bActual)
                {
                    List_ActualDistance20Way.Add(distance);
                }
                else
                {
                    List_VirtualDistance20Way.Add(distance);
                }
            }

            distance = 0.0f;
        }
    }
}
