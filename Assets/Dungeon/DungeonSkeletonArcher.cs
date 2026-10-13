using UnityEngine;

// Uses DungeonSkeleton's health/death contract, so room counts and player homing work unchanged.
public sealed class DungeonSkeletonArcher : MonoBehaviour
{
    DungeonSkeleton body;
    DungeonArcher player;
    CharacterController controller;
    Transform bow,arrow,leftArm,rightArm;
    LineRenderer stringLine;
    Material bone;
    Vector3 aim;
    float draw, cooldown=1, gait;
    bool charging;

    public void Initialize(DungeonSkeleton skeleton,DungeonArcher target)
    {
        body=skeleton;player=target;controller=GetComponent<CharacterController>();
        bone=body.boneRenderers[0].sharedMaterial;
        body.axeArm.gameObject.SetActive(false);body.freeArm.gameObject.SetActive(false);
        bow=new GameObject("Archer Bow").transform;bow.SetParent(body.model,false);
        bow.localPosition=new Vector3(-.32f,1.35f,.6f);
        for(int i=0;i<12;i++)
        {
            float a=i/12f,b=(i+1)/12f;
            DungeonArcher.Segment("Bow limb",bow,new Vector3(0,Mathf.Lerp(-.6f,.6f,a),-.18f*Mathf.Sin(a*Mathf.PI)),
                new Vector3(0,Mathf.Lerp(-.6f,.6f,b),-.18f*Mathf.Sin(b*Mathf.PI)),.045f,bone);
        }
        stringLine=new GameObject("Bowstring").AddComponent<LineRenderer>();
        stringLine.transform.SetParent(bow,false);stringLine.useWorldSpace=false;stringLine.positionCount=3;
        stringLine.sharedMaterial=Resources.Load<Material>("ArrowTrail");stringLine.startWidth=stringLine.endWidth=.012f;
        arrow=new GameObject("Nocked arrow").transform;arrow.SetParent(bow,false);
        DungeonArrow.CreateVisual(arrow,bone,bone);
        leftArm=new GameObject("Bow arm").transform;leftArm.SetParent(body.model,false);
        rightArm=new GameObject("Drawing arm").transform;rightArm.SetParent(body.model,false);
        DungeonArcher.Segment("Bone",leftArm,Vector3.zero,Vector3.up, .075f,bone);
        DungeonArcher.Segment("Bone",rightArm,Vector3.zero,Vector3.up, .075f,bone);
        Pose(0);
    }

    public void Tick(float dt)
    {
        if(player==null || DungeonStartScreen.IsPaused)return;
        Vector3 flat=Vector3.ProjectOnPlane(player.transform.position-transform.position,Vector3.up);
        float distance=flat.magnitude;
        if(flat.sqrMagnitude>.01f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(flat),150*dt);
        bool visible=Visible();
        cooldown=Mathf.Max(0,cooldown-dt);
        if(charging)
        {
            // Lock aim before release to let lateral movement dodge the shot.
            draw+=dt;
            if(draw<.7f)aim=player.transform.position+Vector3.up;
            if(!visible || distance>22){charging=false;draw=0;cooldown=.6f;}
            else if(draw>=1.1f)
            {
                var shot=new GameObject("Enemy Arrow");shot.transform.position=bow.position+transform.forward*.45f;
                shot.AddComponent<DungeonEnemyArrow>().Initialize(transform,(aim-shot.transform.position).normalized*15,bone);
                charging=false;draw=0;cooldown=1.7f;
            }
        }
        else if(visible && distance<=18 && cooldown<=0)
        {charging=true;draw=0;aim=player.transform.position+Vector3.up;}
        else if(distance>10 || distance<4)
        {
            Vector3 direction=flat.normalized*(distance<4?-1:1);
            controller.Move(direction*Mathf.Min(2.4f,player.moveSpeed*.6f)*dt);
            gait+=dt*7;
        }
        float step=charging?0:Mathf.Sin(gait)*18;
        body.leftLeg.localRotation=Quaternion.Euler(step,0,0);body.rightLeg.localRotation=Quaternion.Euler(-step,0,0);
        controller.Move(Vector3.down*10*dt);Pose(charging?Mathf.Clamp01(draw/1.1f):0);
        arrow.gameObject.SetActive(charging || cooldown<.8f);
    }
    bool Visible()
    {
        Vector3 from=transform.position+Vector3.up*1.4f,delta=player.transform.position+Vector3.up-from;
        foreach(var hit in Physics.RaycastAll(from,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
            if(!hit.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<DungeonArcher>()!=player)return false;
        return true;
    }
    void Pose(float amount)
    {
        Vector3 nock=new Vector3(0,0,-.12f-.32f*amount);
        stringLine.SetPosition(0,new Vector3(0,-.6f,0));stringLine.SetPosition(1,nock);stringLine.SetPosition(2,new Vector3(0,.6f,0));
        arrow.localPosition=nock+Vector3.forward*.3f;
        Arm(leftArm,new Vector3(-.35f,1.45f,0),bow.localPosition);
        Arm(rightArm,new Vector3(.35f,1.45f,0),bow.localPosition+nock);
    }
    static void Arm(Transform arm,Vector3 shoulder,Vector3 hand)
    {
        arm.localPosition=shoulder;arm.localRotation=Quaternion.FromToRotation(Vector3.up,hand-shoulder);
        arm.localScale=new Vector3(1,(hand-shoulder).magnitude,1);
    }
}

public sealed class DungeonEnemyArrow : MonoBehaviour
{
    Transform owner;
    Vector3 velocity;
    float age;
    public void Initialize(Transform shooter,Vector3 speed,Material material)
    {
        owner=shooter;velocity=speed;transform.rotation=Quaternion.LookRotation(speed);
        DungeonArrow.CreateVisual(transform,material,material);
        var trail=gameObject.AddComponent<TrailRenderer>();trail.sharedMaterial=Resources.Load<Material>("ArrowTrail");
        trail.time=.18f;trail.startWidth=.07f;trail.endWidth=0;
        trail.startColor=new Color(1,.45f,.16f);trail.endColor=new Color(1,.3f,.05f,0);
    }
    void Update()
    {
        if(DungeonStartScreen.BattleEnded){Destroy(gameObject);return;}
        if(!DungeonStartScreen.BattleStarted || DungeonStartScreen.IsPaused || Cursor.lockState!=CursorLockMode.Locked)return;
        float dt=Time.deltaTime;age+=dt;if(age>6){Destroy(gameObject);return;}
        var hits=Physics.SphereCastAll(transform.position,.06f,velocity.normalized,velocity.magnitude*dt,~0,QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
        foreach(var hit in hits)
        {
            if(owner!=null && hit.transform.IsChildOf(owner))continue;
            var player=hit.collider.GetComponentInParent<DungeonArcher>();
            if(player!=null)player.TakeDamage(1);
            Destroy(gameObject);return;
        }
        transform.position+=velocity*dt;
    }
}
