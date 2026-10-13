using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// A reusable, indestructible practice target. It is deliberately not an encounter enemy.
public sealed class DungeonPracticeTarget : MonoBehaviour, IDungeonDamageable
{
    DungeonArcher player;
    Renderer bullseye;
    bool controlsSensitivity;
    GUIStyle sensitivityLabel, stepLabel;
    MaterialPropertyBlock flashProperties;
    float flashRemaining;
    readonly List<Material> ownedMaterials = new List<Material>();

    public static void Create(Transform parent, DungeonArcher archer)
    {
        if(parent.Find("Starting Room Practice Target")!=null)return;
        CreateTarget(parent,archer,"Starting Room Practice Target",-189,true);
        CreateTarget(parent,archer,"Starting Room Practice Target 2",-198,false);
    }

    static void CreateTarget(Transform parent,DungeonArcher archer,string name,float z,bool controls)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        // Back of the disc sits against the east wall's dressed stone, facing into the room.
        obj.transform.SetPositionAndRotation(new Vector3(15.43f, 2.2f, z),Quaternion.LookRotation(Vector3.left));
        var target = obj.AddComponent<DungeonPracticeTarget>();
        target.player = archer;
        target.controlsSensitivity=controls;
        target.flashProperties = new MaterialPropertyBlock();
        target.Build();
    }

    bool CanAdjust => controlsSensitivity && player!=null && DungeonStartScreen.BattleStarted && !DungeonStartScreen.BattleEnded &&
        !DungeonStartScreen.IsPaused && !player.IsDead && Cursor.lockState==CursorLockMode.Locked &&
        DungeonRoomMap.RoomAt(player.transform.position)==9;

    void Update()
    {
        if(CanAdjust && Keyboard.current!=null)
        {
            var keys = Keyboard.current;
            // The regular keyboard's + shares the equals key; also accept numpad +/-.
            int change = (keys.equalsKey.wasPressedThisFrame || keys.numpadPlusKey.wasPressedThisFrame ? 1 : 0)
                - (keys.minusKey.wasPressedThisFrame || keys.numpadMinusKey.wasPressedThisFrame ? 1 : 0);
            if(change!=0)player.SetLookSensitivityLevel(player.LookSensitivityLevel+change);
        }
        if(flashRemaining>0 && !DungeonStartScreen.IsPaused)
        {
            flashRemaining=Mathf.Max(0,flashRemaining-Time.deltaTime);
            if(flashRemaining<=0 && bullseye!=null)bullseye.SetPropertyBlock(null);
        }
    }

    public void TakeDamage(float damage)
    {
        if(damage<=0 || player==null || player.IsDead || !DungeonStartScreen.BattleStarted || DungeonStartScreen.BattleEnded)return;
        player.ConfirmArrowHit();
        flashRemaining=.12f;
        flashProperties.SetColor("_BaseColor",new Color(1,1,.7f));
        if(bullseye!=null)bullseye.SetPropertyBlock(flashProperties);
    }

    void Build()
    {
        var wood=Material("Target wood",new Color(.26f,.13f,.06f));
        var cream=Material("Target ivory",new Color(.88f,.81f,.61f));
        var red=Material("Target red",new Color(.68f,.055f,.035f));
        var gold=Material("Target bullseye",new Color(1,.6f,.08f));

        Disc("Wooden target backing",.98f,0,.09f,wood);
        Disc("Outer ivory ring",.88f,.102f,.009f,cream);
        Disc("Red ring",.67f,.123f,.009f,red);
        Disc("Inner ivory ring",.46f,.144f,.009f,cream);
        Disc("Inner red ring",.25f,.165f,.009f,red);
        bullseye=Disc("Bullseye",.10f,.186f,.009f,gold);
        // Use the cylindrical backing itself for collision, so shots outside the disc miss.
        var backing=transform.Find("Wooden target backing");
        backing.gameObject.AddComponent<MeshCollider>().sharedMesh=backing.GetComponent<MeshFilter>().sharedMesh;
        var lightObject=new GameObject("Practice target light");lightObject.transform.SetParent(transform,false);
        lightObject.transform.localPosition=new Vector3(0,1.2f,1.2f);
        var light=lightObject.AddComponent<Light>();light.color=new Color(1,.8f,.5f);light.range=4;light.intensity=2;
        light.shadows=LightShadows.None;
    }

    Material Material(string name,Color color)
    {
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.name=name;material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.15f);
        ownedMaterials.Add(material);return material;
    }
    Renderer Disc(string name,float radius,float z,float thickness,Material material)
    {
        var obj=new GameObject(name);obj.transform.SetParent(transform,false);
        obj.transform.localPosition=new Vector3(0,0,z);obj.transform.localRotation=Quaternion.Euler(90,0,0);
        obj.transform.localScale=new Vector3(radius*2,thickness,radius*2);
        obj.AddComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;return renderer;
    }
    void OnGUI()
    {
        if(!CanAdjust)return;
        if(sensitivityLabel==null)
        {
            var font=Resources.Load<Font>("Fonts/NeoDunggeunmo");
            sensitivityLabel=new GUIStyle(GUI.skin.label){font=font,fontSize=18,alignment=TextAnchor.MiddleCenter};
            stepLabel=new GUIStyle(sensitivityLabel){fontSize=14};
        }
        var oldColor=GUI.color;var oldMatrix=GUI.matrix;
        try
        {
            float scale=Mathf.Min(1,Screen.width/640f,Screen.height/360f);
            GUI.matrix=Matrix4x4.TRS(new Vector3(Screen.width*.5f,Screen.height-20*scale,0),Quaternion.identity,Vector3.one*scale);
            GUI.color=new Color(.025f,.04f,.055f,.85f);
            GUI.DrawTexture(new Rect(-190,-88,380,88),Texture2D.whiteTexture);
            GUI.color=Color.white;
            GUI.Label(new Rect(-190,-86,380,30),"[-]  감도 "+player.LookSensitivityLevel+" / 10  [+]",sensitivityLabel);
            for(int i=1;i<=10;i++)
            {
                float x=-160+(i-1)*32;
                GUI.color=i<=player.LookSensitivityLevel?new Color(1,.73f,.31f):new Color(.25f,.30f,.34f);
                GUI.DrawTexture(new Rect(x,-49,26,6),Texture2D.whiteTexture);
                GUI.color=i==player.LookSensitivityLevel?new Color(1,.8f,.4f):new Color(.75f,.8f,.83f);
                GUI.Label(new Rect(x,-39,26,24),i.ToString(),stepLabel);
            }
        }
        finally{GUI.color=oldColor;GUI.matrix=oldMatrix;}
    }
    void OnDestroy()
    {

        foreach(var material in ownedMaterials)if(material!=null)Destroy(material);
    }
}


