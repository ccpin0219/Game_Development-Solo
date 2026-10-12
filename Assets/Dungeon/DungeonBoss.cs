using UnityEngine;

public class DungeonBoss : MonoBehaviour, IDungeonDamageable
{
    public float maxHealth = 1000f;
    public Transform upperBody;
    public Material weakPointMaterial, brokenWeakPointMaterial;
    public Mesh weakPointCollisionMesh;
    public float Health { get; private set; }
    public bool Defeated => Health <= 0;
    const string BestTimeKey = "DungeonBoss.FastestClearSeconds.v1";
    public float BattleTime { get; private set; }
    public float BestClearTime { get; private set; }
    public bool ClearRecorded { get; private set; }
    public bool NewBest { get; private set; }
    Font timerFont;
    Vector3 restPosition;
    public float moveSpeed = 4.5f, attackRange = 18f, attackRadius = 5f;
    public float windupSeconds = .9f, recoverySeconds = 1.44f, attackDamage = 25f;
    public string State { get; private set; } = "Chase";
    public int AttacksResolved { get; private set; }
    DungeonArcher player;
    float stateTime;
    int hammerPhase;
    float openingDelay = 3f;
    int nextAttack;
    public int ActiveAttackCount => (leap.Active?1:0) + NonJumpAttackCount;
    int NonJumpAttackCount => (hammerPhase!=0 || hammerImpactTimer>0?1:0)
        + (volley!=null?1:0) + (specials.ActivePattern!=""?1:0)
        + (circleAttacks.ActivePattern!=""?1:0) + (laserAttacks.ActivePattern!=""?1:0)
        + (floorAttack.Active?1:0);
    Vector3 strikePoint;
    LineRenderer warning;
    MeshRenderer hammerImpact;
    Material hammerImpactMaterial;
    float hammerImpactTimer;
    Transform hammer;
    DungeonRockVolley volley;
    float rockCooldown = 3.2f;
    float hammerCooldown;
    public float hammerInterval = 4.8f;
    public float rockInterval = 7.2f;
    public int rocksPerVolley = 8;
    public DungeonBossPart[] Parts { get; private set; }
    bool hammerBroken, throwingArmBroken;
    int brokenLegs;
    DungeonBossSpecialAttacks specials, circleAttacks, laserAttacks;
    DungeonBossLeap leap;
    DungeonBossFloorAttack floorAttack;
    DungeonBossMotion motion;

