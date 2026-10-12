using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class DungeonArcher : MonoBehaviour
{
    public Camera view;
    public Material wood, metal, stringMaterial;
    public float moveSpeed = 8f, chargeSeconds = 1.2f;
    public enum AttackMode { Heavy, Rapid }
    public AttackMode Mode { get; private set; } = AttackMode.Heavy;
    [Min(.05f)] public float rapidChargeSeconds = .35f;
    [Min(1)] public float heavyDamage = 90f, rapidDamage = 25f;
    [Min(0)] public float heavyReloadSeconds = .35f, rapidReloadSeconds = .12f;
    public float CurrentChargeSeconds => Mathf.Max(.05f, Mode == AttackMode.Heavy ? chargeSeconds : rapidChargeSeconds);
    public float CurrentMaxDamage => Mode == AttackMode.Heavy ? heavyDamage : rapidDamage;
    public float CurrentReloadSeconds => Mode == AttackMode.Heavy ? heavyReloadSeconds : rapidReloadSeconds;
    public float Charge { get; private set; }
    public float CurrentMoveSpeed => moveSpeed * (drawing ? Mathf.Lerp(.8f, .35f, Charge) : 1f);
    public int ShotsFired { get; private set; }
    public const int MaxHits = 5;
    public int HitsTaken { get; private set; }
    public int RemainingHits => MaxHits - HitsTaken;
    public bool IsDead => RemainingHits <= 0;
    float damageFlash, healFlash;
    Font hudFont;
    CharacterController controller;
    Transform bow, nockedArrow;
    LineRenderer cord;
    float pitch, verticalSpeed, cooldown;
    bool drawing;

    void Awake()
    {
        hudFont=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Apple SD Gothic Neo","Noto Sans CJK KR"},18);
        controller = GetComponent<CharacterController>();
        BuildBow();
    }
    void Start() { if(DungeonStartScreen.BattleStarted)LockCursor(); }
    void LockCursor() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    void OnDisable() { CancelDraw(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    void OnApplicationFocus(bool focus) { if (!focus) CancelDraw(); }
    void CancelDraw() { drawing = false; Charge = 0; }
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
    public void AdvanceCharge(float dt)
    {
        if (drawing && dt > 0) Charge = Mathf.Clamp01(Charge + dt / CurrentChargeSeconds);
    }

    public void TakeDamage(float damage)
    {
        if (IsDead || DungeonStartScreen.BattleEnded || damage <= 0) return;
        HitsTaken = Mathf.Min(MaxHits, HitsTaken + 1);
        damageFlash = .3f;
        if (IsDead) { CancelDraw(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
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
        if(DungeonStartScreen.BattleEnded){CancelDraw();return;}
        var mouse = Mouse.current; var keys = Keyboard.current;
        if (mouse == null || keys == null) return;
        damageFlash = Mathf.Max(0, damageFlash - Time.deltaTime);
        healFlash = Mathf.Max(0, healFlash - Time.deltaTime);
        if (IsDead)return;
        if (keys.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true; CancelDraw();
        }
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (mouse.leftButton.wasPressedThisFrame) LockCursor();
            return;
        }
        Vector2 look = mouse.delta.ReadValue() * .09f;
        transform.Rotate(0, look.x, 0);
        pitch = Mathf.Clamp(pitch - look.y, -80, 80);
        view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        Vector2 input = new Vector2((keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0),
            (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0));
        input = Vector2.ClampMagnitude(input, 1);
        if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
        if (controller.isGrounded && keys.spaceKey.wasPressedThisFrame) verticalSpeed = 6;
        verticalSpeed -= 20 * Time.deltaTime;
        cooldown = Mathf.Max(0, cooldown - Time.deltaTime);
        if (keys.tabKey.wasPressedThisFrame)
        {
            ToggleAttackMode();
        }
        else if (mouse.rightButton.wasPressedThisFrame)
        {
            CancelDraw();
        }
        else
        {
            if (mouse.leftButton.wasPressedThisFrame) BeginDraw();
            AdvanceCharge(Time.deltaTime);
            if (drawing && mouse.leftButton.wasReleasedThisFrame) ReleaseArrow();
        }
        // Apply this frame's charge state so release/cancel restores speed immediately.
        controller.Move(((transform.right * input.x + transform.forward * input.y) * CurrentMoveSpeed + Vector3.up * verticalSpeed) * Time.deltaTime);
        UpdateBow();
    }

    public void ReleaseArrow()
    {
        if (IsDead || !drawing || cooldown > 0 || !DungeonStartScreen.BattleStarted || DungeonStartScreen.BattleEnded) return;
        float power = Charge;
        var projectile = new GameObject("Arrow");
        // Launch at the camera position so arrows cannot originate beyond a nearby wall.
        projectile.transform.SetPositionAndRotation(view.transform.position, view.transform.rotation);
        var arrow = projectile.AddComponent<DungeonArrow>();
        float damage = Mathf.Lerp(CurrentMaxDamage * .2f, CurrentMaxDamage, power);
        arrow.Initialize(view.transform.forward * Mathf.Lerp(24, 75, power), damage, controller, wood, metal);
        ShotsFired++; cooldown = CurrentReloadSeconds; CancelDraw();
    }

    void UpdateBow()
    {
        bow.localScale = Vector3.one * .72f;
        bow.localPosition = new Vector3(.43f, -.12f, .72f);
        bow.localRotation = Quaternion.Euler(0, 0, -8);
        cord.SetPosition(0, new Vector3(0, .65f, 0));
        cord.SetPosition(1, new Vector3(0, -.65f, 0));
        nockedArrow.localPosition = new Vector3(-.08f, 0, .3f);
        nockedArrow.gameObject.SetActive(cooldown <= 0);
    }

    public void BuildBow()
    {
        var existing = view.transform.Find("First Person Bow");
        if (existing != null) { bow = existing; cord = bow.Find("Bowstring").GetComponent<LineRenderer>(); nockedArrow = bow.Find("Nocked Arrow"); UpdateBow(); return; }
        bow = new GameObject("First Person Bow").transform; bow.SetParent(view.transform, false);
        var limbObject = new GameObject("Plain Wooden Bow"); limbObject.transform.SetParent(bow,false);
        var limb = limbObject.AddComponent<LineRenderer>();
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
        float x=Screen.width*.5f, y=Screen.height*.5f;
        GUI.color=new Color(1,.9f,.65f,.9f);
        GUI.DrawTexture(new Rect(x-7,y-1,14,2),Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x-1,y-7,2,14),Texture2D.whiteTexture);
        GUI.color=new Color(.08f,.09f,.12f,.8f);GUI.DrawTexture(new Rect(x-61,y+27,122,8),Texture2D.whiteTexture);
        Color modeColor=Mode==AttackMode.Heavy?new Color(1,.72f,.3f):new Color(.4f,.9f,1);
        GUI.color=modeColor;
        GUI.DrawTexture(new Rect(x-60,y+28,120*Charge,6),Texture2D.whiteTexture);
        GUI.color=Color.white;
        var modeStyle=new GUIStyle(GUI.skin.label){font=hudFont,fontSize=22,alignment=TextAnchor.MiddleRight};
        modeStyle.normal.textColor=modeColor;
        GUI.Label(new Rect(Screen.width-330,Screen.height-105,300,34),Mode==AttackMode.Heavy?"강공  ·  묵직한 한 발":"속공  ·  빠른 사격",modeStyle);
        var hintStyle=new GUIStyle(modeStyle){fontSize=15};
        hintStyle.normal.textColor=new Color(.8f,.83f,.85f);
        GUI.Label(new Rect(Screen.width-330,Screen.height-70,300,28),"TAB 전환   |   장전 "+CurrentChargeSeconds.ToString("0.00")+"초",hintStyle);
        if(Cursor.lockState!=CursorLockMode.Locked)
        {
            var pauseStyle=new GUIStyle(GUI.skin.label){font=hudFont,fontSize=28,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};
            GUI.Label(new Rect(x-200,y+48,400,44),"일시정지",pauseStyle);
            var resumeStyle=new GUIStyle(pauseStyle){fontSize=18,fontStyle=FontStyle.Normal};
            GUI.Label(new Rect(x-200,y+92,400,32),"클릭하면 계속합니다",resumeStyle);
        }
    }
}

