using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-1000)]
public class DungeonStartScreen : MonoBehaviour
{
    public static bool BattleStarted { get; private set; }
    public static bool BattleEnded { get; private set; }
    public static bool IsPaused { get; private set; }
    public static float RevivalPenalty { get; private set; }
    public static int DeathCount { get; private set; }
    DungeonLeaderboard leaderboard;
    string rankingName="", rankingMessage="";
    bool scoreSubmitted;
    Vector2 rankingScroll;
    bool restarting;
    DungeonArcher player;
    Camera gameplayCamera, menuCamera;
    Font menuFont;
    bool cameraWasEnabled, showRanking;
    DungeonBoss boss;
    Texture2D menuBackground;
    Font titleFont;
    bool ownsFont;
    int selected;
    int guidePage=-1, guideInputFrame=-1;
    GUIStyle titleStyle, subtitleStyle, bodyStyle, menuButtonStyle, smallStyle;
    static readonly Color Gold = new Color(.83f, .67f, .43f);

    void Awake()
    {
        BattleStarted=false;
        BattleEnded=false;
        IsPaused=false;
        RevivalPenalty=0;
        DeathCount=0;
        leaderboard=new DungeonLeaderboard();
        Time.timeScale=1f;
        menuFont=Resources.Load<Font>("Fonts/NeoDunggeunmo");
        if(menuFont==null){menuFont=Font.CreateDynamicFontFromOSFont("Malgun Gothic",16);ownsFont=true;}
        titleFont=menuFont;
        menuBackground=Resources.Load<Texture2D>("Menu/AshenSanctumPixel");
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
    }
    void Start()
    {
        player=FindFirstObjectByType<DungeonArcher>();
        boss=FindFirstObjectByType<DungeonBoss>(FindObjectsInactive.Include);
        gameplayCamera=player.view;cameraWasEnabled=gameplayCamera.enabled;
        gameplayCamera.enabled=false;
        var cameraObject=new GameObject("Menu Dungeon Camera");
        menuCamera=cameraObject.AddComponent<Camera>();
        menuCamera.CopyFrom(gameplayCamera);menuCamera.enabled=true;
        menuCamera.fieldOfView=55;
        menuCamera.transform.position=new Vector3(0,11,-38);
        menuCamera.transform.LookAt(new Vector3(0,7,0));
    }
    void Update()
    {
        if(!BattleStarted)
        {
            var keys=Keyboard.current;
            if(keys==null)return;
            if(guidePage>=0)
            {
                if(keys.enterKey.wasPressedThisFrame)AdvanceGuide();
                else if(keys.escapeKey.wasPressedThisFrame)guidePage--;
                return;
            }
            if(showRanking){if(keys.escapeKey.wasPressedThisFrame)showRanking=false;return;}
            if(keys.downArrowKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame)selected=(selected+1)%3;
            if(keys.upArrowKey.wasPressedThisFrame || keys.wKey.wasPressedThisFrame)selected=(selected+2)%3;
            if(keys.enterKey.wasPressedThisFrame)
            {
                if(selected==0)StartBattle();else if(selected==1)ToggleRanking();else QuitGame();
            }
            return;
        }
        if(!BattleEnded && ((player!=null && player.IsDead) || (boss!=null && boss.gameObject.activeInHierarchy && boss.Defeated)))
        {
            BattleEnded=true;
            IsPaused=false;
            if(player!=null && player.IsDead){DeathCount++;Time.timeScale=0f;}
        }
        if(!BattleEnded)
        {
            if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if(IsPaused)ResumeBattle();else PauseBattle();
            }
            return;
        }
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
    }
    public void RestartBattle()
    {
        if((!BattleEnded && !IsPaused) || restarting)return;
        restarting=true;
        Time.timeScale=1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().path);
    }
    public void ToggleRanking() { showRanking=!showRanking; }
    public bool SubmitRanking(string name)
    {
        if(!BattleEnded || boss==null || !boss.ClearRecorded || scoreSubmitted || restarting)return false;
        if(!leaderboard.Add(name,boss.BattleTime,DeathCount))
        { rankingMessage="이름을 입력해 주세요.";return false; }
        scoreSubmitted=true;
        rankingMessage="기록이 랭킹에 등록되었습니다.";
        return true;
    }
    public void PauseBattle()
    {
        if(!BattleStarted || BattleEnded || IsPaused || restarting)return;
        IsPaused=true;
        Time.timeScale=0f;
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
    }
    public void ResumeBattle()
    {
        if(!IsPaused || BattleEnded || restarting)return;
        IsPaused=false;
        Time.timeScale=1f;
        Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
    }
    public bool ReviveBattle()
    {
        if(!BattleEnded || restarting || player==null || !player.IsDead || (boss!=null && boss.ClearRecorded))return false;
        if(!player.Revive())return false;
        RevivalPenalty+=90f;
        BattleEnded=false;
        Time.timeScale=1f;
        Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
        return true;
    }
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying=false;
#else
        Application.Quit();
