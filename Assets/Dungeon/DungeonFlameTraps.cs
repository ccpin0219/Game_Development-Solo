using System.Collections.Generic;
using UnityEngine;

public class DungeonFlameTraps : MonoBehaviour
{
    public float warningSeconds = 1.2f, gapSeconds = .32f;
    public float damage = 10f, projectileSpeed = 14f;
    public int ActiveIndex { get; private set; }
    public string Phase { get; private set; } = "Warning";
    public int TorchCount => torches.Count;
    public int CompletedBursts { get; private set; }
    public int LiveProjectiles => projectiles.Count;
    readonly List<Transform> torches = new List<Transform>();
    readonly List<DungeonFireball> projectiles = new List<DungeonFireball>();
    DungeonArcher player;
    DungeonBoss boss;
    Material orange, yellow;
    readonly List<Transform> warnings = new List<Transform>();
    readonly List<int> activeIndices = new List<int>();
    public IReadOnlyList<int> ActiveIndices => activeIndices;
    public int TotalShotsFired { get; private set; }
    float timer;

    void Awake()
    {
        player = FindFirstObjectByType<DungeonArcher>();
        boss = FindFirstObjectByType<DungeonBoss>();
        var room = GameObject.Find("Dungeon Boss Room");
        if (room != null)
            foreach (Transform t in room.transform.Find("05 Braziers and Lighting"))
                if (t.name == "Amber Ember") torches.Add(t);
        torches.Sort((a,b) => Mathf.Abs(a.position.z-b.position.z)>.1f ? a.position.z.CompareTo(b.position.z) : a.position.x.CompareTo(b.position.x));
        orange = new Material(Shader.Find("Universal Render Pipeline/Unlit")); orange.color = new Color(1,.18f,.01f);
        yellow = new Material(Shader.Find("Universal Render Pipeline/Unlit")); yellow.color = new Color(1,.85f,.15f);
        for(int i=0;i<Mathf.Min(3,torches.Count);i++)
        {
            var g=new GameObject("Torch Charge Glow");
            g.AddComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            g.AddComponent<MeshRenderer>().sharedMaterial=yellow;
            warnings.Add(g.transform);
        }
        SelectEmitters();UpdateWarnings();
    }
    void SelectEmitters()
    {
        activeIndices.Clear();
        var available=new List<int>();
        for(int i=0;i<torches.Count;i++)available.Add(i);
        for(int i=0;i<Mathf.Min(3,torches.Count);i++)
        {
            int pick=Random.Range(0,available.Count);
            activeIndices.Add(available[pick]);available.RemoveAt(pick);
        }
        ActiveIndex=activeIndices.Count>0?activeIndices[0]:-1;
    }
    void UpdateWarnings()
    {
        for(int i=0;i<warnings.Count;i++)
        {
            warnings[i].gameObject.SetActive(Phase=="Warning");
            warnings[i].position=torches[activeIndices[i]].position;
            warnings[i].localScale=Vector3.one*(.3f+.65f*Mathf.Clamp01(timer/warningSeconds));
        }
    }
    void Update()
    {
        if (player==null || player.IsDead || (boss!=null && boss.Defeated)) { foreach(var glow in warnings)glow.gameObject.SetActive(false); return; }
        if (Cursor.lockState == CursorLockMode.Locked) Simulate(Time.deltaTime);
    }
    public void Simulate(float dt)
    {
        if(!DungeonStartScreen.BattleStarted)return;
        if (torches.Count==0 || player==null || player.IsDead || (boss!=null && boss.Defeated)) return;
        // Existing shots keep flying independently when the next torch starts charging.
        for(int i=projectiles.Count-1;i>=0;i--)
        {
            if(projectiles[i]==null) { projectiles.RemoveAt(i); continue; }
            projectiles[i].Simulate(dt);
            if(projectiles[i].HasHit) { Destroy(projectiles[i].gameObject); projectiles.RemoveAt(i); }
        }
        timer+=dt;
        if (Phase=="Warning" && timer>=warningSeconds)
        {
            foreach(int index in activeIndices)
            {
            var shot=new GameObject("Torch Fireball").AddComponent<DungeonFireball>();
            Vector3 origin=torches[index].position;
            // Random inward spread, independent of the player's position.
            Vector3 inward=origin.x<0 ? Vector3.right : Vector3.left;
            Vector3 direction=Quaternion.AngleAxis(Random.Range(-65f,65f),Vector3.up)*inward;
            direction=(direction+Vector3.down*Random.Range(.03f,.12f)).normalized;
            shot.Initialize(origin,direction,projectileSpeed,damage,orange,yellow);
            projectiles.Add(shot);TotalShotsFired++;
            }
            CompletedBursts++; Phase="Gap"; timer=0;
        }
        else if(Phase=="Gap" && timer>=gapSeconds)
        {
            SelectEmitters(); Phase="Warning";timer=0;
        }
        UpdateWarnings();
    }
    void OnDestroy()
    {
        foreach(var shot in projectiles)if(shot!=null)Destroy(shot.gameObject);
        foreach(var glow in warnings)if(glow!=null)Destroy(glow.gameObject);
        if(orange!=null)Destroy(orange);
        if(yellow!=null)Destroy(yellow);
    }
}
