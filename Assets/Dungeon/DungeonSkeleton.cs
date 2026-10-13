using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class DungeonSkeleton : MonoBehaviour, IDungeonDamageable
{
    public enum Behaviour { Idle, Chase, Windup, Swing, Recovery, Dead }
    public const float AxeHealth=100f, BrawlerHealth=100f, ArcherHealth=100f;
    public float moveSpeed = 3f, maxHealth = AxeHealth;
    public bool ranged;
    public bool brawler;
    DungeonSkeletonBrawler fists;
    DungeonSkeletonArcher archer;
    public float sightRange = 15f, leashRange = 24f;
    public float attackDistance = 1.8f, hitRange = 2.35f;
    public float windupSeconds = .8f, swingSeconds = .24f, recoverySeconds = 1.1f;
    public Transform model, axeArm, leftLeg, rightLeg, freeArm;
    public Renderer[] boneRenderers;
    public float Health { get; private set; }
    public Behaviour State { get; private set; }
    public int LandedHits { get; private set; }
    CharacterController controller;
    DungeonArcher player;
    Vector3 home;
    float timer, gait, hitFlash;
    MaterialPropertyBlock properties;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        player = FindFirstObjectByType<DungeonArcher>();
        Health = maxHealth; home = transform.position;
        properties = new MaterialPropertyBlock();
        if(brawler)
        {
            attackDistance=1.5f;hitRange=2f;windupSeconds=.45f;swingSeconds=.18f;recoverySeconds=.85f;
            sightRange=30;leashRange=40;
            fists=gameObject.AddComponent<DungeonSkeletonBrawler>();fists.Initialize(this);
        }
        else if(ranged){archer=gameObject.AddComponent<DungeonSkeletonArcher>();archer.Initialize(this,player);}
        // The movement capsule is too narrow around the visible skull and limbs.
        // Separate query-only boxes follow the animated bones without blocking movement.
        foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>())
        {
            var material=renderer.sharedMaterial;
            if(material==null || !material.name.Contains("Bone"))continue;
            if(renderer.name.Contains("Bow") || renderer.name=="Shaft" || renderer.name=="Arrowhead" || renderer.name=="Fletching")continue;
            var filter=renderer.GetComponent<MeshFilter>();
            if(filter==null || filter.sharedMesh==null)continue;
            var box=renderer.gameObject.AddComponent<BoxCollider>();box.isTrigger=true;
            box.center=filter.sharedMesh.bounds.center;box.size=filter.sharedMesh.bounds.size;
        }
    }

    void Update()
    {
        if (!DungeonStartScreen.BattleStarted || DungeonStartScreen.BattleEnded ||
            player == null || player.IsDead || Cursor.lockState != CursorLockMode.Locked) return;
        Simulate(Time.deltaTime);
    }

    public void Simulate(float dt)
    {
        if (dt <= 0 || player == null || player.IsDead || !DungeonStartScreen.BattleStarted || DungeonStartScreen.BattleEnded) return;
        if (State == Behaviour.Dead)
        {
            timer += dt;
            model.localRotation = Quaternion.Euler(0, 0, Mathf.SmoothStep(0, 88, timer / .65f));
            model.localPosition = Vector3.down * Mathf.Min(.18f, timer * .2f);
            if (timer > 5) Destroy(gameObject);
            return;
        }
        if (hitFlash > 0)
        {
            hitFlash -= dt;
            if (hitFlash <= 0) foreach (var r in boneRenderers) if (r != null) r.SetPropertyBlock(null);
        }
        if(archer!=null){archer.Tick(dt);return;}
        Vector3 delta = player.transform.position - transform.position;
        Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);
        float distance = flat.magnitude;
        bool visible = ClearToPlayer();
        timer += dt;
        if (State == Behaviour.Windup)
        {
            if(fists!=null)fists.Pose(-Mathf.Clamp01(timer/windupSeconds));
            else axeArm.localRotation = Quaternion.Euler(Mathf.Lerp(0, -155, Mathf.Clamp01(timer / windupSeconds)), 0, -8);
            if (timer >= windupSeconds) { State = Behaviour.Swing; timer = 0; }
        }
        else if (State == Behaviour.Swing)
        {
            if(fists!=null)fists.Pose(Mathf.Lerp(-1,1,Mathf.Clamp01(timer/swingSeconds)));
            else axeArm.localRotation = Quaternion.Euler(Mathf.Lerp(-155, -50, Mathf.Clamp01(timer / swingSeconds)), 0, -8);
            if (timer >= swingSeconds)
            {
                // One damage event at the downswing, using the player's CURRENT location.
                if (distance <= hitRange && Mathf.Abs(delta.y) < 1.6f &&
                    Vector3.Dot(transform.forward, flat.normalized) >= .65f && visible)
                { player.TakeDamage(1); LandedHits++; }
                State = Behaviour.Recovery; timer = 0;
            }
        }
        else if (State == Behaviour.Recovery)
        {
            if(fists!=null)fists.Pose(1-Mathf.Clamp01(timer/recoverySeconds));
            else axeArm.localRotation = Quaternion.Euler(Mathf.Lerp(-50, 0, Mathf.Clamp01(timer / recoverySeconds)), 0, 0);
            if (timer >= recoverySeconds) { State = Behaviour.Chase; timer = 0; }
        }
        else
        {
            bool inLeash = Vector3.Distance(home, player.transform.position) <= leashRange;
            if (State == Behaviour.Idle && distance <= sightRange && visible && inLeash) State = Behaviour.Chase;
            if (!inLeash) State = Behaviour.Idle;
            Vector3 destination = State == Behaviour.Chase ? player.transform.position : home;
            Vector3 direction = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up);
            if (State == Behaviour.Chase && distance <= attackDistance && visible)
            {
                if (flat.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(flat);
                State = Behaviour.Windup; timer = 0;
                leftLeg.localRotation = rightLeg.localRotation = Quaternion.identity;
                axeArm.localRotation = Quaternion.identity;
                if(fists!=null)fists.Pose(0);
            }
            else if (direction.magnitude > .15f)
            {
                Move(direction.normalized, dt);
                gait += dt * (brawler?12:7);
                float step = Mathf.Sin(gait) * 24;
                leftLeg.localRotation = Quaternion.Euler(step, 0, 0);
                rightLeg.localRotation = Quaternion.Euler(-step, 0, 0);
                freeArm.localRotation = Quaternion.Euler(-step * .6f, 0, 8);
                axeArm.localRotation = Quaternion.Euler(step * .25f, 0, -8);
                if(fists!=null)fists.Pose(Mathf.Sin(gait)*.12f);
            }
            else leftLeg.localRotation = rightLeg.localRotation = Quaternion.identity;
        }
        controller.Move(Vector3.down * 10 * dt);
    }

    void Move(Vector3 direction, float dt)
    {
        // Short probes steer around furniture; the controller supplies solid wall collision.
        float speed = brawler ? player.moveSpeed : Mathf.Min(moveSpeed, player.moveSpeed * .65f);
        foreach (float angle in new[] { 0f, 35f, -35f, 70f, -70f })
        {
            Vector3 travel = Quaternion.Euler(0, angle, 0) * direction;
            bool blocked = false;
            foreach (var hit in Physics.CapsuleCastAll(transform.position + Vector3.up * .5f,
                         transform.position + Vector3.up * 1.6f, .3f, travel, speed * dt + .35f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform)) { blocked = true; break; }
            if (blocked) continue;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(travel), 200 * dt);
            controller.Move(travel * speed * dt);
            break;
        }
    }

    bool ClearToPlayer()
    {
        Vector3 origin = transform.position + Vector3.up * 1.3f;
        Vector3 delta = player.transform.position + Vector3.up - origin;
        foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(transform) && hit.collider.GetComponentInParent<DungeonArcher>() != player) return false;
        return true;
    }

    public void TakeDamage(float damage)
    {
        if (State == Behaviour.Dead || damage <= 0 || DungeonStartScreen.BattleEnded) return;
        Health = Mathf.Max(0, Health - damage);
        properties.SetColor("_BaseColor", new Color(1, .48f, .25f));
        foreach (var r in boneRenderers) if (r != null) r.SetPropertyBlock(properties);
        hitFlash = .12f;
        if (Health > 0) { if (State == Behaviour.Idle) State = Behaviour.Chase; return; }
        State = Behaviour.Dead; timer = 0;
        controller.enabled = false;
        foreach (var r in boneRenderers) if (r != null) r.SetPropertyBlock(null);
    }
}
