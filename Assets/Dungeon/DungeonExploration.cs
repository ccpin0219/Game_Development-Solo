using UnityEngine;
using UnityEngine.InputSystem;

// Scene-local exploration state. Legacy arena-only scenes remain playable without this component.
public class DungeonExploration : MonoBehaviour
{
    public static DungeonExploration Instance { get; private set; }
    public static bool ArenaReady => Instance == null || Instance.bossAwake;
    public Transform[] seals;
    public Transform gate;
    public Transform checkpoint;
    public Material activatedMaterial;
    public bool bossAwake;
    readonly bool[] activated = new bool[2];
    DungeonArcher player;
    Font font;
    int nearby = -1;
    bool rested;
    Vector3 gateRest;
    public int SealCount => (activated[0] ? 1 : 0) + (activated[1] ? 1 : 0);

    void Awake()
    {
        Instance = this;
        font = Resources.Load<Font>("Fonts/NeoDunggeunmo");
        if(gate != null)gateRest=gate.position;
    }
    void Start() { player=FindFirstObjectByType<DungeonArcher>(); }
    void OnDestroy() { if(Instance==this)Instance=null; }
    public bool ActivateSeal(int index)
    {
        if(index<0 || index>=activated.Length || activated[index])return false;
        activated[index]=true;
        if(seals!=null && index<seals.Length && seals[index]!=null)
            foreach(var r in seals[index].GetComponentsInChildren<Renderer>())
                if(r.name=="Seal Crystal")r.sharedMaterial=activatedMaterial;
        return true;
    }
    void Update()
    {
        if(player==null || !DungeonStartScreen.BattleStarted || DungeonStartScreen.BattleEnded || Cursor.lockState!=CursorLockMode.Locked)return;
        Simulate(Time.deltaTime);
    }
    public void Simulate(float dt)
    {
        if(dt<=0 || player==null || !DungeonStartScreen.BattleStarted || DungeonStartScreen.BattleEnded)return;
        if(SealCount==2 && gate!=null)gate.position=Vector3.MoveTowards(gate.position,gateRest+Vector3.up*9,dt*3);
        nearby=-1;
        for(int i=0;i<seals.Length;i++)
            if(!activated[i] && Vector3.Distance(player.transform.position,seals[i].position)<3.5f)nearby=i;
        if(nearby>=0 && Keyboard.current!=null && Keyboard.current.eKey.wasPressedThisFrame)ActivateSeal(nearby);
        if(!rested && checkpoint!=null && Vector3.Distance(player.transform.position,checkpoint.position)<4)
        { player.Heal(DungeonArcher.MaxHits); rested=true; }
        if(!bossAwake && SealCount==2 && player.transform.position.z>-50)bossAwake=true;
    }
    void OnGUI()
    {
        if(!DungeonStartScreen.BattleStarted || DungeonStartScreen.BattleEnded || bossAwake)return;
        var label=new GUIStyle(GUI.skin.label){font=font,fontSize=18,alignment=TextAnchor.UpperCenter};
        label.normal.textColor=new Color(.8f,.87f,.87f);
        string area=player==null?"":player.transform.position.z<-188?"성소의 입구":player.transform.position.x<-18?"잊힌 자의 납골당":player.transform.position.x>18?"푸른 봉인실":player.transform.position.z<-150?"순례자의 홀":player.transform.position.z<-100?"갈림 회랑":"수호자의 문";
        GUI.Label(new Rect(Screen.width*.5f-260,24,520,30),area,label);
        GUI.Label(new Rect(Screen.width*.5f-260,55,520,30),SealCount<2?"두 봉인을 해제하세요  "+SealCount+" / 2":"봉인이 풀렸습니다 · 전당으로 진입하세요",label);
        if(nearby>=0)GUI.Label(new Rect(Screen.width*.5f-220,Screen.height*.65f,440,35),"[E] 봉인 해제",label);
    }
}
