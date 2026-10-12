using UnityEngine;

public interface IDungeonDamageable { void TakeDamage(float damage); }
public class DungeonArrow : MonoBehaviour
{
    public enum ShotKind { Normal, Power, Split, Homing }
    public const float HomingDamageRatio = .4f;
    public float Damage { get; private set; }
    public bool HasHit { get; private set; }
    public ShotKind Kind { get; private set; }
    public MonoBehaviour Target { get; private set; }
    Vector3 velocity;
    Collider owner;
    float age, searchDelay;
    Material shaftMaterial, tipMaterial;
    TrailRenderer flightTrail;
    TrailRenderer coreTrail;

    public void Initialize(Vector3 initialVelocity, float damage, Collider shooter, Material shaft, Material tip,
        ShotKind kind = ShotKind.Normal, MonoBehaviour target = null)
    {
        velocity=initialVelocity; Damage=damage; owner=shooter; Kind=kind; Target=target;
        shaftMaterial=shaft; tipMaterial=tip;
        CreateVisual(transform,shaft,tip);
        if(kind==ShotKind.Homing) transform.localScale=Vector3.one*.5f;
        var g=new GameObject("Arrow Flight Trail");g.transform.SetParent(transform,false);
        flightTrail=g.AddComponent<TrailRenderer>();
        flightTrail.sharedMaterial=Resources.Load<Material>("ArrowTrail");
        flightTrail.time=kind==ShotKind.Power?.24f:kind==ShotKind.Homing?.18f:.12f;
        flightTrail.minVertexDistance=.08f;
        flightTrail.widthCurve=AnimationCurve.Linear(0,kind==ShotKind.Power?.32f:kind==ShotKind.Homing?.055f:.085f,1,0);
        flightTrail.numCapVertices=3;flightTrail.numCornerVertices=2;
        flightTrail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;flightTrail.receiveShadows=false;
        Color front=new Color(1,.94f,.65f),tail=new Color(1,.55f,.15f);
        if(kind==ShotKind.Power){front=new Color(1,.2f,.12f);tail=new Color(.9f,.015f,.01f);}
        if(kind==ShotKind.Split||kind==ShotKind.Homing){front=new Color(.25f,.8f,1);tail=new Color(.03f,.2f,1);}
        var gradient=new Gradient();
        gradient.SetKeys(new[]{new GradientColorKey(front,0),new GradientColorKey(tail,1)},new[]{new GradientAlphaKey(.95f,0),new GradientAlphaKey(0,1)});
        flightTrail.colorGradient=gradient;flightTrail.AddPosition(transform.position);
        if(kind==ShotKind.Power)
        {
            DungeonPowerShotVfx.AddBolt(transform);
            DungeonPowerShotVfx.Burst(transform.position,velocity.normalized,false);
            var core=new GameObject("White Hot Arrow Trail");core.transform.SetParent(transform,false);
            coreTrail=core.AddComponent<TrailRenderer>();
            coreTrail.sharedMaterial=flightTrail.sharedMaterial;coreTrail.time=.16f;
            coreTrail.minVertexDistance=.08f;coreTrail.startWidth=.085f;coreTrail.endWidth=0;
            coreTrail.startColor=new Color(3,1.5f,1,.95f);coreTrail.endColor=new Color(1,.12f,.02f,0);
            coreTrail.numCapVertices=3;coreTrail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            coreTrail.receiveShadows=false;coreTrail.AddPosition(transform.position);
        }
    }
    public static void CreateVisual(Transform parent, Material shaft, Material tip)
    {
        DungeonArcher.Segment("Shaft",parent,new Vector3(0,0,-.3f),new Vector3(0,0,.3f),.014f,shaft);
        DungeonArcher.Segment("Arrowhead",parent,new Vector3(0,0,.3f),new Vector3(0,0,.4f),.035f,tip);
        DungeonArcher.Segment("Fletching",parent,new Vector3(-.045f,0,-.23f),new Vector3(.045f,0,-.23f),.025f,tip);
    }
    void Update()
    {
        if(!DungeonStartScreen.BattleStarted||Cursor.lockState!=CursorLockMode.Locked)return;
        if(DungeonStartScreen.BattleEnded){Destroy(gameObject);return;}
        Simulate(Time.deltaTime);
    }
    public void Simulate(float dt)
    {
        if(HasHit||dt<=0)return;
        // Bound steering and collision steps, including on slow frames.
        while(dt>0&&!HasHit){float slice=Mathf.Min(.02f,dt);dt-=slice;Advance(slice);}
    }
    void Advance(float dt)
    {
        age+=dt;
        if(age>(Kind==ShotKind.Homing?5f:15f)){HasHit=true;EndTrail();Destroy(gameObject);return;}
        if(Kind==ShotKind.Homing)
        {
            searchDelay-=dt;
            if(!Alive(Target)&&searchDelay<=0){Target=NearestEnemy(transform.position);searchDelay=.1f;}
            // Brief outward fan clears the surface and makes the three arrows distinguishable.
            if(age>.1f&&Alive(Target))
            {
                Vector3 aim=AimPoint(Target,transform.position)-transform.position;
                if(aim.sqrMagnitude>.0001f)
                {
                    // A fixed 32 m/s turn makes close shards overshoot the torso and
                    // loop down into the floor. Tighten the turn and brake on approach.
                    float distance=aim.magnitude;
                    float speed=Mathf.Clamp(distance*12f,8f,32f);
                    float turnRate=Mathf.Lerp(40f,16f,Mathf.Clamp01(distance/4f));
                    Vector3 heading=Vector3.RotateTowards(velocity.normalized,aim.normalized,turnRate*dt,0);
                    // Brake while facing away as well: the initial fan needs a U-turn,
                    // and accelerating before that turn completes creates a huge loop.
                    float alignment=Mathf.Clamp01(Vector3.Dot(heading,aim.normalized));
                    velocity=heading*Mathf.Lerp(6f,speed,alignment);
                }
            }
        }
        else if(Kind!=ShotKind.Power) velocity+=Vector3.down*9.81f*dt;
        Vector3 step=velocity*dt;
        var hits=Physics.SphereCastAll(transform.position,.045f,step.normalized,step.magnitude,~0,QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
        // Sweep casts omit initial overlaps, which can occur when a target moves into a shard.
        foreach(var c in Physics.OverlapSphere(transform.position,.045f,~0,QueryTriggerInteraction.Ignore))
            if(!Ignored(c)&&Contact(c,transform.position,-velocity.normalized))return;
        foreach(var hit in hits)
            if(!Ignored(hit.collider)&&Contact(hit.collider,hit.point,hit.normal))return;
        transform.position+=step;
        if(velocity.sqrMagnitude>.01f)transform.rotation=Quaternion.LookRotation(velocity);
        if(flightTrail!=null)flightTrail.AddPosition(transform.position);
        if(coreTrail!=null)coreTrail.AddPosition(transform.position);
    }
    bool Ignored(Collider c)
    {
        if(c==owner||c.transform.IsChildOf(transform))return true;
        var enemy=Enemy(c);
        // Guided shards seek the boss's actual weak-point collider, rather than being
        // consumed by its surrounding armour while turning toward the exposed cap.
        if(Kind==ShotKind.Homing&&enemy==Target&&enemy is DungeonBoss&&
            (c.GetComponent<DungeonBossPart>()==null||c.GetComponent<DungeonBossPart>().Broken))return true;
        return enemy!=null&&!Alive(enemy);
    }
    bool Contact(Collider collider,Vector3 point,Vector3 normal)
    {
        MonoBehaviour enemy=Enemy(collider);
        float healthBefore=EnemyHealth(enemy);
        var part=collider.GetComponent<DungeonBossPart>();
        if(part!=null){if(!part.Broken)part.TakeDamage(Damage);}
        else if(collider.GetComponentInParent<DungeonBoss>()==null)
            foreach(var behaviour in collider.GetComponentsInParent<MonoBehaviour>())
                if(behaviour is IDungeonDamageable damageable){damageable.TakeDamage(Damage);break;}
        // Confirm actual health loss, not contact with scenery, armour or a dead target.
        if(enemy!=null && EnemyHealth(enemy)<healthBefore && owner!=null)
        {
            var archer=owner.GetComponentInParent<DungeonArcher>();
            if(archer!=null)archer.ConfirmArrowHit();
        }
        HasHit=true;transform.position=point-transform.forward*(Kind==ShotKind.Homing?.1f:.3f);EndTrail();
        if(Kind==ShotKind.Power)
        {
            DungeonPowerShotVfx.Burst(point,normal,true);
            var glow=transform.Find("Power Bolt");if(glow!=null)glow.gameObject.SetActive(false);
        }
        if(Kind==ShotKind.Split){SpawnShards(point,normal,Alive(enemy)?enemy:NearestEnemy(point),enemy!=null);Destroy(gameObject);}
        else{transform.SetParent(collider.transform,true);Destroy(gameObject,Kind==ShotKind.Homing?.4f:8f);}
        return true;
    }
    void SpawnShards(Vector3 point,Vector3 normal,MonoBehaviour preferred,bool hitEnemy)
    {
        normal=normal.sqrMagnitude>.01f?normal.normalized:-velocity.normalized;
        if(hitEnemy)
        {
            // Capsule feet and undersides have downward-facing normals. Never use those
            // to launch shards into the floor: fan out above the hit before homing back.
            Vector3 outward=Vector3.ProjectOnPlane(normal,Vector3.up);
            if(outward.sqrMagnitude<.01f)outward=Vector3.ProjectOnPlane(-velocity,Vector3.up);
            if(outward.sqrMagnitude<.01f)outward=Vector3.forward;
            normal=(outward.normalized+Vector3.up*.45f).normalized;
        }
        Vector3 side=Vector3.Cross(normal,Vector3.up);if(side.sqrMagnitude<.01f)side=Vector3.right;side.Normalize();
        for(int i=0;i<3;i++)
        {
            Vector3 direction=(normal+side*(i-1)*.65f+Vector3.up*(hitEnemy?.65f:.35f)).normalized;
            var obj=new GameObject("Homing Arrow");
            obj.transform.SetPositionAndRotation(point+normal*.16f,Quaternion.LookRotation(direction));
            obj.AddComponent<DungeonArrow>().Initialize(direction*16f,Damage*HomingDamageRatio,owner,shaftMaterial,tipMaterial,ShotKind.Homing,preferred);
        }
    }
    void EndTrail()
    {
        if(coreTrail!=null)
        {
            coreTrail.AddPosition(transform.position);coreTrail.emitting=false;coreTrail.transform.SetParent(null,true);
            Destroy(coreTrail.gameObject,coreTrail.time+.05f);coreTrail=null;
        }
        if(flightTrail==null)return;
        flightTrail.AddPosition(transform.position);flightTrail.emitting=false;flightTrail.transform.SetParent(null,true);
        Destroy(flightTrail.gameObject,flightTrail.time+.05f);flightTrail=null;
    }
    static MonoBehaviour Enemy(Collider c)
    {
        var skeleton=c.GetComponentInParent<DungeonSkeleton>();if(skeleton!=null)return skeleton;
        return c.GetComponentInParent<DungeonBoss>();
    }
    static float EnemyHealth(MonoBehaviour enemy)
    {
        if(enemy is DungeonSkeleton skeleton)return skeleton.Health;
        if(enemy is DungeonBoss boss)return boss.Health;
        return 0;
    }
    static bool Alive(MonoBehaviour enemy)
    {
        if(enemy==null||!enemy.gameObject.activeInHierarchy)return false;
        if(enemy is DungeonSkeleton skeleton)return skeleton.Health>0&&skeleton.State!=DungeonSkeleton.Behaviour.Dead;
        if(enemy is DungeonBoss boss)return !boss.Defeated;
        return false;
    }
    static Vector3 AimPoint(MonoBehaviour enemy,Vector3 from)
    {
        if(enemy is DungeonBoss boss)
        {
            Vector3 closest=enemy.transform.position+Vector3.up*8;float distance=float.PositiveInfinity;
            foreach(var part in boss.Parts)
            {
                if(part.Broken)continue;
                Vector3 p=part.transform.position;float d=(p-from).sqrMagnitude;
                if(d<distance){closest=p;distance=d;}
            }
            return closest;
        }
        return enemy.transform.position+Vector3.up*1.2f;
    }
    static MonoBehaviour NearestEnemy(Vector3 from)
    {
        MonoBehaviour best=null;float distance=float.PositiveInfinity;
        foreach(var enemy in FindObjectsByType<DungeonSkeleton>(FindObjectsSortMode.None))
        {
            if(!Alive(enemy))continue;float d=(AimPoint(enemy,from)-from).sqrMagnitude;
            if(d<distance){best=enemy;distance=d;}
        }
        foreach(var enemy in FindObjectsByType<DungeonBoss>(FindObjectsSortMode.None))
        {
            if(!Alive(enemy))continue;float d=(AimPoint(enemy,from)-from).sqrMagnitude;
            if(d<distance){best=enemy;distance=d;}
        }
        return best;
    }
}
