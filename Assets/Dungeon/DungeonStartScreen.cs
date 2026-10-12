using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-1000)]
public class DungeonStartScreen : MonoBehaviour
{
    public static bool BattleStarted { get; private set; }
    public static bool BattleEnded { get; private set; }
    bool restarting;
    DungeonArcher player;
    Camera gameplayCamera, menuCamera;
    Font menuFont;
    bool cameraWasEnabled, showControls;
    DungeonBoss boss;
    Texture2D menuBackground;
    Font titleFont;
    bool ownsFont;
    int selected;
    GUIStyle titleStyle, subtitleStyle, bodyStyle, menuButtonStyle, smallStyle;
    static readonly Color Gold = new Color(.83f, .67f, .43f);

    void Awake()
    {
        BattleStarted=false;
        BattleEnded=false;
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
            if(showControls){if(keys.escapeKey.wasPressedThisFrame)showControls=false;return;}
            if(keys.downArrowKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame)selected=(selected+1)%3;
            if(keys.upArrowKey.wasPressedThisFrame || keys.wKey.wasPressedThisFrame)selected=(selected+2)%3;
            if(keys.enterKey.wasPressedThisFrame)
            {
                if(selected==0)StartBattle();else if(selected==1)ToggleControls();else QuitGame();
            }
            return;
        }
        if(!BattleEnded && ((player!=null && player.IsDead) || (boss!=null && boss.gameObject.activeInHierarchy && boss.Defeated)))
            BattleEnded=true;
        if(!BattleEnded)return;
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
    }
    public void RestartBattle()
    {
        if(!BattleEnded || restarting)return;
        restarting=true;
        Time.timeScale=1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().path);
    }
    public void ToggleControls() { showControls=!showControls; }
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
        BattleStarted=true;
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
        if(BattleStarted){if(BattleEnded)DrawResult();return;}
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
        Fill(new Rect(98,282,28,2),Gold);
        GUI.Label(new Rect(142,269,300,32),"돌의 수호자",smallStyle);
        bool previousEnabled=GUI.enabled;
        GUI.enabled=!showControls;
        if(MenuButton(new Rect(96,374,340,52),"탐험 시작",selected==0,0))StartBattle();
        if(MenuButton(new Rect(96,438,340,52),"조작법",selected==1,1))ToggleControls();
        if(MenuButton(new Rect(96,502,340,52),"나가기",selected==2,2))QuitGame();
        GUI.enabled=previousEnabled;
        GUI.Label(new Rect(98,605,400,28),"BEST  /  "+(boss==null || boss.BestClearTime<0?"--:--.--":DungeonBoss.FormatTime(boss.BestClearTime)),smallStyle);
        GUI.Label(new Rect(98,667,520,24),"W/S 선택    ENTER 확인    마우스로 선택 가능",smallStyle);
        // Lightweight drifting embers; no new scene objects or particle materials.
        for(int i=0;i<18;i++)
        {
            float t=Time.unscaledTime;
            float x=570+(i*139%660)+Mathf.Sin(t*.35f+i)*12;
            float y=710-Mathf.Repeat(i*47+t*(9+i%4*3),650);
            float alpha=(.18f+.25f*Mathf.Sin(i+t*.8f))*Mathf.Clamp01(y/100);
            Fill(new Rect(Mathf.Floor(x/4)*4,Mathf.Floor(y/4)*4,4,4),new Color(1,.52f,.2f,Mathf.Max(0,alpha)));
        }
        if(showControls)DrawControls();
        GUI.matrix=oldMatrix;GUI.color=oldColor;
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
    void DrawControls()
    {
        Fill(new Rect(0,0,1280,720),new Color(0,0,0,.78f));
        Fill(new Rect(352,112,576,496),new Color(.045f,.07f,.085f,1));
        Fill(new Rect(392,146,32,2),Gold);
        GUI.Label(new Rect(392,170,450,40),"조작법",new GUIStyle(bodyStyle){fontSize=32});
        string[] keys={"W A S D","마우스","SPACE","좌클릭 유지 / 놓기","우클릭","TAB","ESC","E"};
        string[] actions={"이동","주변 둘러보기","점프","활 충전 / 발사","충전 취소","강공 / 속공 전환","일시정지 · 클릭하여 복귀","가까운 봉인 장치 작동"};
        for(int i=0;i<keys.Length;i++)
        {
            float y=222+i*36;
            GUI.Label(new Rect(414,y,215,32),keys[i],bodyStyle);
            GUI.Label(new Rect(638,y,252,32),actions[i],new GUIStyle(bodyStyle){fontSize=16});
        }
        if(MenuButton(new Rect(406,528,450,44),"돌아가기",true))showControls=false;
    }
    void DrawResult()
    {
        GUI.depth=-100;
        var oldMatrix=GUI.matrix;
        var oldColor=GUI.color;
        GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1280f,Screen.height/720f,1));
        GUI.color=new Color(.025f,.035f,.045f,.94f);
        GUI.DrawTexture(new Rect(400,220,480,300),Texture2D.whiteTexture);
        GUI.color=Color.white;
        bool cleared=boss!=null && boss.ClearRecorded;
        var title=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=32,fontStyle=FontStyle.Normal,font=menuFont};
        var text=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=16,font=menuFont};
        GUI.Label(new Rect(420,248,440,50),cleared?"보스 처치 완료":"게임 오버",title);
        GUI.Label(new Rect(420,310,440,40),cleared?"클리어 시간  "+DungeonBoss.FormatTime(boss.BattleTime):"다시 도전해 보세요.",text);
        var button=new GUIStyle(GUI.skin.button){fontSize=25,fontStyle=FontStyle.Bold,font=menuFont};
        if(GUI.Button(new Rect(490,380,300,60),"다시 하기",button))RestartBattle();
        GUI.matrix=oldMatrix;GUI.color=oldColor;
    }
}