#endif
    }
    public void StartBattle()
    {
        if(BattleStarted)return;
        guidePage=0;
        guideInputFrame=Time.frameCount;
        showRanking=false;
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
    }
    void AdvanceGuide()
    {
        if(guidePage<0 || guideInputFrame==Time.frameCount)return;
        guideInputFrame=Time.frameCount;
        if(guidePage==0){guidePage=1;return;}
        guidePage=-1;
        BeginBattle();
    }
    void BeginBattle()
    {
        if(BattleStarted)return;
        BattleStarted=true;
        if(DungeonExploration.Instance!=null)DungeonExploration.Instance.BeginRun();
        if(menuCamera!=null){menuCamera.enabled=false;Destroy(menuCamera.gameObject);}
        if(gameplayCamera!=null)gameplayCamera.enabled=cameraWasEnabled;
        Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
    }
    void OnDestroy()
    {
        if(menuCamera!=null)Destroy(menuCamera.gameObject);
        if(ownsFont && menuFont!=null)Destroy(menuFont);
    }
    void OnGUI()
    {
        if(BattleStarted){if(BattleEnded)DrawResult();else if(IsPaused)DrawPause();return;}
        GUI.depth=-100;
        var oldMatrix=GUI.matrix;
        var oldColor=GUI.color;
        GUI.color=Color.white;
        if(menuBackground!=null)
            GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),menuBackground,ScaleMode.ScaleAndCrop);
        Fill(new Rect(0,0,Screen.width,Screen.height),new Color(0,0,0,.16f));
        float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
        GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)*.5f,(Screen.height-720*scale)*.5f,0),Quaternion.identity,new Vector3(scale,scale,1));
        EnsureStyles();
        // Feather the reading space into the artwork instead of boxing in the whole menu.
        for(int i=0;i<40;i++)
            Fill(new Rect(i*18,0,18,720),new Color(.015f,.025f,.04f,.72f*(1-Mathf.SmoothStep(0,1,i/39f))));
        GUI.Label(new Rect(98,112,340,24),"A S H E N   S A N C T U M",subtitleStyle);
        var titleRect=new Rect(92,150,500,108);
        GUI.color=new Color(.08f,.13f,.16f);
        GUI.Label(new Rect(titleRect.x+4,titleRect.y+4,titleRect.width,titleRect.height),"잿빛 성소",titleStyle);
        GUI.color=Color.white;
        GUI.Label(titleRect,"잿빛 성소",titleStyle);
        bool previousEnabled=GUI.enabled;
        GUI.enabled=!showRanking && guidePage<0;
        if(MenuButton(new Rect(96,374,340,52),"탐험 시작",selected==0,0))StartBattle();
        if(MenuButton(new Rect(96,438,340,52),"랭킹",selected==1,1))ToggleRanking();
        if(MenuButton(new Rect(96,502,340,52),"나가기",selected==2,2))QuitGame();
        GUI.enabled=previousEnabled;
        GUI.Label(new Rect(98,605,400,28),"BEST  /  "+(boss==null || boss.BestClearTime<0?"--:--.--":DungeonBoss.FormatTime(boss.BestClearTime)),smallStyle);
        // Lightweight drifting embers; no new scene objects or particle materials.
        for(int i=0;i<18;i++)
        {
            float t=Time.unscaledTime;
            float x=570+(i*139%660)+Mathf.Sin(t*.35f+i)*12;
            float y=710-Mathf.Repeat(i*47+t*(9+i%4*3),650);
            float alpha=(.18f+.25f*Mathf.Sin(i+t*.8f))*Mathf.Clamp01(y/100);
            Fill(new Rect(Mathf.Floor(x/4)*4,Mathf.Floor(y/4)*4,4,4),new Color(1,.52f,.2f,Mathf.Max(0,alpha)));
        }
        if(showRanking)DrawRanking();
        if(guidePage>=0)DrawGuide();
        GUI.matrix=oldMatrix;GUI.color=oldColor;
    }
    GUIStyle GuideStyle(int size,Color color,TextAnchor anchor=TextAnchor.UpperLeft)
    {
        var style=new GUIStyle(bodyStyle){fontSize=size,wordWrap=true,alignment=anchor};
        style.normal.textColor=color;return style;
    }
    void DrawGuide()
    {
        Fill(new Rect(0,0,1280,720),new Color(0,0,0,.82f));
        Fill(new Rect(110,62,1060,596),new Color(.035f,.055f,.07f,1));
        GUI.Label(new Rect(150,91,700,48),guidePage==0?"조작법과 스킬":"탐험의 목표",GuideStyle(32,Color.white));
        GUI.Label(new Rect(150,145,900,30),guidePage==0?"키를 익히고, 상황에 맞는 공격을 선택하세요.":"방을 돌파하고 열쇠를 모아, 보스를 처치하세요.",bodyStyle);
        for(int i=0;i<2;i++)Fill(new Rect(1040+i*42,111,32,5),i==guidePage?Gold:new Color(.2f,.28f,.3f));
        if(guidePage==0)
        {
            Fill(new Rect(150,194,456,364),new Color(.055f,.08f,.10f));
            ControlRow(207,"W A S D","이동",true);
            ControlRow(249,"마우스","움직여서 조준");
            ControlRow(291,"좌클릭","유지: 충전  /  놓기: 발사");
            ControlRow(333,"우클릭","충전 취소");
            ControlRow(375,"SPACE","점프");
            ControlRow(417,"TAB","강공 / 속공 전환");
            SkillCard(194,0,"강공", "완충하면 강력한 직선 화살\n정확한 조준에 유리",new Color(1,.35f,.27f));
            SkillCard(286,1,"속공", "완충 후 충돌하면 3발로 분열\n작은 화살이 적을 추적",new Color(.32f,.72f,1));
            SkillCard(378,2,"SHIFT · 돌진", "WASD 방향으로 빠르게 이동\n재사용 대기 15초",Gold);
            SkillCard(470,3,"Q · 자동 충전", "5초간 빠른 자동 충전 · 클릭 발사\n재사용 대기 30초",new Color(.45f,.86f,.68f));
        }
        else
        {
            string[] titles={"방 돌파","열쇠 2개","보스 처치"};
            string[] descriptions={"입장하면 문이 닫힙니다\n적을 모두 잡으면 개방","던전 곳곳을 탐색해\n열쇠 2개를 모으세요","열쇠 2개로 문 개방\n수호자를 처치하면 승리"};
            for(int i=0;i<3;i++)
            {
                float x=150+i*335;
                Fill(new Rect(x,207,310,307),new Color(.055f,.08f,.10f));
                GUI.Label(new Rect(x+18,224,80,28),"0"+(i+1),GuideStyle(18,Gold));
                ObjectiveIcon(new Vector2(x+155,314),i+1);
                GUI.Label(new Rect(x+10,380,290,34),titles[i],GuideStyle(24,Color.white,TextAnchor.MiddleCenter));
                GUI.Label(new Rect(x+10,434,290,68),descriptions[i],GuideStyle(16,new Color(.77f,.83f,.85f),TextAnchor.UpperCenter));
                if(i<2)GuideArrow(new Vector2(x+314,346),new Vector2(x+331,346),Gold,2);
            }
            GUI.Label(new Rect(150,534,980,28),"오른쪽 위 지도에서 현재 있는 방을 확인할 수 있습니다.",GuideStyle(17,Gold,TextAnchor.MiddleCenter));
        }
        Fill(new Rect(150,576,980,1),new Color(.3f,.36f,.37f));
        int page=guidePage;
        if(MenuButton(new Rect(150,590,270,46),page==0?"돌아가기":"이전",false))guidePage--;
        GUI.Label(new Rect(475,603,290,24),"ENTER 다음  ·  ESC 이전",smallStyle);
        if(MenuButton(new Rect(835,590,295,46),page==0?"다음  →":"탐험 시작",true))AdvanceGuide();
    }
    void ControlRow(float y,string key,string action,bool movement=false)
    {
        if(movement)
            for(int i=0;i<4;i++)KeyCap(new Rect(165+i*35,y,30,30),"WASD"[i].ToString());
        else KeyCap(new Rect(165,y,136,30),key);
        GUI.Label(new Rect(319,y,278,32),action,GuideStyle(18,new Color(.87f,.9f,.9f),TextAnchor.MiddleLeft));
    }
    void KeyCap(Rect rect,string key)
    {
        Fill(new Rect(rect.x,rect.y+3,rect.width,rect.height),new Color(.015f,.025f,.035f));
        Fill(rect,new Color(.17f,.23f,.27f));
        Fill(new Rect(rect.x+2,rect.y,rect.width-4,2),new Color(.35f,.44f,.47f));
        GUI.Label(rect,key,GuideStyle(16,Color.white,TextAnchor.MiddleCenter));
    }
    void SkillCard(float y,int kind,string title,string description,Color accent)
    {
        Fill(new Rect(626,y,504,88),new Color(.055f,.08f,.10f));
        Fill(new Rect(626,y,3,88),accent);
        Vector2 c=new Vector2(679,y+44);
        if(kind==0)
        {
            GuideLine(c+new Vector2(-32,8),c+new Vector2(12,8),new Color(accent.r,accent.g,accent.b,.3f),6);
            GuideArrow(c+new Vector2(-30,0),c+new Vector2(31,0),accent,4);
        }
        else if(kind==1)
        {
            GuideLine(c+new Vector2(-32,0),c+new Vector2(-8,0),accent,3);
            for(int i=-1;i<=1;i++)GuideArrow(c+new Vector2(-8,0),c+new Vector2(30,i*22),accent,2);
        }
        else if(kind==2)
        {
            for(int i=0;i<3;i++)GuideLine(c+new Vector2(-34,i*12-12),c+new Vector2(-14,i*12-12),accent,2);
            GuideArrow(c+new Vector2(-5,0),c+new Vector2(30,0),accent,5);
        }
        else
        {
            GuideRing(c,24,accent);
            GuideLine(c+new Vector2(4,-14),c+new Vector2(-6,2),accent,4);
            GuideLine(c+new Vector2(-6,2),c+new Vector2(7,2),accent,4);
            GuideLine(c+new Vector2(7,2),c+new Vector2(-3,16),accent,4);
        }
        GUI.Label(new Rect(733,y+9,382,28),title,GuideStyle(21,accent));
        GUI.Label(new Rect(733,y+38,382,46),description,GuideStyle(17,new Color(.85f,.89f,.9f)));
    }
    void ObjectiveIcon(Vector2 c,int kind)
    {
        if(kind==0)
        {
            GuideRing(c,40,Gold);GuideRing(c,25,new Color(.85f,.35f,.28f));GuideRing(c,10,Gold);
            GuideArrow(c+new Vector2(-52,26),c,Color.white,3);
        }
        else if(kind==1)
        {
            for(int row=0;row<2;row++)for(int col=0;col<2;col++)
                Fill(new Rect(c.x-44+col*48,c.y-40+row*44,38,34),row==1&&col==1?Gold:new Color(.28f,.4f,.46f));
            GuideArrow(c+new Vector2(-25,-23),c+new Vector2(23,21),Color.white,3);
        }
        else if(kind==2)
        {
            for(int i=0;i<2;i++)
            {
                Vector2 p=c+new Vector2(-25+i*50,-22);
                GuideRing(p,15,Gold);GuideLine(p+Vector2.up*15,p+Vector2.up*58,Gold,5);
                GuideLine(p+new Vector2(0,44),p+new Vector2(12,44),Gold,5);
                GuideLine(p+new Vector2(0,56),p+new Vector2(12,56),Gold,5);
            }
        }
        else
        {
            Fill(new Rect(c.x-32,c.y-24,64,60),new Color(.33f,.4f,.43f));
            GuideLine(c+new Vector2(-30,-22),c+new Vector2(-44,-42),Gold,7);
            GuideLine(c+new Vector2(30,-22),c+new Vector2(44,-42),Gold,7);
            Fill(new Rect(c.x-21,c.y-7,14,6),new Color(1,.35f,.2f));
            Fill(new Rect(c.x+7,c.y-7,14,6),new Color(1,.35f,.2f));
            GuideLine(c+new Vector2(0,8),c+new Vector2(0,25),Gold,4);
        }
    }
    static void GuideLine(Vector2 a,Vector2 b,Color color,float width)
    {
        var matrix=GUI.matrix;
        try
        {
            // Compose in the guide's logical coordinates; RotateAroundPivot mixes these
            // with screen coordinates when the menu is scaled or letterboxed.
            GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(a.x,a.y,0),
                Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg),Vector3.one);
            Fill(new Rect(0,-width*.5f,Vector2.Distance(a,b),width),color);
        }
        finally{GUI.matrix=matrix;}
    }
    static void GuideArrow(Vector2 a,Vector2 b,Color color,float width)
    {
        GuideLine(a,b,color,width);
        Vector2 direction=(b-a).normalized,side=new Vector2(-direction.y,direction.x);
        GuideLine(b,b-direction*8+side*6,color,width);
        GuideLine(b,b-direction*8-side*6,color,width);
    }
    static void GuideRing(Vector2 center,float radius,Color color)
    {
        for(int i=0;i<24;i++)
        {
            float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;
            GuideLine(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,color,3);
        }
    }
    void EnsureStyles()
    {
        if(titleStyle!=null)return;
        titleStyle=new GUIStyle(GUI.skin.label){font=titleFont,fontSize=80,fontStyle=FontStyle.Normal};
        titleStyle.normal.textColor=new Color(.9f,.9f,.82f);
        subtitleStyle=new GUIStyle(GUI.skin.label){font=menuFont,fontSize=16};
        subtitleStyle.normal.textColor=Gold;
        bodyStyle=new GUIStyle(GUI.skin.label){font=menuFont,fontSize=16};
        bodyStyle.normal.textColor=new Color(.77f,.79f,.77f);
        smallStyle=new GUIStyle(bodyStyle){fontSize=16};
        smallStyle.normal.textColor=new Color(.49f,.59f,.61f);
        menuButtonStyle=new GUIStyle(bodyStyle){fontSize=32,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(34,0,0,0)};
    }
    static void Fill(Rect rect,Color color)
    {
        var previous=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=previous;
    }
    bool MenuButton(Rect rect,string label,bool primary,int index=-1)
    {
        bool hover=GUI.enabled && rect.Contains(Event.current.mousePosition);
        if(hover && index>=0 && (Event.current.type==EventType.MouseMove || Event.current.type==EventType.MouseDown))selected=index;
        bool active=hover||primary;
        if(primary || hover)
        {
            float cy=Mathf.Floor(rect.center.y/4)*4;
            Fill(new Rect(rect.x,cy-4,4,8),Gold);
            Fill(new Rect(rect.x+4,cy-8,4,16),Gold);
            Fill(new Rect(rect.x+8,cy-4,4,8),Gold);
        }
        menuButtonStyle.normal.textColor=active?new Color(.94f,.81f,.54f):new Color(.56f,.65f,.67f);
        GUI.Label(rect,label,menuButtonStyle);
        return GUI.Button(rect,GUIContent.none,GUIStyle.none);
    }
    void DrawRanking()
    {
        Fill(new Rect(0,0,1280,720),new Color(0,0,0,.78f));
        Fill(new Rect(260,80,760,560),new Color(.045f,.07f,.085f,1));
        Fill(new Rect(300,112,32,2),Gold);
        GUI.Label(new Rect(300,130,400,46),"성소의 기록 · 랭킹",new GUIStyle(bodyStyle){fontSize=32});
        GUI.Label(new Rect(300,183,680,28),"클리어 시간순 · 부활 페널티 포함 · 이 PC에 저장",bodyStyle);
        GUI.Label(new Rect(308,228,70,30),"순위",bodyStyle);
        GUI.Label(new Rect(400,228,260,30),"이름",bodyStyle);
        GUI.Label(new Rect(684,228,165,30),"클리어 타임",bodyStyle);
        GUI.Label(new Rect(860,228,110,30),"사망 횟수",bodyStyle);
        Fill(new Rect(300,265,680,1),Gold);
        var entries=leaderboard.Entries;
        var rowStyle=new GUIStyle(bodyStyle){richText=false};
        rankingScroll=GUI.BeginScrollView(new Rect(300,278,680,270),rankingScroll,new Rect(0,0,650,Mathf.Max(270,entries.Count*40)));
        if(entries.Count==0)GUI.Label(new Rect(8,65,640,40),"아직 기록이 없습니다. 첫 번째 클리어를 남겨보세요.",rowStyle);
        for(int i=0;i<entries.Count;i++)
        {
            var entry=entries[i];float y=i*40;
            if(i%2==0)Fill(new Rect(0,y,650,38),new Color(1,1,1,.035f));
            GUI.Label(new Rect(8,y+6,70,28),leaderboard.RankAt(i).ToString(),rowStyle);
            GUI.Label(new Rect(100,y+6,275,28),entry.playerName,rowStyle);
            GUI.Label(new Rect(384,y+6,165,28),DungeonBoss.FormatTime(entry.seconds),rowStyle);
            GUI.Label(new Rect(560,y+6,85,28),entry.deaths+"회",rowStyle);
        }
        GUI.EndScrollView();
        if(MenuButton(new Rect(300,572,350,44),"돌아가기",true))showRanking=false;
    }
    void DrawPause()
    {
        GUI.depth=-100;
        var oldMatrix=GUI.matrix;
        var oldColor=GUI.color;
        GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1280f,Screen.height/720f,1));
        GUI.color=Color.white;
        Fill(new Rect(0,0,1280,720),new Color(0,0,0,.45f));
        Fill(new Rect(400,220,480,300),new Color(.025f,.035f,.045f,.96f));
        var title=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=32,font=menuFont};
        var text=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=16,font=menuFont};
        var button=new GUIStyle(GUI.skin.button){fontSize=25,fontStyle=FontStyle.Bold,font=menuFont};
        GUI.Label(new Rect(420,248,440,50),"일시정지",title);
        GUI.Label(new Rect(420,310,440,40),"잠시 숨을 고르세요.",text);
        if(GUI.Button(new Rect(420,380,210,60),"다시하기",button))RestartBattle();
        if(GUI.Button(new Rect(650,380,210,60),"계속하기",button))ResumeBattle();
        GUI.Label(new Rect(420,456,440,30),"다시하기는 시작 화면으로 · ESC로 계속하기",text);
        GUI.matrix=oldMatrix;GUI.color=oldColor;
    }
    void DrawResult()
    {
        GUI.depth=-100;
        var oldMatrix=GUI.matrix;
        var oldColor=GUI.color;
        GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1280f,Screen.height/720f,1));
        GUI.color=new Color(.025f,.035f,.045f,.94f);
        bool cleared=boss!=null && boss.ClearRecorded;
        GUI.DrawTexture(new Rect(400,220,480,cleared?420:300),Texture2D.whiteTexture);
        GUI.color=Color.white;
        var title=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=32,fontStyle=FontStyle.Normal,font=menuFont};
        var text=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=16,font=menuFont};
        GUI.Label(new Rect(420,248,440,50),cleared?"보스 처치 완료":"게임 오버",title);
        GUI.Label(new Rect(420,310,440,40),cleared?"클리어 시간  "+DungeonBoss.FormatTime(boss.BattleTime):"현재 위치에서 부활 · 기록 +1분 30초",text);
        var button=new GUIStyle(GUI.skin.button){fontSize=25,fontStyle=FontStyle.Bold,font=menuFont};
        if(cleared)
        {
            GUI.Label(new Rect(420,350,440,28),"사망 횟수  "+DeathCount+"회",text);
            GUI.Label(new Rect(420,390,90,36),"이름",text);
            var input=new GUIStyle(GUI.skin.textField){font=menuFont,fontSize=22,alignment=TextAnchor.MiddleLeft,richText=false};
            bool wasEnabled=GUI.enabled;
            GUI.enabled=!scoreSubmitted;
            rankingName=GUI.TextField(new Rect(520,390,330,40),rankingName,16,input);
            if(GUI.Button(new Rect(420,456,210,56),scoreSubmitted?"등록 완료":"기록 등록",button))SubmitRanking(rankingName);
            GUI.enabled=wasEnabled;
            if(GUI.Button(new Rect(650,456,210,56),"시작 화면",button))RestartBattle();
            GUI.Label(new Rect(420,530,440,32),rankingMessage,text);
            GUI.Label(new Rect(420,574,440,32),"이름은 최대 16자 · 등록 후 시작 화면으로 이동",text);
        }
        else
        {
            if(GUI.Button(new Rect(420,380,210,60),"다시하기",button))RestartBattle();
            if(GUI.Button(new Rect(650,380,210,60),"부활하기",button))ReviveBattle();
            GUI.Label(new Rect(420,456,440,30),"다시하기를 선택하면 시작 화면으로 돌아갑니다.",text);
        }
        GUI.matrix=oldMatrix;GUI.color=oldColor;
    }
}
