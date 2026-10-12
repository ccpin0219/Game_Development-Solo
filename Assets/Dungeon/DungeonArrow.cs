using UnityEngine;

public interface IDungeonDamageable { void TakeDamage(float damage); }

public class DungeonArrow : MonoBehaviour
{
    public float Damage { get; private set; }
    public bool HasHit { get; private set; }
    Vector3 velocity;
    Collider owner;
    float age;
    public void Initialize(Vector3 initialVelocity, float damage, Collider shooter, Material shaft, Material tip)
    {
        velocity=initialVelocity; Damage=damage; owner=shooter;
        CreateVisual(transform,shaft,tip);
    }
    public static void CreateVisual(Transform parent, Material shaft, Material tip)
    {
        DungeonArcher.Segment("Shaft",parent,new Vector3(0,0,-.3f),new Vector3(0,0,.3f),.014f,shaft);
        DungeonArcher.Segment("Arrowhead",parent,new Vector3(0,0,.3f),new Vector3(0,0,.4f),.035f,tip);
        DungeonArcher.Segment("Fletching",parent,new Vector3(-.045f,0,-.23f),new Vector3(.045f,0,-.23f),.025f,tip);
    }
    void Update() { Simulate(Time.deltaTime); }
    public void Simulate(float deltaTime)
    {
        age+=deltaTime;
        if(age>15) {Destroy(gameObject);return;}
        if(HasHit)return;
        velocity+=Vector3.down*9.81f*deltaTime;
        Vector3 step=velocity*deltaTime;
        RaycastHit closest=new RaycastHit(); float distance=float.MaxValue;
        foreach(var hit in Physics.SphereCastAll(transform.position,.045f,step.normalized,step.magnitude,~0,QueryTriggerInteraction.Ignore))
            if(hit.collider!=owner && hit.distance<distance){closest=hit;distance=hit.distance;}
        if(distance<float.MaxValue)
        {
            transform.position=closest.point-transform.forward*.3f;HasHit=true;
            var part=closest.collider.GetComponent<DungeonBossPart>();
            if(part!=null) part.TakeDamage(Damage);
            else if(closest.collider.GetComponentInParent<DungeonBoss>()==null) foreach(var behaviour in closest.collider.GetComponentsInParent<MonoBehaviour>())
                if(behaviour is IDungeonDamageable damageable){damageable.TakeDamage(Damage);break;}
            transform.SetParent(closest.collider.transform,true);
            if(part!=null && part.Broken) Destroy(gameObject);
        }
        else {transform.position+=step;if(velocity.sqrMagnitude>.01f)transform.rotation=Quaternion.LookRotation(velocity);}
    }
}

