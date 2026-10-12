using UnityEngine;

public class DungeonBossFloorAttack : MonoBehaviour
{
    public float warningSeconds=2f, activeSeconds=.8f, cooldownSeconds=9.6f;
    public bool Active { get; private set; }
    public bool Dangerous { get; private set; }
    public int SelectedColor { get; private set; } = -1;
    DungeonBoss boss;
    DungeonArcher player;
    DungeonBossPart torso;
    MeshRenderer floor;
    Material[] original;
    Material warningMaterial, damageMaterial;
    float elapsed, cooldown=4.8f;
    bool hitPlayer;
    readonly int[,] tileColors=new int[12,18];

    public void Initialize(DungeonBoss owner,DungeonArcher target)
    {
        boss=owner;player=target;
        foreach(var part in boss.Parts)if(part.PartName=="Torso")torso=part;
        var tiles=GameObject.Find("Four Color Floor Tiles");
        if(tiles==null)return;
        floor=tiles.GetComponent<MeshRenderer>();original=floor.sharedMaterials;
        // Read the actual mesh/material layout so damage follows the randomized visible tiles.
        for(int x=0;x<12;x++)for(int z=0;z<18;z++)tileColors[x,z]=-1;
        var mesh=tiles.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;
        for(int color=0;color<mesh.subMeshCount;color++)
        {
            var triangles=mesh.GetTriangles(color);
            for(int i=0;i<triangles.Length;i+=3)
            {
                Vector3 center=(vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3;
                int x=Mathf.FloorToInt((center.x+36)/6), z=Mathf.FloorToInt((center.z+54)/6);
                if(x>=0 && x<12 && z>=0 && z<18)tileColors[x,z]=color;
            }
        }
        warningMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        damageMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));damageMaterial.color=new Color(1,.025f,.025f);
    }
    public void AdvanceCooldown(float dt) { cooldown=Mathf.Max(0,cooldown-dt); }
    public bool TryBegin()
    {
        if(Active || cooldown>0 || floor==null || original.Length!=4 || torso==null || torso.Broken || boss.Defeated || player.IsDead)return false;
        SelectedColor=Random.Range(0,4);Active=true;Dangerous=false;hitPlayer=false;elapsed=0;cooldown=cooldownSeconds;
        warningMaterial.color=new Color(1,.6f,.08f);Apply(warningMaterial);return true;
    }
    void Apply(Material material)
    {
        var mats=(Material[])original.Clone();mats[SelectedColor]=material;floor.sharedMaterials=mats;
    }
    public int ColorAt(Vector3 worldPosition)
    {
        Vector3 p=floor.transform.InverseTransformPoint(worldPosition);
        int x=Mathf.FloorToInt((p.x+36)/6), z=Mathf.FloorToInt((p.z+54)/6);
        if(x<0 || x>=12 || z<0 || z>=18)return -1;
        return tileColors[x,z];
    }
    public bool Simulate(float dt)
    {
        if(!Active)return true;
        if(torso.Broken || boss.Defeated || player.IsDead){Cancel();return true;}
        elapsed+=dt;
        if(elapsed<warningSeconds)
            warningMaterial.color=Color.Lerp(new Color(.65f,.3f,.025f),new Color(1,.8f,.15f),.5f+.5f*Mathf.Sin(elapsed*12));
        else
        {
            if(!Dangerous){Dangerous=true;Apply(damageMaterial);}
            if(!hitPlayer && ColorAt(player.transform.position)==SelectedColor && player.transform.position.y<.6f)
            {hitPlayer=true;player.TakeDamage(1);}
        }
        if(elapsed>=warningSeconds+activeSeconds){Cancel();return true;}
        return false;
    }
    public void Cancel()
    {
        if(floor!=null && original!=null)floor.sharedMaterials=original;
        Active=false;Dangerous=false;
    }
    void OnDestroy()
    {
        Cancel();if(warningMaterial!=null)Destroy(warningMaterial);if(damageMaterial!=null)Destroy(damageMaterial);
    }
}
