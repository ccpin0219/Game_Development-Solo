using UnityEngine;

public class DungeonFireball : MonoBehaviour
{
    public bool HasHit { get; private set; }
    public string HitObject { get; private set; }
    Vector3 velocity;
    Vector3 launchOrigin;
    float damage;
    const float Radius = .48f;

    public void Initialize(Vector3 origin, Vector3 direction, float speed, float hitDamage, Material orange, Material yellow)
    {
        transform.SetPositionAndRotation(origin,Quaternion.LookRotation(direction));
        launchOrigin=origin;
        velocity=direction.normalized*speed; damage=hitDamage;
        MakeSphere("Flame",new Vector3(0,0,-.2f),new Vector3(.85f,.85f,1.5f),orange);
        MakeSphere("Bright Core",new Vector3(0,0,.28f),Vector3.one*.65f,yellow);
        var trail=gameObject.AddComponent<TrailRenderer>();trail.sharedMaterial=orange;
        trail.time=.25f;trail.startWidth=.65f;trail.endWidth=0;trail.minVertexDistance=.1f;
        var light=gameObject.AddComponent<Light>();light.type=LightType.Point;
        light.color=new Color(1,.35f,.04f);light.intensity=3;light.range=4;
    }
    void MakeSphere(string label,Vector3 position,Vector3 scale,Material material)
    {
        var g=new GameObject(label);g.transform.SetParent(transform,false);
        g.transform.localPosition=position;g.transform.localScale=scale;
        g.AddComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        g.AddComponent<MeshRenderer>().sharedMaterial=material;
    }
    bool CanHit(Collider c)
    {
        if(c.transform.IsChildOf(transform) || c.GetComponentInParent<DungeonBoss>()!=null) return false;
        // The emitter's bowl surrounds the flame; ignore only that source brazier.
        if((c.name=="Brazier Bowl" || c.name=="Brazier Pedestal") && Vector3.Distance(c.transform.position,launchOrigin)<3.5f) return false;
        return true;
    }
    void Impact(Collider collider)
    {
        HasHit=true;HitObject=collider.name;
        var target=collider.GetComponentInParent<DungeonArcher>();
        if(target!=null)target.TakeDamage(damage);
        gameObject.SetActive(false);
    }
    public void Simulate(float dt)
    {
        if(HasHit || dt<=0)return;
        // Check both overlap and swept travel to cover point-blank and fast shots.
        foreach(var c in Physics.OverlapSphere(transform.position,Radius,~0,QueryTriggerInteraction.Ignore))
            if(CanHit(c)){Impact(c);return;}
        Vector3 step=velocity*dt;
        RaycastHit closest=new RaycastHit();float distance=float.MaxValue;
        foreach(var hit in Physics.SphereCastAll(transform.position,Radius,step.normalized,step.magnitude,~0,QueryTriggerInteraction.Ignore))
            if(CanHit(hit.collider) && hit.distance<distance){closest=hit;distance=hit.distance;}
        if(distance<float.MaxValue)
        {
            transform.position+=step.normalized*distance;Impact(closest.collider);
        }
        else transform.position+=step;
        // No timer or maximum range: a miss travels until it hits level geometry.
    }
}
