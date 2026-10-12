using System.Collections.Generic;
using UnityEngine;

// The boss advances this pattern so cursor pause and death also stop every projectile.
public class DungeonRockVolley : MonoBehaviour
{
    class Rock
    {
        public Transform visual;
        public LineRenderer marker;
        public Vector3 target, start;
        public float delay, size;
        public bool impacted;
    }
    readonly List<Rock> rocks = new List<Rock>();
    DungeonArcher player;
    Material warningMaterial;
    float elapsed;
    public int RockCount => rocks.Count;
    public int ImpactCount { get; private set; }
    public bool Finished => elapsed >= .6f + Mathf.Max(0, rocks.Count - 1) * .11f + FlightTime + .7f;
    public Vector3[] Targets => rocks.ConvertAll(r => r.target).ToArray();
    const float Radius = 1.5f, FlightTime = 1.1f;

    public void Initialize(DungeonBoss boss, DungeonArcher targetPlayer, int count)
    {
        player = targetPlayer;
        warningMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        warningMaterial.color = new Color(1,.42f,.06f);
        Material stone = boss.upperBody.GetComponentInChildren<Renderer>().sharedMaterial;
        Vector3 center = player.transform.position;
        for (int attempt=0; attempt<240 && rocks.Count<count; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle*9;
            Vector3 point = center + new Vector3(offset.x,0,offset.y);
            if (Mathf.Abs(point.x)>34 || Mathf.Abs(point.z)>52) continue;
            if (Vector2.Distance(new Vector2(point.x,point.z),new Vector2(boss.transform.position.x,boss.transform.position.z))<7) continue;
            RaycastHit ground = new RaycastHit(); float nearest = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(new Vector3(point.x,23,point.z),Vector3.down,25,~0,QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(player.transform) || hit.transform.IsChildOf(boss.transform)) continue;
                if (hit.distance<nearest) { nearest=hit.distance; ground=hit; }
            }
            if (nearest==float.MaxValue || ground.normal.y<.9f || ground.point.y>1.5f) continue;
            point=ground.point;
            bool overlaps=false;
            foreach (var existing in rocks) if (Vector3.Distance(existing.target,point)<3.3f) { overlaps=true;break; }
            if (overlaps) continue;
            var rock=new Rock { target=point, start=boss.transform.TransformPoint(new Vector3(-4.4f,9,-1)), delay=rocks.Count*.11f, size=Random.Range(.65f,1.05f) };
            var visual=new GameObject("Thrown Stone");rock.visual=visual.transform;rock.visual.SetParent(transform);
            rock.visual.position=rock.start;rock.visual.localScale=new Vector3(rock.size,rock.size*.8f,rock.size*1.15f);
            visual.AddComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            visual.AddComponent<MeshRenderer>().sharedMaterial=stone;
            var marker=new GameObject("Stone Landing Warning");marker.transform.SetParent(transform);
            rock.marker=marker.AddComponent<LineRenderer>();rock.marker.sharedMaterial=warningMaterial;
            rock.marker.loop=true;rock.marker.positionCount=40;rock.marker.startWidth=.1f;rock.marker.endWidth=.1f;
            DrawRing(rock,Radius);
            rocks.Add(rock);
        }
    }
    void DrawRing(Rock rock,float radius)
    {
        for(int i=0;i<40;i++)
        {
            float a=i*Mathf.PI*2/40;
            rock.marker.SetPosition(i,rock.target+new Vector3(Mathf.Cos(a)*radius,.07f,Mathf.Sin(a)*radius));
        }
    }
    public void Simulate(float dt)
    {
        elapsed+=dt;
        foreach(var rock in rocks)
        {
            // Every location is locked at cast time; movement can evade the entire volley.
            float t=Mathf.Clamp01((elapsed-.6f-rock.delay)/FlightTime);
            if (!rock.impacted)
            {
                rock.visual.position=Vector3.Lerp(rock.start,rock.target+Vector3.up*rock.size*.4f,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*12);
                rock.visual.Rotate(new Vector3(100,160,70)*dt);
                if(t>=1)
                {
                    rock.impacted=true;ImpactCount++;
                    Vector3 difference=player.transform.position-rock.target;
                    if(!player.IsDead && new Vector2(difference.x,difference.z).magnitude<=Radius+.35f && Mathf.Abs(difference.y)<3)
                        player.TakeDamage(12);
                }
            }
            else
            {
                float after=elapsed-.6f-rock.delay-FlightTime;
                DrawRing(rock,Radius+after*3);
                rock.marker.enabled=after<.25f;
                rock.visual.localScale=Vector3.Lerp(rock.visual.localScale,Vector3.zero,dt*4);
            }
        }
    }
    void OnDestroy() { if(warningMaterial!=null) Destroy(warningMaterial); }
}
