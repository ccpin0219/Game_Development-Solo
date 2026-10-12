using UnityEngine;
using UnityEngine.SceneManagement;

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

    void Awake()
    {
        BattleStarted=false;
        BattleEnded=false;
        menuFont=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Apple SD Gothic Neo","Noto Sans CJK KR"},20);
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
    }
    void Start()
    {
        player=FindFirstObjectByType<DungeonArcher>();
        boss=FindFirstObjectByType<DungeonBoss>();
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
        if(!BattleStarted)return;
        if(!BattleEnded && ((player!=null && player.IsDead) || (boss!=null && boss.Defeated)))
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
        if(menuFont!=null)Destroy(menuFont);
    }
    void OnGUI()
    {
        if(BattleStarted){if(BattleEnded)DrawResult();return;}
        GUI.depth=-100;
        var oldMatrix=GUI.matrix;
        GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1280f,Screen.height/720f,1));
        GUI.color=new Color(.025f,.035f,.045f,.87f);
        GUI.DrawTexture(new Rect(790,90,420,560),Texture2D.whiteTexture);
        GUI.color=new Color(.85f,.46f,.18f,1);
        GUI.DrawTexture(new Rect(840,137,80,3),Texture2D.whiteTexture);
        GUI.color=Color.white;
        var text=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleLeft,fontSize=17,font=menuFont};
        text.normal.textColor=new Color(.82f,.84f,.85f);
        var button=new GUIStyle(GUI.skin.button){fontSize=25,fontStyle=FontStyle.Bold,font=menuFont};
        if(GUI.Button(new Rect(840,212,320,54),"시작",button))StartBattle();
        if(GUI.Button(new Rect(840,278,320,54),"조작법",button))ToggleControls();
        if(GUI.Button(new Rect(840,344,320,54),"나가기",button))QuitGame();
        if(showControls)
        {
            GUI.Label(new Rect(840,410,340,150),"WASD     이동\nSpace     점프\n좌클릭 누르기     활 차징\n좌클릭 놓기     발사\n우클릭     차징 취소\nEsc     일시정지",text);
        }
            GUI.Label(new Rect(840,568,320,28),"최고 클리어 기록",text);
            var recordStyle=new GUIStyle(text){fontSize=27,fontStyle=FontStyle.Bold};
            recordStyle.normal.textColor=new Color(1f,.8f,.4f);
            GUI.Label(new Rect(840,599,320,42),boss==null || boss.BestClearTime<0?"기록 없음":DungeonBoss.FormatTime(boss.BestClearTime),recordStyle);
        GUI.matrix=oldMatrix;GUI.color=Color.white;
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
        var title=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=32,fontStyle=FontStyle.Bold,font=menuFont};
        var text=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=20,font=menuFont};
        GUI.Label(new Rect(420,248,440,50),cleared?"보스 처치 완료":"게임 오버",title);
        GUI.Label(new Rect(420,310,440,40),cleared?"클리어 시간  "+DungeonBoss.FormatTime(boss.BattleTime):"다시 도전해 보세요.",text);
        var button=new GUIStyle(GUI.skin.button){fontSize=25,fontStyle=FontStyle.Bold,font=menuFont};
        if(GUI.Button(new Rect(490,380,300,60),"다시 하기",button))RestartBattle();
        GUI.matrix=oldMatrix;GUI.color=oldColor;
    }
}
