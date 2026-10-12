using UnityEngine;

public class DungeonBossPart : MonoBehaviour, IDungeonDamageable
{
    public string PartName { get; private set; }
    public float Health { get; private set; }
    public float MaxHealth { get; private set; }
    public bool Broken => Health <= 0;
    public Transform Body => transform.parent;
    DungeonBoss boss;
    Renderer orbRenderer;
    MaterialPropertyBlock hitProperties;
    float hitFlash;

    public void Initialize(DungeonBoss owner, string label, float durability)
    {
        boss=owner;PartName=label;MaxHealth=durability;Health=durability;
        orbRenderer=GetComponent<Renderer>();
        hitProperties=new MaterialPropertyBlock();
        hitFlash=0;
        orbRenderer.SetPropertyBlock(null);
        orbRenderer.sharedMaterial=boss.weakPointMaterial;
    }
    public void TakeDamage(float damage)
    {
        if(Broken || boss==null || boss.Defeated || damage<=0)return;
        float applied=Mathf.Min(Health,damage);
        Health-=applied;
        if(Broken)orbRenderer.sharedMaterial=boss.brokenWeakPointMaterial;
        hitFlash=.16f;
        hitProperties.SetColor("_BaseColor",new Color(1f,.92f,.7f));
        orbRenderer.SetPropertyBlock(hitProperties);
        boss.TakeDamage(applied);
        if(Broken)boss.OnPartBroken(this);
    }
    void Update()
    {
        if(hitFlash<=0)return;
        hitFlash=Mathf.Max(0,hitFlash-Time.deltaTime);
        // Clear only this orb's override, revealing red or its destroyed gray material.
        if(hitFlash<=0)orbRenderer.SetPropertyBlock(null);
    }
}
