using System.Collections.Generic;
using UnityEngine;

public class DungeonHealingPickups : MonoBehaviour
{
    const float SpawnCooldown = 30f;
    public float lifetime = 25f, pickupRadius = 1.5f;
    public int Collected { get; private set; }
    public int ActiveCount => pickups.Count;
    class Pickup
    {
        public Transform visual;
        public Vector3 position;
        public float age;
    }
    readonly List<Pickup> pickups = new List<Pickup>();
    DungeonArcher player;
    DungeonBoss boss;
    Material green;
    float spawnTimer;

    void Awake()
    {
        player = FindFirstObjectByType<DungeonArcher>();
        boss = FindFirstObjectByType<DungeonBoss>(FindObjectsInactive.Include);
        green = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        green.color = new Color(.05f,1f,.2f);
        ResetTimer();
    }
    void ResetTimer() { spawnTimer = SpawnCooldown; }
    void Update()
    {
        if (player == null || player.IsDead || (boss != null && boss.Defeated))
        {
            Clear();
            return;
        }
        if (Cursor.lockState == CursorLockMode.Locked) Simulate(Time.deltaTime);
    }
    public void Simulate(float dt)
    {
        if(!DungeonStartScreen.BattleStarted || !DungeonExploration.ArenaReady)return;
        if (dt <= 0 || player == null || player.IsDead || (boss != null && boss.Defeated)) return;
        bool hadPickup = pickups.Count > 0;
        for (int i=pickups.Count-1;i>=0;i--)
        {
            var item=pickups[i];item.age+=dt;
            item.visual.position=item.position+Vector3.up*(Mathf.Sin(item.age*2.5f)*.15f);
            item.visual.Rotate(Vector3.up,65f*dt,Space.World);
            Vector3 delta=player.transform.position-item.position;delta.y=0;
            bool collected=delta.sqrMagnitude<=pickupRadius*pickupRadius
                && Mathf.Abs(player.transform.position.y)<2f && player.Heal(1);
            if(collected || item.age>=lifetime)
            {
                if(collected)Collected++;
                Destroy(item.visual.gameObject);pickups.RemoveAt(i);
                ResetTimer();
            }
        }
        // The next cooldown starts on removal, never while an item is still present.
        if(hadPickup)return;
        spawnTimer=Mathf.Max(0,spawnTimer-dt);
        if(spawnTimer<=0)TrySpawn();
    }
    public bool TrySpawn()
    {
        if(player==null || player.IsDead || (boss!=null && boss.Defeated) || pickups.Count>=1)return false;
        for(int attempt=0;attempt<40;attempt++)
        {
            Vector3 point=new Vector3(Random.Range(-30f,30f),1.2f,Random.Range(-48f,48f));
            Vector3 toPlayer=point-player.transform.position;toPlayer.y=0;
            if(toPlayer.sqrMagnitude<16f)continue;
            if(boss!=null)
            {
                Vector3 toBoss=point-boss.transform.position;toBoss.y=0;
                if(toBoss.sqrMagnitude<100f)continue;
            }
            if(Physics.CheckSphere(point,.9f,~0,QueryTriggerInteraction.Ignore))continue;
            bool crowded=false;
            foreach(var item in pickups)if((item.position-point).sqrMagnitude<16f)crowded=true;
            if(crowded)continue;
            var cross=new GameObject("Green Healing Cross").transform;
            cross.SetParent(transform,false);cross.position=point;
            MakeBar(cross,new Vector3(.35f,1.4f,.35f));
            MakeBar(cross,new Vector3(1.2f,.35f,.35f));
            pickups.Add(new Pickup {visual=cross,position=point});
            return true;
        }
        return false;
    }
    void MakeBar(Transform parent,Vector3 scale)
    {
        var bar=new GameObject("Cross Bar");bar.transform.SetParent(parent,false);
        bar.transform.localScale=scale;
        bar.AddComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        var renderer=bar.AddComponent<MeshRenderer>();renderer.sharedMaterial=green;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    void Clear()
    {
        foreach(var item in pickups)if(item.visual!=null)Destroy(item.visual.gameObject);
        pickups.Clear();
    }
    void OnDestroy() { Clear();if(green!=null)Destroy(green); }
}
