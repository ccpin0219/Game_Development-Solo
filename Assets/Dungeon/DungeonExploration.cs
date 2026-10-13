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
    public DungeonRoomEncounter[] keyRooms;
    public Transform[] keySpawns;
    readonly int[] selectedRooms = { -1, -1 };
    static int previousPair = -1;
    bool runInitialized;
    public bool UsesRoomKeys => keyRooms != null && keyRooms.Length == 8 && keySpawns != null && keySpawns.Length == 8;
    public int FirstKeyRoom => selectedRooms[0];
    public int SecondKeyRoom => selectedRooms[1];
    public bool bossAwake;
    readonly bool[] activated = new bool[2];
    DungeonArcher player;
    Font font;
    DungeonRoomMap roomMap;
    int nearby = -1;
    bool rested;
    float explorationSeconds;
    bool runCompleted;
    public float RunTime => explorationSeconds+DungeonStartScreen.RevivalPenalty;
    public void CompleteRun(){runCompleted=true;}
    Vector3 gateRest;
    public int SealCount => (activated[0] ? 1 : 0) + (activated[1] ? 1 : 0);

    void Awake()
    {
        Instance = this;
        font = Resources.Load<Font>("Fonts/NeoDunggeunmo");
        if(gate != null)gateRest=gate.position;
    }
    void Start()
    {
        player=FindFirstObjectByType<DungeonArcher>();
        if(UsesRoomKeys && player!=null)DungeonPracticeTarget.Create(transform,player);
    }
    void OnDestroy() { if(Instance==this)Instance=null; }
    public bool ActivateSeal(int index)
    {
        if(UsesRoomKeys)return false;
        if(index<0 || index>=activated.Length || activated[index])return false;
        activated[index]=true;
        if(seals!=null && index<seals.Length && seals[index]!=null)
            foreach(var r in seals[index].GetComponentsInChildren<Renderer>())
                if(r.name=="Seal Crystal")r.sharedMaterial=activatedMaterial;
        return true;
    }
    public void BeginRun()
    {
        if(runInitialized || !UsesRoomKeys)return;
        runInitialized=true;
        // Choose uniformly among distinct room pairs, excluding the previous run's pair.
        int choice=Random.Range(0,previousPair<0?28:27);
        if(previousPair>=0 && choice>=previousPair)choice++;
        int pair=0;
        for(int a=0;a<7;a++)for(int b=a+1;b<8;b++,pair++)
            if(pair==choice){selectedRooms[0]=a;selectedRooms[1]=b;}
        previousPair=choice;
        foreach(var spawn in keySpawns)if(spawn!=null)spawn.gameObject.SetActive(false);
    }
    public bool CollectKey(int slot)
    {
        if(!runInitialized || !UsesRoomKeys || player==null || slot<0 || slot>1 || activated[slot] ||
            !DungeonStartScreen.BattleStarted || DungeonStartScreen.BattleEnded)return false;
        int room=selectedRooms[slot];
        if(room<0 || keyRooms[room]==null || keyRooms[room].State!=DungeonRoomEncounter.Phase.Cleared ||
            keySpawns[room]==null || Vector3.Distance(player.transform.position,keySpawns[room].position)>3.5f)return false;
        activated[slot]=true;keySpawns[room].gameObject.SetActive(false);return true;
    }
    void Update()
    {
        if(player==null || !DungeonStartScreen.BattleStarted || DungeonStartScreen.BattleEnded || Cursor.lockState!=CursorLockMode.Locked)return;
        Simulate(Time.deltaTime);
    }
    public void Simulate(float dt)
    {
        if(dt<=0 || player==null || player.IsDead || DungeonStartScreen.IsPaused || !DungeonStartScreen.BattleStarted || DungeonStartScreen.BattleEnded)return;
        if(!runCompleted)explorationSeconds+=dt;
        if(SealCount==2 && gate!=null)gate.position=Vector3.MoveTowards(gate.position,gateRest+Vector3.up*9,dt*3);
        nearby=-1;
        if(UsesRoomKeys)
        {
            if(!runInitialized)BeginRun();
            for(int slot=0;slot<2;slot++)
            {
                int room=selectedRooms[slot];
                if(activated[slot] || room<0 || keySpawns[room]==null)continue;
                bool cleared=keyRooms[room]!=null && keyRooms[room].State==DungeonRoomEncounter.Phase.Cleared;
                keySpawns[room].gameObject.SetActive(cleared);
                if(cleared){keySpawns[room].Rotate(0,55*dt,0);if(Vector3.Distance(player.transform.position,keySpawns[room].position)<3.5f)nearby=slot;}
            }
            if(nearby>=0 && Keyboard.current!=null && Keyboard.current.eKey.wasPressedThisFrame)CollectKey(nearby);
        }
        else
        {
            for(int i=0;i<seals.Length;i++)
                if(!activated[i] && Vector3.Distance(player.transform.position,seals[i].position)<3.5f)nearby=i;
            if(nearby>=0 && Keyboard.current!=null && Keyboard.current.eKey.wasPressedThisFrame)ActivateSeal(nearby);
        }
        if(!rested && checkpoint!=null && Vector3.Distance(player.transform.position,checkpoint.position)<4)
        { player.Heal(DungeonArcher.MaxHits); rested=true; }
        if(!bossAwake && SealCount==2 && player.transform.position.z>-50)bossAwake=true;
    }
    void OnGUI()
    {
        if(!DungeonStartScreen.BattleStarted || DungeonStartScreen.BattleEnded)return;
        if(UsesRoomKeys && player!=null)
        {
            if(roomMap==null)roomMap=new DungeonRoomMap(keyRooms);
            if(!DungeonStartScreen.IsPaused && !player.IsDead)roomMap.Draw(player.transform.position,RunTime);
        }
        if(bossAwake)return;
        if(UsesRoomKeys && player!=null && DungeonRoomMap.RoomAt(player.transform.position)==9)return;
        var label=new GUIStyle(GUI.skin.label){font=font,fontSize=18,alignment=TextAnchor.UpperCenter};
        label.normal.textColor=new Color(.8f,.87f,.87f);
        GUI.Label(new Rect(Screen.width*.5f-260,55,520,30),SealCount<2?(UsesRoomKeys?"보스 문 열쇠  ":"두 봉인을 해제하세요  ")+SealCount+" / 2":"문이 열렸습니다 · 전당으로 진입하세요",label);
        if(nearby>=0)GUI.Label(new Rect(Screen.width*.5f-220,Screen.height*.65f,440,35),UsesRoomKeys?"[E] 보스 문 열쇠 획득":"[E] 봉인 해제",label);
    }
}
