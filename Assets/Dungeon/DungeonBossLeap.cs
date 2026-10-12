using UnityEngine;

public class DungeonBossLeap : MonoBehaviour
{
    public float triggerDistance=28f, radius=8f, damage=35f;
    public float preparation=.6f, flightTime=1.5f, jumpHeight=7f, cooldownSeconds=9.6f;
    public bool Active { get; private set; }
    public Vector3 LandingPoint { get; private set; }
    public int Landings { get; private set; }
    DungeonBoss boss;
    DungeonArcher player;
    LineRenderer ring;
    MeshRenderer impact;
    Material material, warningMaterial;
    Vector3 launch;
    float elapsed, cooldown=1.6f;
    public float Elapsed => elapsed;
    bool landed;
    public void Initialize(DungeonBoss owner,DungeonArcher target)
    {
        boss=owner;player=target;
        material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.color=new Color(1,.08f,.015f);
        warningMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));warningMaterial.color=new Color(1,.55f,.04f);
        var r=new GameObject("Jump Landing Warning");r.transform.SetParent(transform);
        ring=r.AddComponent<LineRenderer>();ring.sharedMaterial=warningMaterial;ring.loop=true;ring.positionCount=64;
        ring.startWidth=.2f;ring.endWidth=.2f;ring.enabled=false;
        var g=new GameObject("Jump Impact Area");g.transform.SetParent(transform);
        g.AddComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        impact=g.AddComponent<MeshRenderer>();impact.sharedMaterial=material;impact.enabled=false;
        impact.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    public void AdvanceCooldown(float dt) { cooldown=Mathf.Max(0,cooldown-dt); }
    public bool Ready(float distance)
    {
        if(Active || cooldown>0 || distance<triggerDistance || boss.Defeated || player.IsDead)return false;
        foreach(var part in boss.Parts)if(part.PartName.EndsWith("Leg") && !part.Broken)return true;
        return false;
    }
    public bool TryBegin(float distance)
    {
        if(!Ready(distance) || boss.ActiveAttackCount>0)return false;
        launch=boss.transform.position;
        Vector3 target=player.transform.position;target.y=0;
        Vector3 facing=target-launch;facing.y=0;
        Quaternion rotation=Quaternion.LookRotation(-facing.normalized);
        // Leave room for the giant's whole body near walls and existing braziers.
        bool found=false;
        for(int i=0;i<=12;i++)
        {
            Vector3 point=Vector3.MoveTowards(target,launch,i);
            point.x=Mathf.Clamp(point.x,-28,28);point.z=Mathf.Clamp(point.z,-46,46);
            bool blocked=false;
            foreach(var c in Physics.OverlapBox(point+Vector3.up*7.3f,new Vector3(6.5f,7,3),rotation,~0,QueryTriggerInteraction.Ignore))
                if(!c.transform.IsChildOf(boss.transform) && c.GetComponentInParent<DungeonArcher>()!=player){blocked=true;break;}
            if(!blocked){LandingPoint=point;found=true;break;}
        }
        if(!found)return false;
        boss.transform.rotation=rotation;
        Active=true;landed=false;elapsed=0;cooldown=cooldownSeconds;
        for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;ring.SetPosition(i,LandingPoint+new Vector3(Mathf.Cos(a)*radius,.12f,Mathf.Sin(a)*radius));}
        ring.enabled=true;
        impact.enabled=false;
        impact.transform.position=LandingPoint+Vector3.up*.08f;
        impact.transform.rotation=Quaternion.identity;
        // The built-in cylinder mesh has radius 1 (diameter 2).
        impact.transform.localScale=new Vector3(radius,.02f,radius);
        return true;
    }
    public bool Simulate(float dt)
    {
        if(!Active)return true;
        if(boss.Defeated || player.IsDead){Cancel();return true;}
        elapsed+=dt;ring.enabled=!landed;
        float t=Mathf.Clamp01((elapsed-preparation)/flightTime);
        boss.transform.position=Vector3.Lerp(launch,LandingPoint,t)+Vector3.up*(4*t*(1-t)*jumpHeight);
        impact.transform.position=LandingPoint+Vector3.up*.08f;
        if(t>=1 && !landed)
        {
            landed=true;Landings++;impact.enabled=true;ring.enabled=false;
            Vector3 delta=player.transform.position-LandingPoint;delta.y=0;
            if(delta.magnitude<=radius+.35f)player.TakeDamage(damage);
        }
        if(elapsed>=preparation+flightTime+.35f){Cancel();return true;}
        return false;
    }
    public void Cancel()
    {
        if(Active && !landed)
        {
            // Avoid a defeated or interrupted boss remaining suspended in midair.
            Vector3 p=boss.transform.position;p.y=0;boss.transform.position=p;
        }
        Active=false;if(ring!=null)ring.enabled=false;if(impact!=null)impact.enabled=false;
    }
    void OnDestroy(){if(material!=null)Destroy(material);if(warningMaterial!=null)Destroy(warningMaterial);}
}
