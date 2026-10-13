using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class DungeonArcher : MonoBehaviour
{
    public Camera view;
    public Material wood, metal, stringMaterial;
    public float moveSpeed = 8f, chargeSeconds = 1f;
    const string SensitivityKey = "Dungeon.LookSensitivityLevel.v1";
    public int LookSensitivityLevel { get; private set; } = 5;
    public float LookSensitivity => SensitivityForLevel(LookSensitivityLevel);
    public static float SensitivityForLevel(int level) => .03f + .015f * (Mathf.Clamp(level, 1, 10) - 1);
    public void SetLookSensitivityLevel(int level)
    {
        level = Mathf.Clamp(level, 1, 10);
        if (LookSensitivityLevel == level) return;
        LookSensitivityLevel = level;
        PlayerPrefs.SetInt(SensitivityKey, level);
        PlayerPrefs.Save();
    }
    public enum AttackMode { Heavy, Rapid }
    public AttackMode Mode { get; private set; } = AttackMode.Heavy;
    [Min(.05f)] public float rapidChargeSeconds = .35f;
    // Full-charge cycles: heavy 180 / 1.30s; rapid (20 + 3 * 10) / .47s.
    // Accurate heavy shots win on damage; rapid retains forgiving homing and faster retries.
    [Min(1)] public float heavyDamage = 90f, rapidDamage = 20f;
    [Min(1)] public float fullHeavyMultiplier = 2f;
    [Min(0)] public float heavyReloadSeconds = .3f, rapidReloadSeconds = .12f;
    public float CurrentChargeSeconds => Mathf.Max(.05f, Mode == AttackMode.Heavy ? chargeSeconds : rapidChargeSeconds);
    public float CurrentMaxDamage => Mode == AttackMode.Heavy ? heavyDamage : rapidDamage;
    public float CurrentReloadSeconds => Mode == AttackMode.Heavy ? heavyReloadSeconds : rapidReloadSeconds;
    public float Charge { get; private set; }
    public const float AutoChargeDuration = 5f, AutoChargeCooldownSeconds = 30f;
    public float AutoChargeRemaining { get; private set; }
    public float AutoChargeCooldownRemaining { get; private set; }
    public bool AutoChargeActive => AutoChargeRemaining>0;
    public float EffectiveChargeSeconds => AutoChargeActive ? (Mode==AttackMode.Heavy?.4f:.12f) : CurrentChargeSeconds;
    public bool TryAutoCharge()
    {
        if(!DungeonStartScreen.BattleStarted || DungeonStartScreen.BattleEnded || DungeonStartScreen.IsPaused ||
            IsDead || AutoChargeCooldownRemaining>0)return false;
        AutoChargeRemaining=AutoChargeDuration;
        AutoChargeCooldownRemaining=AutoChargeCooldownSeconds;
        return true;
    }
    // Rapid keeps the player mobile; committing to a heavy charge carries a movement cost.
    public float CurrentMoveSpeed => !drawing ? moveSpeed : moveSpeed *
        (Mode == AttackMode.Rapid ? Mathf.Lerp(.95f, .9f, Charge) : Mathf.Lerp(.65f, .2f, Charge));
    public int ShotsFired { get; private set; }
    public const int MaxHits = 5;
    public int HitsTaken { get; private set; }
    public int RemainingHits => MaxHits - HitsTaken;
    public bool IsDead => RemainingHits <= 0;
    public const float DashCooldownSeconds = 15f;
    const float DashDistance = 6f, DashDuration = .2f;
    public float DashCooldownRemaining { get; private set; }
    public bool IsDashing => dashRemaining > 0;
    float dashRemaining;
    Vector3 dashDirection;
    float dashFxAge, dashFxStrength, dashBaseFov;
    bool dashFxActive;

    void ResetDashFeedback()
    {
        if(dashFxActive && view!=null)view.fieldOfView=dashBaseFov;
        dashFxActive=false;dashFxStrength=0;
    }
    void LateUpdate()
    {
        if(!dashFxActive)return;
        if(IsDead || DungeonStartScreen.BattleEnded || DungeonStartScreen.IsPaused ||
            Cursor.lockState!=CursorLockMode.Locked){ResetDashFeedback();return;}
        dashFxAge+=Time.deltaTime;
        // Brief acceleration followed by a soft release; no camera shake or blur.
        dashFxStrength=Mathf.SmoothStep(0,1,dashFxAge/.04f)*
            (1-Mathf.SmoothStep(0,1,(dashFxAge-.16f)/.22f));
        if(view!=null)view.fieldOfView=dashBaseFov+4f*dashFxStrength;
        if(dashFxAge>=.38f)ResetDashFeedback();
    }

    void DrawDashFeedback()
    {
        if(dashFxStrength<=0 || DungeonStartScreen.IsPaused || DungeonStartScreen.BattleEnded || IsDead)return;
        var oldColor=GUI.color;var oldMatrix=GUI.matrix;
        Vector2 center=new Vector2(Screen.width*.5f,Screen.height*.5f);
        float scale=Mathf.Max(.65f,Screen.height/720f);
        for(int i=0;i<20;i++)
        {
            float angle=(i*18f+7)*Mathf.Deg2Rad;
            Vector2 ray=new Vector2(Mathf.Cos(angle)*Screen.width*.5f,Mathf.Sin(angle)*Screen.height*.5f);
            // Start in the outer 18% of the viewport; keep the aiming area clear.
            ray/=Mathf.Max(Mathf.Abs(Mathf.Cos(angle)),Mathf.Abs(Mathf.Sin(angle)));
            float travel=Mathf.Repeat(dashFxAge*2.5f+i*.137f,1);
            Vector2 start=center+ray*(.82f+travel*.12f);
            float length=(18+i%4*8)*scale;
            GUI.matrix=oldMatrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(ray.y,ray.x)*Mathf.Rad2Deg,start);
            for(int segment=0;segment<3;segment++)
            {
                GUI.color=new Color(.68f,.85f,1f,dashFxStrength*(.06f+segment*.055f));
                GUI.DrawTexture(new Rect(start.x+segment*length/3,start.y,length/3,scale*(i%3==0?2:1)),Texture2D.whiteTexture);
            }
        }
        GUI.matrix=oldMatrix;GUI.color=oldColor;
    }

    public bool TryDash(Vector2 direction)
    {
        if(!DungeonStartScreen.BattleStarted || DungeonStartScreen.BattleEnded ||
            DungeonStartScreen.IsPaused || IsDead || IsDashing || DashCooldownRemaining>0)return false;
        if(direction.sqrMagnitude<.001f)direction=Vector2.up;
        direction.Normalize();
        dashDirection=(transform.right*direction.x+transform.forward*direction.y).normalized;
        dashRemaining=DashDuration;
        DashCooldownRemaining=DashCooldownSeconds;
        ResetDashFeedback();
        dashBaseFov=view!=null?view.fieldOfView:60;
        dashFxAge=0;dashFxActive=true;
        return true;
    }
    // Returns the part of this frame occupied by the dash, so normal movement is not added twice.
    public float AdvanceDash(float dt)
    {
        if(dt<=0 || !DungeonStartScreen.BattleStarted || DungeonStartScreen.BattleEnded ||
            DungeonStartScreen.IsPaused || IsDead)return 0;
        DashCooldownRemaining=Mathf.Max(0,DashCooldownRemaining-dt);
        float used=Mathf.Min(dt,dashRemaining);
        float distance=used*(DashDistance/DashDuration);
        while(distance>0)
        {
            float step=Mathf.Min(.25f,distance);
            controller.Move(dashDirection*step);
            distance-=step;
        }
        dashRemaining=Mathf.Max(0,dashRemaining-used);
        return used;
    }
    float damageFlash, healFlash;
    const float HitMarkerPulseSeconds = .11f;
    int pendingHitMarkers;
    float hitMarkerRemaining;
    // Each hit gets a visible pulse followed by a blank gap, even for same-frame shards.
    public float HitMarkerAlpha => pendingHitMarkers == 0 ? 0 : Mathf.Clamp01((hitMarkerRemaining - .035f) / .035f);
    public void ConfirmArrowHit()
    {
        if (!DungeonStartScreen.BattleStarted || IsDead) return;
        if (pendingHitMarkers == 0) hitMarkerRemaining = HitMarkerPulseSeconds;
        pendingHitMarkers++;
    }
    void AdvanceHitMarkers(float dt)
    {
        if (pendingHitMarkers == 0 || dt <= 0) return;
        // Preserve the on/off separation during a slow frame instead of skipping queued hits.
        hitMarkerRemaining -= Mathf.Min(dt, .03f);
        while (hitMarkerRemaining <= 0 && pendingHitMarkers > 0)
        {
            pendingHitMarkers--;
            if (pendingHitMarkers > 0) hitMarkerRemaining += HitMarkerPulseSeconds;
        }
    }
    Font hudFont;
    CharacterController controller;
    Transform bow, nockedArrow;
    LineRenderer cord, bowLimb;
    LineRenderer powerGlow;
    float drawDistance, drawVelocity, shotAge = 1f, shotStrength;
    float pitch, verticalSpeed, cooldown;
    bool drawing;

    void Awake()
    {
        LookSensitivityLevel = Mathf.Clamp(PlayerPrefs.GetInt(SensitivityKey, 5), 1, 10);
        hudFont=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Apple SD Gothic Neo","Noto Sans CJK KR"},18);
        controller = GetComponent<CharacterController>();
        BuildBow();
        powerGlow=DungeonPowerShotVfx.Line(nockedArrow,"Charged Arrow Core",.065f,new Color(3,.12f,.03f,.9f),2);
        powerGlow.SetPosition(0,new Vector3(0,0,-.3f));powerGlow.SetPosition(1,new Vector3(0,0,.4f));
        powerGlow.enabled=false;
    }
    void Start() { if(DungeonStartScreen.BattleStarted)LockCursor(); }
    void LockCursor() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    void OnDisable() { ResetDashFeedback();CancelDraw(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    void OnApplicationFocus(bool focus) { if (!focus) CancelDraw(); }
    void CancelDraw()
    {
        drawing = false; Charge = 0;
        if(bow!=null && cord!=null && nockedArrow!=null)UpdateBow();
    }
    public void ToggleAttackMode()
    {
        if (IsDead || !DungeonStartScreen.BattleStarted || DungeonStartScreen.BattleEnded) return;
        CancelDraw();
        Mode = Mode == AttackMode.Heavy ? AttackMode.Rapid : AttackMode.Heavy;
        // Preserve the previous shot's reload so switching cannot bypass it.
    }
    public bool BeginDraw()
    {
        if (IsDead || !DungeonStartScreen.BattleStarted || DungeonStartScreen.BattleEnded || cooldown > 0) return false;
        drawing = true;
        return true;
    }
    public void AdvanceCharge(float dt, bool allowCharge = true, bool manualHeld = false)
    {
        if(dt<=0 || IsDead || !DungeonStartScreen.BattleStarted || DungeonStartScreen.BattleEnded || DungeonStartScreen.IsPaused)return;
        bool wasAutomatic=AutoChargeActive;
        float automaticTime=Mathf.Min(dt,AutoChargeRemaining);
        if(allowCharge)
        {
            if(wasAutomatic && !drawing && cooldown<=0)BeginDraw();
            if(drawing)
            {
                float gain=automaticTime/(Mode==AttackMode.Heavy?.4f:.12f)+(dt-automaticTime)/CurrentChargeSeconds;
                Charge=Mathf.Clamp01(Charge+gain);
            }
        }
        AutoChargeRemaining=Mathf.Max(0,AutoChargeRemaining-dt);
        AutoChargeCooldownRemaining=Mathf.Max(0,AutoChargeCooldownRemaining-dt);
        if(wasAutomatic && !AutoChargeActive && !manualHeld)CancelDraw();
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || DungeonStartScreen.BattleEnded || damage <= 0) return;
        HitsTaken = Mathf.Min(MaxHits, HitsTaken + 1);
        damageFlash = .3f;
        if (IsDead) { CancelDraw(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    public bool Revive()
    {
        if(!IsDead)return false;
        HitsTaken=0;
        damageFlash=0;healFlash=.4f;
        return true;
    }

    public bool Heal(int hits = 1)
    {
        if (IsDead || hits <= 0 || HitsTaken == 0) return false;
        HitsTaken = Mathf.Max(0, HitsTaken - hits);
        healFlash = .4f;
        return true;
    }

    void Update()
    {
        if(!DungeonStartScreen.BattleStarted)return;
        AdvanceHitMarkers(Time.unscaledDeltaTime);
        if(DungeonStartScreen.BattleEnded || DungeonStartScreen.IsPaused){CancelDraw();return;}
        var mouse = Mouse.current; var keys = Keyboard.current;
        if (mouse == null || keys == null) return;
        damageFlash = Mathf.Max(0, damageFlash - Time.deltaTime);
        healFlash = Mathf.Max(0, healFlash - Time.deltaTime);
        if (IsDead)return;
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (mouse.leftButton.wasPressedThisFrame) LockCursor();
            AdvanceBowAnimation(Time.deltaTime);
            UpdateBow();
            return;
        }
        Vector2 look = mouse.delta.ReadValue() * LookSensitivity;
        transform.Rotate(0, look.x, 0);
        pitch = Mathf.Clamp(pitch - look.y, -80, 80);
        view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        Vector2 input = new Vector2((keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0),
            (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0));
        input = Vector2.ClampMagnitude(input, 1);
        if(keys.leftShiftKey.wasPressedThisFrame || keys.rightShiftKey.wasPressedThisFrame)
            TryDash(input);
        float dashTime=AdvanceDash(Time.deltaTime);
        if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
        if (controller.isGrounded && keys.spaceKey.wasPressedThisFrame) verticalSpeed = 6;
        verticalSpeed -= 20 * Time.deltaTime;
        cooldown = Mathf.Max(0, cooldown - Time.deltaTime);
        if(keys.qKey.wasPressedThisFrame)TryAutoCharge();
        bool releaseAfterMovement = false;
        bool chargeCancelled=false;
        if (keys.tabKey.wasPressedThisFrame)
        {
            ToggleAttackMode();
            chargeCancelled=true;
        }
        else if (mouse.rightButton.wasPressedThisFrame)
        {
            CancelDraw();
            chargeCancelled=true;
        }
        else
        {
            if (mouse.leftButton.wasPressedThisFrame && !AutoChargeActive) BeginDraw();
        }
        bool automaticThisFrame=AutoChargeActive;
        AdvanceCharge(Time.deltaTime,!chargeCancelled,mouse.leftButton.isPressed);
        if(!chargeCancelled)releaseAfterMovement=drawing &&
            (automaticThisFrame?mouse.leftButton.wasPressedThisFrame:mouse.leftButton.wasReleasedThisFrame);
        // Apply this frame's charge state so release/cancel restores speed immediately.
        float frameSpeed = releaseAfterMovement ? moveSpeed : CurrentMoveSpeed;
        controller.Move((transform.right * input.x + transform.forward * input.y) * frameSpeed * (Time.deltaTime-dashTime)
            + Vector3.up * verticalSpeed * Time.deltaTime);
        AdvanceBowAnimation(Time.deltaTime);
        UpdateBow();
        if (releaseAfterMovement) ReleaseArrow();
    }

    public void ReleaseArrow()
    {
        if (IsDead || !drawing || cooldown > 0 || !DungeonStartScreen.BattleStarted || DungeonStartScreen.BattleEnded) return;
        float power = Charge;
        Physics.SyncTransforms();
        var projectile = new GameObject("Arrow");
        // Resolve the muzzle after this frame's movement so the shot leaves the visible bow.
        Vector3 cameraOrigin = view.transform.position;
        Vector3 origin = nockedArrow.TransformPoint(new Vector3(0, 0, .3f));
        Vector3 muzzleOffset = origin - cameraOrigin;
        float clearance = muzzleOffset.magnitude;
        foreach (var hit in Physics.SphereCastAll(cameraOrigin, .08f, muzzleOffset.normalized,
                     clearance, ~0, QueryTriggerInteraction.Ignore))
            if (hit.collider != controller) clearance = Mathf.Min(clearance, Mathf.Max(0, hit.distance - .06f));
        origin = cameraOrigin + muzzleOffset.normalized * clearance;

        // Converge from the offset bow to the crosshair, including close targets.
        float aimDistance = 200f;
        foreach (var hit in Physics.SphereCastAll(cameraOrigin, .045f, view.transform.forward,
                     aimDistance, ~0, QueryTriggerInteraction.Collide))
            if (hit.collider != controller && DungeonArrow.CanHit(hit.collider)) aimDistance = Mathf.Min(aimDistance, hit.distance);
        if (aimDistance <= Vector3.Dot(origin - cameraOrigin, view.transform.forward) + .1f)
            origin = cameraOrigin;
        Vector3 direction = (cameraOrigin + view.transform.forward * aimDistance - origin).normalized;
        projectile.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
        var arrow = projectile.AddComponent<DungeonArrow>();
        float damage = Mathf.Lerp(CurrentMaxDamage * .2f, CurrentMaxDamage, power);
        var shot = DungeonArrow.ShotKind.Normal;
        if (power >= 1f)
        {
            shot = Mode == AttackMode.Heavy ? DungeonArrow.ShotKind.Power : DungeonArrow.ShotKind.Split;
            if (Mode == AttackMode.Heavy) damage *= fullHeavyMultiplier;
        }
        arrow.Initialize(direction * Mathf.Lerp(24, 75, power), damage, controller, wood, metal, shot);
        shotAge=0;shotStrength=shot==DungeonArrow.ShotKind.Power?1f:.5f;
        ShotsFired++; cooldown = CurrentReloadSeconds; CancelDraw();
    }

    void AdvanceBowAnimation(float dt)
    {
        if(dt<=0)return;
        float target=drawing?Mathf.SmoothStep(0,1,Charge)*(Mode==AttackMode.Heavy?.28f:.24f):0;
        // Release is a fast elastic return; cancelling gently relaxes the string.
        float smoothing=drawing?.045f:shotAge<.12f?.025f:.10f;
        drawDistance=Mathf.SmoothDamp(drawDistance,target,ref drawVelocity,smoothing,Mathf.Infinity,dt);
        shotAge+=dt;
    }

    void UpdateBow()
    {
        float pull=drawDistance;
        float draw=Mathf.Clamp01(pull/.28f);
        float tipZ=-.08f*draw;
        // Keep each half of the string the same length as the tips flex backwards.
        float vibration=drawing?0:Mathf.Sin(shotAge*110)*.009f*Mathf.Exp(-shotAge*24)*shotStrength;
        float nockZ=-pull+vibration;
        float tipY=Mathf.Sqrt(.65f*.65f-(nockZ-tipZ)*(nockZ-tipZ));
        float shotKick=shotStrength*(1-Mathf.Exp(-shotAge/ .015f))*Mathf.Exp(-shotAge/ .085f);
        bow.localScale = Vector3.one * .72f;
        bow.localRotation = Quaternion.Euler(-2.8f*shotKick, 0, -8-1.2f*draw);
        // Pivot around the grip instead of dragging the whole bow sideways as it bends.
        Vector3 grip=new Vector3(-.25f,0,0)*.72f;
        Vector3 restingGrip=new Vector3(.43f,-.12f,.72f)+Quaternion.Euler(0,0,-8)*grip;
        bow.localPosition=restingGrip-bow.localRotation*grip+new Vector3(0,-.012f,-.055f)*shotKick;
        if(powerGlow!=null)
        {
            powerGlow.enabled=drawing && Mode==AttackMode.Heavy && Charge>=1;
            powerGlow.widthMultiplier=.065f*(1+.15f*Mathf.Sin(Time.time*18));
        }
        if(bowLimb!=null)
            for(int i=0;i<bowLimb.positionCount;i++)
            {
                float t=i/(float)(bowLimb.positionCount-1);
                float flex=Mathf.Pow(Mathf.Abs(t*2-1),2);
                bowLimb.SetPosition(i,new Vector3(-.25f*Mathf.Sin(t*Mathf.PI),Mathf.Lerp(-tipY,tipY,t),tipZ*flex));
            }
        var nock=new Vector3(0,0,nockZ);
        cord.positionCount=3;
        cord.SetPosition(0, new Vector3(0, tipY, tipZ));
        cord.SetPosition(1, nock);
        cord.SetPosition(2, new Vector3(0, -tipY, tipZ));
        // Keep the back of the arrow attached to the moving centre of the string.
        nockedArrow.localPosition = nock+Vector3.forward*.3f;
        nockedArrow.gameObject.SetActive(cooldown <= 0);
    }

    public void BuildBow()
    {
        var existing = view.transform.Find("First Person Bow");
        if (existing != null) { bow = existing; cord = bow.Find("Bowstring").GetComponent<LineRenderer>(); bowLimb=bow.Find("Plain Wooden Bow").GetComponent<LineRenderer>(); nockedArrow = bow.Find("Nocked Arrow"); UpdateBow(); return; }
        bow = new GameObject("First Person Bow").transform; bow.SetParent(view.transform, false);
        var limbObject = new GameObject("Plain Wooden Bow"); limbObject.transform.SetParent(bow,false);
        var limb = limbObject.AddComponent<LineRenderer>();
        bowLimb=limb;
        limb.useWorldSpace=false;limb.positionCount=21;limb.sharedMaterial=wood;
        limb.generateLightingData=true;limb.numCornerVertices=2;limb.numCapVertices=2;
        limb.widthCurve=new AnimationCurve(new Keyframe(0,.022f),new Keyframe(.5f,.046f),new Keyframe(1,.022f));
        limb.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        for(int i=0;i<21;i++)
        {
            float t=i/20f;
            limb.SetPosition(i,new Vector3(-.25f*Mathf.Sin(t*Mathf.PI),Mathf.Lerp(-.65f,.65f,t),0));
        }
        var line = new GameObject("Bowstring"); line.transform.SetParent(bow,false); cord=line.AddComponent<LineRenderer>();
        cord.useWorldSpace=false;cord.positionCount=2;cord.startWidth=.004f;cord.endWidth=.004f;cord.sharedMaterial=stringMaterial;
        nockedArrow = new GameObject("Nocked Arrow").transform; nockedArrow.SetParent(bow,false);
        DungeonArrow.CreateVisual(nockedArrow, wood, metal);
        UpdateBow();
    }

    public static void Segment(string name, Transform parent, Vector3 a, Vector3 b, float width, Material material)
    {
        // Mesh-only objects avoid creating weapon colliders even for a single frame.
        var obj = new GameObject(name); obj.transform.SetParent(parent,false);
        obj.transform.localPosition=(a+b)*.5f; obj.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
        obj.transform.localScale=new Vector3(width,(b-a).magnitude,width);
        obj.AddComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        var renderer=obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void OnDestroy() { if(hudFont!=null)Destroy(hudFont); }

    void OnGUI()
    {
        if(!DungeonStartScreen.BattleStarted)return;
        DrawDashFeedback();
        if (healFlash > 0)
        {
            GUI.color = new Color(.05f,1f,.2f,healFlash * .35f);
            GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);
        }
        if (damageFlash > 0)
        {
            GUI.color = new Color(.8f,0,0,damageFlash * .65f);
            GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
        GUI.Label(new Rect(20,18,100,24), "체력",new GUIStyle(GUI.skin.label){font=hudFont,fontSize=18});
        for(int i=0;i<MaxHits;i++)
        {
            GUI.color=new Color(.12f,.12f,.14f,.95f);
            GUI.DrawTexture(new Rect(20+i*30,46,24,24),Texture2D.whiteTexture);
            GUI.color=i<RemainingHits ? new Color(1f,.25f,.22f) : new Color(.25f,.25f,.28f);
            GUI.DrawTexture(new Rect(23+i*30,49,18,18),Texture2D.whiteTexture);
        }
        GUI.color=Color.white;
        if (IsDead || DungeonStartScreen.BattleEnded)return;
        DrawSkillHud();
        float x=Screen.width*.5f, y=Screen.height*.5f;
        GUI.color=Color.Lerp(new Color(1,.9f,.65f,.9f),new Color(1,.12f,.08f,1),HitMarkerAlpha);
        GUI.DrawTexture(new Rect(x-7,y-1,14,2),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x-1,y-7,2,14),Texture2D.whiteTexture);
        DrawHitMarker(x,y);
        GUI.color=new Color(.08f,.09f,.12f,.8f);GUI.DrawTexture(new Rect(x-61,y+27,122,8),Texture2D.whiteTexture);
        Color modeColor=Mode==AttackMode.Heavy?new Color(1,.25f,.15f):new Color(.2f,.65f,1);
        GUI.color=modeColor;
        GUI.DrawTexture(new Rect(x-60,y+28,120*Charge,6),Texture2D.whiteTexture);
        GUI.color=Color.white;
        var modeStyle=new GUIStyle(GUI.skin.label){font=hudFont,fontSize=22,alignment=TextAnchor.MiddleRight};
        modeStyle.normal.textColor=modeColor;
        GUI.Label(new Rect(Screen.width-330,Screen.height-105,300,34),Mode==AttackMode.Heavy?"강공":"속공",modeStyle);
        if(Cursor.lockState!=CursorLockMode.Locked)
        {
            var pauseStyle=new GUIStyle(GUI.skin.label){font=hudFont,fontSize=28,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            GUI.Label(new Rect(x-200,y+48,400,44),"일시정지",pauseStyle);
            var resumeStyle=new GUIStyle(pauseStyle){fontSize=18,fontStyle=FontStyle.Normal};
            GUI.Label(new Rect(x-200,y+92,400,32),"클릭하면 계속합니다",resumeStyle);
        }
    }
    void DrawSkillHud()
    {
        var matrix=GUI.matrix;var color=GUI.color;
        try
        {
            float scale=Mathf.Min(1.25f,Screen.width/1280f,Screen.height/720f);
            GUI.matrix=Matrix4x4.TRS(new Vector3(20*scale,Screen.height-170*scale,0),Quaternion.identity,Vector3.one*scale);
            DrawSkillTile(0,"SHIFT","돌진",DashCooldownRemaining,DashCooldownSeconds,IsDashing,0,new Color(1,.72f,.3f),false);
            DrawSkillTile(112,"Q","자동 충전",AutoChargeCooldownRemaining,AutoChargeCooldownSeconds,AutoChargeActive,AutoChargeRemaining,new Color(.35f,.88f,.7f),true);
        }
        finally{GUI.matrix=matrix;GUI.color=color;}
    }
    void DrawSkillTile(float x,string key,string title,float cooldown,float maximum,bool active,float remaining,Color accent,bool chargeIcon)
    {
        var small=new GUIStyle(GUI.skin.label){font=hudFont,fontSize=14,alignment=TextAnchor.MiddleCenter};
        var number=new GUIStyle(small){fontSize=27,fontStyle=FontStyle.Bold};
        SkillFill(new Rect(x,22,88,88),active||cooldown<=0?accent:new Color(.3f,.37f,.4f));
        SkillFill(new Rect(x+2,24,84,84),new Color(.035f,.06f,.075f,.96f));
        GUI.color=Color.white;GUI.Label(new Rect(x,0,88,22),title,small);
        Color ink=active||cooldown<=0?accent:new Color(.35f,.43f,.47f);
        Vector2 c=new Vector2(x+44,63);
        if(chargeIcon)
        {
            SkillStroke(c+new Vector2(8,-24),c+new Vector2(-13,3),ink,6);
            SkillStroke(c+new Vector2(-13,3),c+new Vector2(12,3),ink,6);
            SkillStroke(c+new Vector2(12,3),c+new Vector2(-8,26),ink,6);
        }
        else
        {
            for(int i=0;i<2;i++)
            {
                float offset=-17+i*23;
                SkillStroke(c+new Vector2(offset-9,-17),c+new Vector2(offset+9,0),ink,5);
                SkillStroke(c+new Vector2(offset+9,0),c+new Vector2(offset-9,17),ink,5);
            }
        }
        if(cooldown>0 || active)
        {
            SkillFill(new Rect(x+3,25,82,75),new Color(0,0,0,.4f));
            GUI.color=Color.white;
            GUI.Label(new Rect(x+2,39,84,44),active?(chargeIcon?remaining.ToString("0.0"):"돌진"):Mathf.CeilToInt(cooldown).ToString(),number);
        }
        float progress=active&&chargeIcon?remaining/AutoChargeDuration:1-Mathf.Clamp01(cooldown/maximum);
        SkillFill(new Rect(x+5,103,78*progress,3),accent);
        SkillFill(new Rect(x+20,113,48,23),new Color(.12f,.18f,.22f,.95f));
        GUI.color=Color.white;GUI.Label(new Rect(x+20,113,48,23),key,small);
        var state=new GUIStyle(small){fontSize=11};
        GUI.Label(new Rect(x-8,137,104,18),active?(chargeIcon?"사용 중 · 대기 "+Mathf.CeilToInt(cooldown)+"초":"사용 중"):cooldown>0?"재사용 대기":"준비 완료",state);
    }
    static void SkillFill(Rect rect,Color color)
    {
        var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;
    }
    static void SkillStroke(Vector2 a,Vector2 b,Color color,float width)
    {
        var matrix=GUI.matrix;
        try
        {
            GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(a.x,a.y,0),Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg),Vector3.one);
            SkillFill(new Rect(0,-width*.5f,Vector2.Distance(a,b),width),color);
        }
        finally{GUI.matrix=matrix;}
    }
    void DrawHitMarker(float x, float y)
    {
        float alpha=HitMarkerAlpha;
        if(alpha<=0)return;
        var matrix=GUI.matrix;var color=GUI.color;
        float scale=Mathf.Clamp(Screen.height/720f,.85f,1.5f);
        float pop=1+.15f*alpha;
        // Four separated diagonal strokes leave the exact aiming point unobstructed.
        for(int i=0;i<4;i++)
        {
            GUI.matrix=matrix;
            GUIUtility.RotateAroundPivot(45+i*90,new Vector2(x,y));
            GUI.color=new Color(.04f,0,0,alpha*.85f);
            GUI.DrawTexture(new Rect(x+9*scale*pop,y-2.5f*scale,12*scale*pop,5*scale),Texture2D.whiteTexture);
            GUI.color=new Color(1,.12f,.08f,alpha);
            GUI.DrawTexture(new Rect(x+10*scale*pop,y-1.25f*scale,10*scale*pop,2.5f*scale),Texture2D.whiteTexture);
        }
        GUI.matrix=matrix;GUI.color=color;
    }
}

