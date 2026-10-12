using UnityEngine;

// Cosmetic debris has no collider, so it cannot block arrows or movement.
public class DungeonStoneFragment : MonoBehaviour
{
    Transform owner;
    Vector3 velocity, originalScale;
    float age;
    bool settled;
    public void Initialize(Transform boss,Vector3 initialVelocity)
    {
        owner=boss;velocity=initialVelocity;originalScale=transform.localScale;
    }
    void Update() { Simulate(Time.deltaTime); }
    public void Simulate(float dt)
    {
        age+=dt;
        if(age>=5){Destroy(gameObject);return;}
        if(!settled)
        {
            velocity+=Vector3.down*15*dt;
            Vector3 step=velocity*dt;
            float nearest=float.MaxValue;RaycastHit closest=new RaycastHit();
            foreach(var hit in Physics.SphereCastAll(transform.position,.15f,step.normalized,step.magnitude,~0,QueryTriggerInteraction.Ignore))
            {
                if(owner!=null && hit.transform.IsChildOf(owner))continue;
                if(hit.collider.GetComponentInParent<DungeonArcher>()!=null)continue;
                if(hit.distance<nearest){nearest=hit.distance;closest=hit;}
            }
            if(nearest<float.MaxValue){transform.position=closest.point+closest.normal*.15f;settled=true;}
            else{transform.position+=step;transform.Rotate(new Vector3(90,120,60)*dt);}
        }
        if(age>4)transform.localScale=originalScale*(5-age);
    }
}
