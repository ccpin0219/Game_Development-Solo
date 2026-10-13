using UnityEngine;

// Fixed, north-up room diagram for the layout authored by FixedDungeonBuilder.BuildGrid.
// The player selects a room or corridor; no continuously moving player marker is drawn.
public sealed class DungeonRoomMap
{
    const float Width = 112, Height = 164;
    readonly string[] roomNames = {
        "서쪽 묘실", "고요한 전실", "동쪽 무기고",
        "잊힌 서고", "순례자의 홀", "푸른 제단",
        "서쪽 감시실", "수호자의 회랑", "동쪽 유적"
    };
    readonly Color accent = new Color(1, .73f, .31f);
    GUIStyle timerStyle;

    public DungeonRoomMap(DungeonRoomEncounter[] rooms)
    {
        // Use scene-authored names, keeping the safe room which has no encounter component.
        foreach (var room in rooms)
        {
            if (room == null) continue;
            Vector3 center = room.entryBounds.center;
            int col = Mathf.RoundToInt(center.x / 32) + 1;
            int row = Mathf.RoundToInt((center.z + 152) / 32);
            if (col >= 0 && col < 3 && row >= 0 && row < 3)
                roomNames[row * 3 + col] = room.roomName;
        }
    }

    // Match actual room floors, not the inset combat triggers. Height is deliberately
    // ignored so jumping near a doorway does not change the highlighted room.
    public static int RoomAt(Vector3 position)
    {
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
                if (Inside(position, (col - 1) * 32, -152 + row * 32, 12, 12))
                    return row * 3 + col;
        if (Inside(position, 0, -196, 16, 16)) return 9;
        if (Inside(position, 0, 0, 36, 54)) return 10;
        return -1;
    }

    public string AreaName(Vector3 position)
    {
        int room = RoomAt(position);
        if (room >= 0 && room < 9) return roomNames[room];
        if (room == 9) return "시작 방";
        if (room == 10) return "수호자의 전당";
        return Inside(position, 0, -65, 4, 11) ? "수호자의 문" : "연결 회랑";
    }

    public void Draw(Vector3 position,float elapsedSeconds=0)
    {
        var oldMatrix = GUI.matrix;
        var oldColor = GUI.color;
        try
        {
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f, 1.4f);
            GUI.matrix = Matrix4x4.TRS(new Vector3(Screen.width - (Width + 16) * scale, 16 * scale, 0),
                Quaternion.identity, new Vector3(scale, scale, 1));
            Fill(new Rect(0, 0, Width, Height), new Color(.025f, .04f, .06f, .5f));

            // Draw connections first so they terminate underneath the room tiles.
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                {
                    Rect tile = Tile(row, col);
                    float x = (col - 1) * 32, z = -152 + row * 32;
                    if (col < 2) Link(new Rect(tile.xMax, tile.center.y - 1, 13, 2), Inside(position, x + 16, z, 4, 4));
                    if (row < 2) Link(new Rect(tile.center.x - 1, tile.y - 8, 2, 8), Inside(position, x, z + 16, 4, 4));
                }
            Link(new Rect(55, 30, 2, 10), Inside(position, 0, -65, 4, 11));
            Link(new Rect(55, 122, 2, 12), Inside(position, 0, -172, 4, 8));

            int current = RoomAt(position);
            Node(new Rect(45, 8, 22, 22), current == 10);
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                {
                    int id = row * 3 + col;
                    Node(Tile(row, col), current == id);
                }
            Node(new Rect(45, 134, 22, 22), current == 9);
            if(timerStyle==null)
            {
                timerStyle=new GUIStyle(GUI.skin.label){font=Resources.Load<Font>("Fonts/NeoDunggeunmo"),fontSize=18,alignment=TextAnchor.MiddleCenter};
                timerStyle.normal.textColor=new Color(.92f,.92f,.85f);
            }
            Fill(new Rect(0,Height+6,Width,28),new Color(.025f,.04f,.06f,.65f));
            int totalSeconds=Mathf.FloorToInt(Mathf.Max(0,elapsedSeconds));
            GUI.color=Color.white;
            GUI.Label(new Rect(0,Height+6,Width,28),string.Format("{0:00}:{1:00}",totalSeconds/60,totalSeconds%60),timerStyle);
        }
        finally { GUI.matrix = oldMatrix; GUI.color = oldColor; }
    }

    static bool Inside(Vector3 p, float x, float z, float halfX, float halfZ)
        => Mathf.Abs(p.x - x) <= halfX && Mathf.Abs(p.z - z) <= halfZ;
    static Rect Tile(int row, int col) => new Rect(10 + col * 35, 40 + (2 - row) * 30, 22, 22);
    void Node(Rect rect, bool current) => Fill(rect, current ? accent : new Color(.28f, .36f, .4f));
    void Link(Rect rect, bool current) => Fill(rect, current ? accent : new Color(.25f, .33f, .37f));
    static void Fill(Rect rect, Color color) { GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); }
}