    void Awake()
    {
        BestClearTime=PlayerPrefs.GetFloat(BestTimeKey,-1f);
        timerFont=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Apple SD Gothic Neo","Noto Sans CJK KR"},20);
        Health = maxHealth;
        restPosition = upperBody.localPosition;
        player = FindFirstObjectByType<DungeonArcher>();
        // Group the existing hammer pieces around the hand for a readable swing.
        hammer = new GameObject("Hammer Pivot").transform;
        hammer.SetParent(upperBody, false); hammer.localPosition = new Vector3(4.4f,5.4f,-.7f);
        foreach (string name in new[] { "War Hammer Handle", "War Hammer Head", "Hammer Rune" })
        {
            var part = upperBody.Find(name);
            if (part != null) part.SetParent(hammer, true);
        }
        var ring = new GameObject("Attack Warning");
        warning = ring.AddComponent<LineRenderer>();
        warning.useWorldSpace = true; warning.loop = true; warning.positionCount = 64;
        warning.startWidth = .14f; warning.endWidth = .14f;
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.color = new Color(1,.22f,.025f); warning.sharedMaterial = material;
        warning.enabled = false;
        hammerImpactMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        hammerImpactMaterial.color=new Color(1,.02f,.02f);
        var area=new GameObject("Hammer Attack Red Area");area.transform.SetParent(transform,false);
        area.AddComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        hammerImpact=area.AddComponent<MeshRenderer>();hammerImpact.sharedMaterial=hammerImpactMaterial;
        hammerImpact.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        hammerImpact.enabled=false;
        BuildWeakPoints();
        maxHealth=0;foreach(var point in Parts)maxHealth+=point.MaxHealth;
        Health=maxHealth;
        var head=upperBody.Find("Simple Head");
        var eyes=new System.Collections.Generic.List<Transform>();
        foreach(Transform child in upperBody)if(child.name=="Eye")eyes.Add(child);
        foreach(var eye in eyes)eye.SetParent(head,true);
        specials=gameObject.AddComponent<DungeonBossSpecialAttacks>();
        specials.Initialize(this,player);
        circleAttacks=gameObject.AddComponent<DungeonBossSpecialAttacks>();
        circleAttacks.Initialize(this,player);
        laserAttacks=gameObject.AddComponent<DungeonBossSpecialAttacks>();
        laserAttacks.Initialize(this,player);
        leap=gameObject.AddComponent<DungeonBossLeap>();
        leap.Initialize(this,player);
        floorAttack=gameObject.AddComponent<DungeonBossFloorAttack>();
        floorAttack.Initialize(this,player);
        motion = new DungeonBossMotion(this, hammer);
    }
    public void BuildWeakPoints()
    {
        var parts=new System.Collections.Generic.List<DungeonBossPart>();
        foreach(var child in GetComponentsInChildren<Transform>())
        {
            string label=null;
            if(child.name=="Simple Arm")label=child.localPosition.x<0?"Left Arm":"Right Arm";
            else if(child.name=="Simple Leg")label=child.localPosition.x<0?"Left Leg":"Right Leg";
            else if(child.name=="Simple Head")label="Head";
            else if(child.name=="Simple Body")label="Torso";
            if(label==null)continue;
            var orb=child.Find("Weak Point");
            if(orb==null)
            {
                var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name="Weak Point";
                orb=g.transform;orb.SetParent(child,false);
            }
            Vector3 scale=child.lossyScale;
            // Embed the sphere centrally, leaving a small hittable cap on both faces.
            float diameter=Mathf.Abs(scale.z)+.4f;
            orb.localPosition=Vector3.zero;
            orb.localRotation=Quaternion.identity;
            float width=Mathf.Min(diameter,Mathf.Abs(scale.x)*.8f);
            float height=Mathf.Min(diameter,Mathf.Abs(scale.y)*.8f);
            orb.localScale=new Vector3(width/scale.x,height/scale.y,diameter/scale.z);
            if(label=="Head")
            {
                // A shallow embedded cap: only 5 mm extends past the front face.
                float faceSize=Mathf.Min(Mathf.Abs(scale.x),Mathf.Abs(scale.y))*.7f;
                orb.localPosition=new Vector3(0,0,-.5f+.015f/scale.z);
                orb.localScale=new Vector3(faceSize/scale.x,faceSize/scale.y,.04f/scale.z);
            }
            // A sphere collider expands to the largest axis; use the actual ellipsoid surface instead.
            var sphere=orb.GetComponent<SphereCollider>();
            if(sphere!=null){if(Application.isPlaying){sphere.enabled=false;Destroy(sphere);}else DestroyImmediate(sphere);}
            var hitShape=orb.GetComponent<MeshCollider>();if(hitShape==null)hitShape=orb.gameObject.AddComponent<MeshCollider>();
            hitShape.sharedMesh=weakPointCollisionMesh;hitShape.convex=true;
            var part=orb.GetComponent<DungeonBossPart>();if(part==null)part=orb.gameObject.AddComponent<DungeonBossPart>();
            part.Initialize(this,label,label=="Torso"?300:label=="Head"?200:150);parts.Add(part);
        }
        Parts=parts.ToArray();
    }
    public void TakeDamage(float damage)
    {
        if (Defeated || damage <= 0) return;
        Health = Mathf.Max(0, Health - damage);
        if (Defeated)
        {
            RecordClear();
            State = "Defeated";
            specials.Cancel();circleAttacks.Cancel();laserAttacks.Cancel();
            leap.Cancel();
            floorAttack.Cancel();
            warning.enabled = false;
            hammerImpact.enabled=false;
            if (volley != null) Destroy(volley.gameObject);
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        }
    }
    void Update()
    {
        if (player != null && player.IsDead && volley != null) Destroy(volley.gameObject);
        if(player!=null && player.IsDead){hammerPhase=0;hammerImpact.enabled=false;specials.Cancel();circleAttacks.Cancel();laserAttacks.Cancel();leap.Cancel();floorAttack.Cancel();}
        if (player != null && !player.IsDead && Cursor.lockState == CursorLockMode.Locked)
            Simulate(Time.deltaTime);
        else if (warning != null) warning.enabled = false;
        if (Defeated)
        {
            upperBody.localPosition = Vector3.Lerp(upperBody.localPosition, restPosition + Vector3.down * 3, Time.deltaTime * 2);
            upperBody.localRotation = Quaternion.Slerp(upperBody.localRotation, Quaternion.Euler(-18,0,0), Time.deltaTime * 2);
        }
        if (motion != null && (Defeated || player == null || player.IsDead ||
            (DungeonStartScreen.BattleStarted && Cursor.lockState == CursorLockMode.Locked)))
            AnimateAttacks(Time.deltaTime);
    }
    public void AnimateAttacks(float dt)
    {
        motion.Apply(dt, Defeated || player == null || player.IsDead,
            hammerPhase, stateTime, hammerImpactTimer,
            volley, specials, circleAttacks, laserAttacks, floorAttack, leap);
    }
    public void Simulate(float dt)
    {
        if(!DungeonStartScreen.BattleStarted || !DungeonExploration.ArenaReady)return;
        if(Defeated || player==null || player.IsDead)return;
        BattleTime+=Mathf.Max(0,dt);
        if(openingDelay>0)
        {
            float wait=Mathf.Min(dt,openingDelay);
            openingDelay-=wait;dt-=wait;
            if(openingDelay>0){State="Waiting";return;}
        }
        hammerImpactTimer=Mathf.Max(0,hammerImpactTimer-dt);
        if(hammerImpactTimer<=0)hammerImpact.enabled=false;
        rockCooldown=Mathf.Max(0,rockCooldown-dt);
        hammerCooldown=Mathf.Max(0,hammerCooldown-dt);
        specials.AdvanceCooldowns(dt);circleAttacks.AdvanceCooldowns(dt);laserAttacks.AdvanceCooldowns(dt);
        leap.AdvanceCooldown(dt);floorAttack.AdvanceCooldown(dt);
        Vector3 delta=player.transform.position-transform.position;delta.y=0;
        float distance=delta.magnitude;
        // Jump owns the entire attack budget, including its landing effect.
        if(leap.Active)
        {
            if(leap.Simulate(dt))AttacksResolved++;
            State=leap.Active?"Leap":"Chase";
            return;
        }
        SimulateHammer(dt);
        TickSpecial(specials,dt);TickSpecial(circleAttacks,dt);TickSpecial(laserAttacks,dt);
        if(volley!=null)
        {
            volley.Simulate(dt);
            if(volley.Finished){Destroy(volley.gameObject);volley=null;rockCooldown=rockInterval;AttacksResolved++;}
        }
        if(floorAttack.Active && floorAttack.Simulate(dt))AttacksResolved++;
        hammerImpact.transform.SetPositionAndRotation(strikePoint,Quaternion.identity);
        if(player.IsDead)return;

        // Drain existing attacks before a ready jump; never cancel them or refill their slots.
        bool waitingForJump=leap.Ready(distance);
        if(waitingForJump && NonJumpAttackCount==0)
        {
            if(leap.TryBegin(distance)){State="Leap";return;}
            waitingForJump=false;
        }
        if(!waitingForJump)
        {
            // Rotate selection priority so long-running patterns do not starve other ready attacks.
            int first=nextAttack;
            for(int i=0;i<6 && NonJumpAttackCount<2;i++)
            {
                int choice=(first+i)%6;
                if(TryStartAttack(choice,distance))nextAttack=(choice+1)%6;
            }
        }
        if(!waitingForJump && hammerPhase==0)
        {
            if(delta.sqrMagnitude>.01f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(-delta.normalized),100*dt);
            if(distance>attackRange)MoveTowards(delta.normalized,dt);
        }
        State=NonJumpAttackCount>0?"Attacking":"Chase";
    }
    bool TryStartAttack(int choice,float distance)
    {
        switch(choice)
        {
            case 0:
                // The hammer now strikes a warned player location at long range.
                if(hammerPhase!=0 || hammerImpactTimer>0 || hammerBroken || hammerCooldown>0 || distance<attackRange)return false;
                BeginHammer();return true;
            case 1:return circleAttacks.TryBeginPattern("Circle",distance);
            case 2:return specials.TryBeginPattern("Line",distance);
            case 3:return laserAttacks.TryBeginPattern("Laser",distance);
            case 4:
                if(volley!=null || throwingArmBroken || rockCooldown>0)return false;
                BeginRockVolley();return volley!=null;
            case 5:return floorAttack.TryBegin();
        }
        return false;
    }
    void TickSpecial(DungeonBossSpecialAttacks attack,float dt)
    {
        if(attack.ActivePattern!="" && attack.Simulate(dt))AttacksResolved++;
    }
    void BeginHammer()
    {
        hammerCooldown=hammerInterval;hammerPhase=1;stateTime=0;
        strikePoint=player.transform.position;strikePoint.y=.08f;
        hammerImpact.enabled=false;
        hammerImpact.transform.localScale=new Vector3(attackRadius,.02f,attackRadius);
        for(int i=0;i<64;i++)
        {
            float a=i*Mathf.PI*2/64;
            warning.SetPosition(i,strikePoint+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*attackRadius);
        }
        warning.enabled=true;
    }
    void SimulateHammer(float dt)
    {
        if(hammerPhase==0)return;
        warning.enabled=true;stateTime+=dt;
        if(hammerPhase==1)
        {
            hammer.localRotation=Quaternion.Euler(Mathf.Lerp(0,155,Mathf.Clamp01(stateTime/windupSeconds)),0,0);
            if(stateTime>=windupSeconds){hammerPhase=2;stateTime=0;}
        }
        else
        {
            hammer.localRotation=Quaternion.Euler(Mathf.Lerp(155,0,Mathf.Clamp01(stateTime/.18f)),0,0);
            if(stateTime>=.18f)
            {
                Vector3 toPlayer=player.transform.position-strikePoint;toPlayer.y=0;
                if(toPlayer.magnitude<=attackRadius+.35f)player.TakeDamage(attackDamage);
                hammerImpact.enabled=true;hammerImpactTimer=.3f;
                AttacksResolved++;warning.enabled=false;hammerPhase=0;stateTime=0;
            }
        }
    }
    public void BeginRockVolley()
    {
        if(Defeated || throwingArmBroken || player==null || player.IsDead || volley!=null || leap.Active || NonJumpAttackCount>=2) return;
        var obj=new GameObject("Guardian Rock Volley");
        volley=obj.AddComponent<DungeonRockVolley>();
        volley.Initialize(this,player,rocksPerVolley);
    }
    void MoveTowards(Vector3 direction, float dt)
    {
        float speed=moveSpeed*Mathf.Max(.4f,1f-brokenLegs*.3f);
        // A broad capsule keeps the giant out of walls and pillars; angled probes steer around them.
        foreach (float angle in new[] { 0f, 35f, -35f, 70f, -70f })
        {
            Vector3 stepDirection = Quaternion.Euler(0,angle,0)*direction;
            bool blocked = false;
            foreach (var hit in Physics.CapsuleCastAll(transform.position+Vector3.up*5.2f,transform.position+Vector3.up*11,5,stepDirection,speed*dt+.3f,~0,QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform)) { blocked = true; break; }
            if (!blocked) { transform.position += stepDirection * speed * dt; break; }
        }
    }
    public void OnPartBroken(DungeonBossPart part)
    {
        specials.OnPartBroken(part.PartName);
        circleAttacks.OnPartBroken(part.PartName);laserAttacks.OnPartBroken(part.PartName);
        if(part.PartName=="Torso")floorAttack.Cancel();
        if(part.PartName.EndsWith("Leg"))brokenLegs++;
        if(part.PartName=="Left Arm")
        {
            throwingArmBroken=true;
            if(volley!=null)Destroy(volley.gameObject);
            volley=null;
        }
        if(part.PartName=="Right Arm")
        {
            hammerBroken=true;
            hammerPhase=0;hammerImpactTimer=0;stateTime=0;warning.enabled=false;hammerImpact.enabled=false;hammer.localRotation=Quaternion.identity;
        }
    }
    void OnDestroy()
    {
        if(timerFont!=null)Destroy(timerFont);
        if(hammerImpactMaterial!=null)Destroy(hammerImpactMaterial);
        if (volley != null) Destroy(volley.gameObject);
        if (warning != null) { Destroy(warning.sharedMaterial); Destroy(warning.gameObject); }
    }
    void OnGUI()
    {
        if(!DungeonStartScreen.BattleStarted || !DungeonExploration.ArenaReady)return;
        float width = Mathf.Min(600, Screen.width * .55f), x = (Screen.width-width)*.5f;
        var label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20, font = timerFont };
        GUI.color = Color.white;
        GUI.Label(new Rect(x,20,width,32), Defeated ? "스톤 가디언 처치 완료" : "스톤 가디언", label);
        GUI.color = new Color(.08f,.07f,.07f,.95f);
        GUI.DrawTexture(new Rect(x,56,width,14),Texture2D.whiteTexture);
        GUI.color = new Color(.8f,.24f,.08f);
        GUI.DrawTexture(new Rect(x+2,58,(width-4)*Health/maxHealth,10),Texture2D.whiteTexture);
        GUI.color = Color.white;
        var timerStyle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=18,font=timerFont};
        GUI.color=new Color(.04f,.04f,.05f,.85f);
        GUI.DrawTexture(new Rect(x,78,width,60),Texture2D.whiteTexture);
        GUI.color=ClearRecorded?new Color(.5f,1f,.65f):Color.white;
        GUI.Label(new Rect(x,80,width,27),(ClearRecorded?"클리어 시간  ":"전투 시간  ")+FormatTime(BattleTime),timerStyle);
        GUI.color=new Color(1f,.8f,.4f);
        GUI.Label(new Rect(x,107,width,27),"최고 기록  "+(BestClearTime<0?"기록 없음":FormatTime(BestClearTime))+(NewBest?"  ·  신기록!":""),timerStyle);
        GUI.color=Color.white;
    }
    void RecordClear()
    {
        if(ClearRecorded || !DungeonStartScreen.BattleStarted || player==null || player.IsDead)return;
        ClearRecorded=true;
        NewBest=BestClearTime<0 || BattleTime<BestClearTime;
        if(!NewBest)return;
        BestClearTime=BattleTime;
        PlayerPrefs.SetFloat(BestTimeKey,BestClearTime);
        PlayerPrefs.Save();
    }
    public static string FormatTime(float seconds)
    {
        int hundredths=Mathf.FloorToInt(Mathf.Max(0,seconds)*100);
        return string.Format("{0:00}:{1:00}.{2:00}",hundredths/6000,(hundredths/100)%60,hundredths%100);
    }
}
