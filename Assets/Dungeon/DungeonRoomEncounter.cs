using UnityEngine;

// Room bounds and doors are authored in world space. Dormant enemies are inactive,
// so they cannot be seen, damaged or selected by homing arrows before entry.
public class DungeonRoomEncounter : MonoBehaviour
{
    public enum Phase { Dormant, Closing, Fighting, Opening, Cleared }
    public string roomName;
    public Bounds entryBounds;
    public DungeonSkeleton[] enemies;
    public DungeonBoss boss;
    public Transform[] doorLeaves;
    public Collider[] doorBlockers;
    public float doorTravel = 8.5f, doorSeconds = .65f;
    public Phase State { get; private set; }
    public int RemainingEnemies
    {
        get
        {
            int count = boss != null && !boss.Defeated ? 1 : 0;
            if (enemies != null) foreach (var enemy in enemies)
                if (enemy != null && enemy.Health > 0) count++;
            return count;
        }
    }
    DungeonArcher player;
    Vector3[] closedPositions;
    float progress, clearedNotice;
    Font font;

    void Awake()
    {
        player = FindFirstObjectByType<DungeonArcher>();
        font = Resources.Load<Font>("Fonts/NeoDunggeunmo");
        closedPositions = new Vector3[doorLeaves.Length];
        for (int i = 0; i < doorLeaves.Length; i++)
            closedPositions[i] = doorLeaves[i].localPosition - Vector3.up * doorTravel;
        SetBlockers(false);
        if (enemies != null) foreach (var enemy in enemies) if (enemy != null) enemy.gameObject.SetActive(false);
        if(boss==null)BuildEnemyWave();
        if (boss != null) boss.gameObject.SetActive(false);
    }

    void BuildEnemyWave()
    {
        if(enemies==null)return;
        var source=System.Array.Find(enemies,e=>e!=null && !e.ranged && !e.brawler);
        if(source==null)return;
        // Exactly one of each role, followed by three independent random choices.
        int[] roles={0,1,2,Random.Range(0,3),Random.Range(0,3),Random.Range(0,3)};
        for(int i=roles.Length-1;i>0;i--)
        {
            int other=Random.Range(0,i+1);
            int role=roles[i];roles[i]=roles[other];roles[other]=role;
        }
        var wave=new DungeonSkeleton[6];
        float spacingX=Mathf.Min(5,entryBounds.extents.x*.5f);
        float spacingZ=Mathf.Min(4,entryBounds.extents.z*.4f);
        for(int i=0;i<wave.Length;i++)
        {
            // The inactive template prevents Awake before role, health and home are configured.
            var enemy=Instantiate(source,transform);
            enemy.ranged=roles[i]==1;enemy.brawler=roles[i]==2;
            enemy.maxHealth=enemy.ranged?DungeonSkeleton.ArcherHealth:
                enemy.brawler?DungeonSkeleton.BrawlerHealth:DungeonSkeleton.AxeHealth;
            enemy.name=enemy.ranged?"Skeleton Archer":enemy.brawler?"Skeleton Brawler":"Axe Skeleton";
            enemy.transform.position=new Vector3(entryBounds.center.x+(i%3-1)*spacingX,
                source.transform.position.y,entryBounds.center.z+(i<3?-spacingZ:spacingZ));
            wave[i]=enemy;
        }
        foreach(var original in enemies)if(original!=null)Destroy(original.gameObject);
        enemies=wave;
    }

    void Update()
    {
        // Finish opening even when killing the boss has displayed the victory screen.
        if (State != Phase.Opening && !(State == Phase.Fighting && RemainingEnemies == 0))
            if (Cursor.lockState != CursorLockMode.Locked) return;
        Simulate(Time.deltaTime);
    }

    public void Simulate(float dt)
    {
        if (dt <= 0 || player == null || !DungeonStartScreen.BattleStarted || player.IsDead) return;
        if (State == Phase.Dormant)
        {
            if (DungeonStartScreen.BattleEnded || !entryBounds.Contains(player.transform.position)) return;
            if (boss != null && !DungeonExploration.ArenaReady) return;
            State = Phase.Closing;
            progress = 0;
            // Collision starts immediately after the entire player has crossed the threshold.
            SetBlockers(true);
        }
        if (State == Phase.Closing)
        {
            progress = Mathf.Clamp01(progress + dt / Mathf.Max(.01f, doorSeconds));
            PoseDoors(1 - Mathf.SmoothStep(0, 1, progress));
            if (progress < 1) return;
            if (enemies != null) foreach (var enemy in enemies)
                if (enemy != null) enemy.gameObject.SetActive(true);
            if (boss != null) boss.gameObject.SetActive(true);
            State = Phase.Fighting;
        }
        if (State == Phase.Fighting && RemainingEnemies == 0)
        { State = Phase.Opening; progress = 0; }
        if (State == Phase.Opening)
        {
            progress = Mathf.Clamp01(progress + dt / Mathf.Max(.01f, doorSeconds));
            PoseDoors(Mathf.SmoothStep(0, 1, progress));
            if (progress >= 1)
            { SetBlockers(false); State = Phase.Cleared; clearedNotice = 3; }
        }
        else if (State == Phase.Cleared) clearedNotice = Mathf.Max(0, clearedNotice - dt);
    }

    void SetBlockers(bool blocked)
    { foreach (var door in doorBlockers) if (door != null) door.enabled = blocked; }
    void PoseDoors(float open)
    {
        for (int i = 0; i < doorLeaves.Length; i++)
            if (doorLeaves[i] != null) doorLeaves[i].localPosition = closedPositions[i] + Vector3.up * (doorTravel * open);
    }
    void OnGUI()
    {
        if (!DungeonStartScreen.BattleStarted || DungeonStartScreen.BattleEnded || State == Phase.Dormant ||
            (State == Phase.Cleared && clearedNotice <= 0)) return;
        var style = new GUIStyle(GUI.skin.label) { font = font, fontSize = 20, alignment = TextAnchor.UpperCenter };
        style.normal.textColor = State == Phase.Cleared ? new Color(.55f, 1, .8f) : new Color(1, .65f, .36f);
        string message = State == Phase.Closing ? "출입구 봉쇄 · 전투 준비" :
            State == Phase.Fighting ? "남은 적  " + RemainingEnemies : "전투 완료 · 출입구 개방";
        GUI.Label(new Rect(Screen.width * .5f - 240, 90, 480, 32), message, style);
    }
    void OnDrawGizmosSelected()
    { Gizmos.color = Color.cyan; Gizmos.DrawWireCube(entryBounds.center, entryBounds.size); }
}
