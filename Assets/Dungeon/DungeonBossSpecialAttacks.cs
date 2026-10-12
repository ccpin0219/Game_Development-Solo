using UnityEngine;

public class DungeonBossSpecialAttacks : MonoBehaviour
{
    public string ActivePattern { get; private set; } = "";
    public float lineLength=32f, lineWidth=9f, circleRadius=12f, circleTriggerRange=9f;
    public float laserDuration=3f, laserTrackingSpeed=5f;
    DungeonBoss boss;
    DungeonArcher player;
    LineRenderer marker, beam;
    Material warningMaterial, laserMaterial, impactMaterial;
    MeshRenderer lineImpact, circleImpact;
    Vector3 origin, direction, laserDirection, laserAimPoint;
    Transform movingLeg;
    float elapsed, damageTimer;
    public float Elapsed => elapsed;
    float lineCooldown=2.4f, circleCooldown, laserCooldown=5.6f;
    bool resolved;
    public void Initialize(DungeonBoss owner,DungeonArcher target)
    {
        boss=owner;player=target;
        warningMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));warningMaterial.color=new Color(1,.35f,.02f);
        laserMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));laserMaterial.color=new Color(1,.04f,.12f);
        marker=MakeLine("Leg Attack Warning",warningMaterial,.16f);
        beam=MakeLine("Head Tracking Laser",laserMaterial,.45f);
        impactMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        impactMaterial.color=new Color(1f,.02f,.02f);
        var impact=new GameObject("Line Attack Red Area");impact.transform.SetParent(transform,false);
        impact.AddComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        lineImpact=impact.AddComponent<MeshRenderer>();lineImpact.sharedMaterial=impactMaterial;
        lineImpact.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        lineImpact.enabled=false;
        var circle=new GameObject("Circle Attack Red Area");circle.transform.SetParent(transform,false);
        circle.AddComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        circleImpact=circle.AddComponent<MeshRenderer>();circleImpact.sharedMaterial=impactMaterial;
        circleImpact.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        circleImpact.enabled=false;
    }
    LineRenderer MakeLine(string label,Material material,float width)
    {
        var g=new GameObject(label);g.transform.SetParent(transform,false);
        var line=g.AddComponent<LineRenderer>();line.useWorldSpace=true;line.sharedMaterial=material;
        line.startWidth=width;line.endWidth=width;line.enabled=false;return line;
    }
    DungeonBossPart Part(string name)
    {
        foreach(var p in boss.Parts)if(p.PartName==name)return p;
        return null;
    }
    bool Available(string name) {var p=Part(name);return p!=null && !p.Broken;}
    public bool MeleeIntact => Available("Right Leg");
    public void AdvanceCooldowns(float dt)
    {
        lineCooldown-=dt;circleCooldown-=dt;laserCooldown-=dt;
    }
    public bool TryBeginPattern(string pattern,float distance)
    {
        if(pattern=="Circle" && distance<=circleTriggerRange && circleCooldown<=0)return Begin(pattern);
        if(pattern=="Line" && distance<=lineLength && lineCooldown<=0)return Begin(pattern);
        if(pattern=="Laser" && laserCooldown<=0)return Begin(pattern);
        return false;
    }
    public bool Begin(string pattern)
    {
        if(boss.Defeated || player.IsDead || ActivePattern!="")return false;
        string part=pattern=="Line"?"Left Leg":pattern=="Circle"?"Right Leg":"Head";
        if(!Available(part))return false;
        if(pattern=="Circle" && Vector3.Distance(player.transform.position,boss.transform.position)>circleTriggerRange)return false;
        ActivePattern=pattern;elapsed=0;damageTimer=0;resolved=false;
        origin=boss.transform.position;origin.y=.1f;
        direction=player.transform.position-boss.transform.position;direction.y=0;
        direction=direction.sqrMagnitude>.01f?direction.normalized:-boss.transform.forward;
        if(pattern=="Line")
        {
            lineCooldown=6.4f;movingLeg=Part("Left Leg").Body;
            // Lock the direction when the warning begins, allowing a sideways dodge.
            Vector3 side=Vector3.Cross(Vector3.up,direction)*lineWidth*.5f;
            marker.loop=true;marker.positionCount=4;
            marker.SetPositions(new[]{origin-side,origin+direction*lineLength-side,origin+direction*lineLength+side,origin+side});
            marker.enabled=true;
            lineImpact.transform.SetPositionAndRotation(origin+direction*(lineLength*.5f),Quaternion.LookRotation(direction));
            lineImpact.transform.localScale=new Vector3(lineWidth,.025f,lineLength);
        }
        else if(pattern=="Circle")
        {
            circleCooldown=8f;movingLeg=Part("Right Leg").Body;
            circleImpact.enabled=false;
            circleImpact.transform.SetPositionAndRotation(origin,Quaternion.identity);
            circleImpact.transform.localScale=new Vector3(circleRadius,.02f,circleRadius);
            marker.loop=true;marker.positionCount=64;
            for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;marker.SetPosition(i,origin+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*circleRadius);}
            marker.enabled=true;
        }
        else
        {
            laserCooldown=12f;
            laserAimPoint=player.transform.position+Vector3.up*.9f;
            laserDirection=(laserAimPoint-HeadOrigin()).normalized;
            beam.positionCount=2;beam.loop=false;beam.enabled=true;
        }
        return true;
    }
    Vector3 HeadOrigin() => Part("Head").transform.position-boss.transform.forward*.7f;
    public bool Simulate(float dt)
    {
        if(ActivePattern=="")return true;
        if(boss.Defeated || player.IsDead){Cancel();return true;}
        lineImpact.transform.SetPositionAndRotation(origin+direction*(lineLength*.5f),Quaternion.LookRotation(direction));
        circleImpact.transform.SetPositionAndRotation(origin,Quaternion.identity);
        elapsed+=dt;
        if(ActivePattern=="Laser")
        {
            Vector3 start=HeadOrigin();
            Vector3 target=player.transform.position+Vector3.up*.9f;
            // Limit the aim point in world units so distance cannot make tracking faster than movement.
            laserAimPoint=Vector3.MoveTowards(laserAimPoint,target,laserTrackingSpeed*dt);
            laserDirection=(laserAimPoint-start).normalized;
            float distance=110f;Collider hitCollider=null;
            bool firing=elapsed>=1.1f;
            foreach(var hit in Physics.SphereCastAll(start,firing?.23f:.02f,laserDirection,distance,~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(boss.transform) && hit.distance<distance){distance=hit.distance;hitCollider=hit.collider;}
            beam.enabled=true;beam.startWidth=beam.endWidth=firing?.45f:.045f;
            beam.SetPosition(0,start);beam.SetPosition(1,start+laserDirection*distance);
            damageTimer-=dt;
            if(firing && damageTimer<=0 && hitCollider!=null && hitCollider.GetComponentInParent<DungeonArcher>()==player)
            {player.TakeDamage(8);damageTimer=.5f;}
            if(elapsed>=1.1f+laserDuration){Cancel();return true;}
        }
        else
        {
            float windup=ActivePattern=="Line"?1.2f:1.5f;
            marker.enabled=true;
            if(elapsed>=windup && !resolved)
            {
                resolved=true;
                if(ActivePattern=="Line")lineImpact.enabled=true;
                else if(ActivePattern=="Circle")circleImpact.enabled=true;
                Vector3 delta=player.transform.position-origin;delta.y=0;
                float along=Vector3.Dot(delta,direction);
                float across=Vector3.Cross(direction,delta).magnitude;
                bool inArea=ActivePattern=="Line" ? along>=0 && along<=lineLength+.35f && across<=lineWidth*.5f+.35f : delta.magnitude<=circleRadius+.35f;
                if(inArea && ClearPathToPlayer())player.TakeDamage(ActivePattern=="Line"?22:30);
                marker.startWidth=marker.endWidth=.6f;
            }
            if(elapsed>=windup+.3f){Cancel();return true;}
        }
        return false;
    }
    bool ClearPathToPlayer()
    {
        Vector3 start=origin+Vector3.up;
        Vector3 delta=player.transform.position+Vector3.up-start;
        foreach(var hit in Physics.RaycastAll(start,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
            if(!hit.transform.IsChildOf(boss.transform) && hit.collider.GetComponentInParent<DungeonArcher>()!=player)return false;
        return true;
    }
    public void OnPartBroken(string name)
    {
        if((name=="Head" && ActivePattern=="Laser") || (name=="Left Leg" && ActivePattern=="Line") || (name=="Right Leg" && ActivePattern=="Circle"))Cancel();
    }
    public void Cancel()
    {
        if(movingLeg!=null)movingLeg.localRotation=Quaternion.identity;
        movingLeg=null;ActivePattern="";
        if(marker!=null){marker.enabled=false;marker.startWidth=marker.endWidth=.16f;}
        if(beam!=null)beam.enabled=false;
        if(lineImpact!=null)lineImpact.enabled=false;
        if(circleImpact!=null)circleImpact.enabled=false;
    }
    void OnDestroy(){if(warningMaterial!=null)Destroy(warningMaterial);if(laserMaterial!=null)Destroy(laserMaterial);if(impactMaterial!=null)Destroy(impactMaterial);}
}
