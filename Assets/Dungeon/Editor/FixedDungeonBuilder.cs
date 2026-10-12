using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class FixedDungeonBuilder
{
    const string Folder="Assets/Dungeon/FixedDungeon";
    static Transform root;
    static Mesh block,cube;
    static Material stone,edge,dark,gold,teal,glow,amber,floorA,floorB;
    static readonly Dictionary<Vector2Int,float> cells=new Dictionary<Vector2Int,float>();
    static readonly List<MeshFilter> geometry=new List<MeshFilter>();
    static System.Random random;

    [MenuItem("Tools/Dungeon/Build Fixed Sanctum")]
    public static void Build()
    {
        if(Application.isPlaying)throw new System.InvalidOperationException("Build in edit mode.");
        if(GameObject.Find("Fixed Sanctum Dungeon")!=null)throw new System.InvalidOperationException("Dungeon already exists.");
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Dungeon","FixedDungeon");
        random=new System.Random(714);cells.Clear();geometry.Clear();
        root=new GameObject("Fixed Sanctum Dungeon").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject,"Build fixed dungeon");
        block=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Dungeon/Guardian/Bevel Block.asset");
        cube=Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        stone=Mat("Blue Grey Limestone");edge=Mat("Pale Carved Edges");dark=Mat("Recessed Basalt");gold=Mat("Antique Brass Inlay");teal=Mat("Sanctum Teal");glow=Mat("Quiet Azure Runes");
        floorA=NewMaterial("Worn flagstone",new Color(.25f,.3f,.32f));
        floorB=NewMaterial("Cool flagstone",new Color(.2f,.25f,.27f));
        amber=NewMaterial("Torch ember",new Color(1,.42f,.075f));amber.EnableKeyword("_EMISSION");amber.SetColor("_EmissionColor",new Color(1,.24f,.025f)*3);
        // Connected grid, 4m cells. The western passage loops back into the crossroads.
        Room(-8,8,-216,-192,9); Room(-4,4,-192,-180,7);
        Room(-12,12,-180,-156,13); Room(-4,4,-156,-124,7);
        Room(-12,12,-124,-100,11); Room(-4,4,-100,-96,7);
        Room(-8,8,-96,-76,10); Room(-4,4,-76,-52,8);
        Room(-32,0,-148,-140,7); Room(-40,-24,-160,-132,9);
        Room(-36,-28,-132,-108,7); Room(-28,-12,-116,-108,7);
        Room(12,24,-116,-108,7); Room(24,44,-124,-100,12);
        BuildShell();
        // Monumental entry, repeated arches and changing ceiling height establish readable progression.
        foreach(float z in new[]{-211f,-194f,-185f,-153f,-136f,-126f,-99f,-77f,-67f,-57f})Arch(0,z,z==-211?6:3.6f,4.7f);
        foreach(float z in new[]{-174f,-162f})foreach(float x in new[]{-8f,8f})Column(new Vector3(x,0,z),10);
        foreach(float z in new[]{-119f,-105f})foreach(float x in new[]{-8f,8f})Column(new Vector3(x,0,z),8);
        foreach(float x in new[]{-5f,5f})Column(new Vector3(x,0,-88),8);
        // A processional strip leads from the spawn toward the first hall.
        for(int z=-210;z<-156;z+=4){Box("Processional slab",new Vector3(0,.025f,z),new Vector3(3,.05f,3.6f),dark);foreach(float s in new[]{-1f,1f})Box("Brass path edging",new Vector3(s*1.65f,.04f,z),new Vector3(.08f,.05f,3.6f),gold);}
        // Central dry fountain and broken obelisks keep the nave from feeling like an empty box.
        Box("Font base",new Vector3(0,.25f,-168),new Vector3(5,.5f,5),dark,true);
        foreach(float s in new[]{-1f,1f}){
            Box("Font rim",new Vector3(s*2.3f,.7f,-168),new Vector3(.45f,1,5),edge,true);
            Box("Font rim",new Vector3(0,.7f,-168+s*2.3f),new Vector3(4.2f,1,.45f),edge,true);
        }
        Box("Still blue basin",new Vector3(0,.65f,-168),new Vector3(4,.1f,4),teal);
        Box("Font monolith",new Vector3(0,2,-168),new Vector3(.7f,3,.7f),dark,true);
        Box("Font rune",new Vector3(0,3.65f,-168),new Vector3(.5f,.7f,.5f),glow).rotation=Quaternion.Euler(0,45,0);
        LightAt(new Vector3(0,4,-168),new Color(.25f,.65f,.8f),3,13);
        // Crypt: individual tombs with recessed lids, bronze bands and funerary candles.
        foreach(float x in new[]{-37f,-27f})foreach(float z in new[]{-154f,-143f}){
            Box("Sarcophagus",new Vector3(x,.7f,z),new Vector3(2,1.4f,4),dark,true);
            Box("Carved tomb lid",new Vector3(x,1.45f,z),new Vector3(2.25f,.3f,4.2f),edge);
            Box("Tomb seal",new Vector3(x,1.64f,z),new Vector3(.18f,.08f,3.3f),gold);
        }
        // Shrine raised altar with walkable low steps, blue crystal and radiating inlays.
        for(int i=0;i<3;i++)Box("Shrine step",new Vector3(34,.15f+i*.3f,-115-i*1.1f),new Vector3(9-i*.8f,.3f,5-i*.5f),edge,true);
        var exploration=root.gameObject.AddComponent<DungeonExploration>();
        exploration.seals=new[]{Seal(new Vector3(-32,0,-154)),Seal(new Vector3(34,.9f,-118))};
        exploration.activatedMaterial=amber;
        foreach(float x in new[]{27f,41f})Column(new Vector3(x,0,-119),9);
        for(int i=0;i<12;i++){float a=i*Mathf.PI/6;var t=Box("Shrine radial inlay",new Vector3(34+Mathf.Cos(a)*4,.04f,-108+Mathf.Sin(a)*4),new Vector3(.12f,.07f,1),gold);t.rotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);}
        LightAt(new Vector3(34,5,-114),new Color(.12f,.6f,.85f),5,18);
        // Braziers set the route cadence; non-shadowed local lights cap the GPU cost.
        foreach(var p in new[]{new Vector3(-5,0,-208),new Vector3(5,0,-196),new Vector3(-8,0,-177),new Vector3(8,0,-159),new Vector3(-2.8f,0,-144),new Vector3(-36,0,-135),new Vector3(-36,0,-157),new Vector3(-32,0,-119),new Vector3(-8,0,-119),new Vector3(8,0,-105),new Vector3(27,0,-104),new Vector3(41,0,-121),new Vector3(-5,0,-91),new Vector3(5,0,-80),new Vector3(-2.8f,0,-62)})Torch(p);
        // Side-wall banners and shallow rubble stay off the walking centerline.
        foreach(float z in new[]{-174f,-162f,-118f,-106f})foreach(float s in new[]{-1f,1f}){
            Box("Hanging tapestry",new Vector3(s*11.3f,5.7f,z),new Vector3(.15f,4.4f,2),teal);
            Box("Banner top rail",new Vector3(s*11.05f,8,z),new Vector3(.4f,.2f,2.5f),gold);
            Box("Banner rune",new Vector3(s*11.18f,5.8f,z),new Vector3(.06f,1.1f,.15f),gold);
        }
        foreach(var p in new[]{new Vector3(-6,0,-212),new Vector3(9,0,-176),new Vector3(-10,0,-160),new Vector3(-38,0,-150),new Vector3(-9,0,-103),new Vector3(42,0,-102)}){
            for(int i=0;i<7;i++){float r=.25f+(float)random.NextDouble()*.55f;var t=Box("Fallen stone",p+new Vector3((float)random.NextDouble()*2,r*.4f,(float)random.NextDouble()*2),new Vector3(r*1.7f,r,r),i%2==0?stone:edge);t.rotation=Quaternion.Euler(0,random.Next(180),random.Next(-18,18));}
        }
        var rest=Box("Resting shrine",new Vector3(-4,.6f,-84),new Vector3(1.8f,1.2f,1.8f),dark,true);
        Box("Resting light",new Vector3(-4,1.5f,-84),new Vector3(.65f,.65f,.65f),glow);
        exploration.checkpoint=new GameObject("Rest Area").transform;exploration.checkpoint.SetParent(root);exploration.checkpoint.position=new Vector3(-2,0,-84);
        LightAt(new Vector3(-4,3,-84),new Color(.2f,.7f,.65f),3,9);
        // Portcullis moves as a single object after both seals are activated.
        var gate=new GameObject("Sealed Portcullis").transform;gate.SetParent(root);gate.position=new Vector3(0,0,-68);
        for(int i=-3;i<=3;i++){var t=Box("Gate bar",new Vector3(i,3.8f,-68),new Vector3(.18f,7.6f,.25f),gold);t.SetParent(gate,true);geometry.Remove(t.GetComponent<MeshFilter>());t.gameObject.isStatic=false;}
        foreach(float y in new[]{1.2f,5.5f}){var t=Box("Gate crossbar",new Vector3(0,y,-68),new Vector3(7.8f,.25f,.35f),dark);t.SetParent(gate,true);geometry.Remove(t.GetComponent<MeshFilter>());t.gameObject.isStatic=false;}
        var gateCollider=gate.gameObject.AddComponent<BoxCollider>();gateCollider.center=new Vector3(0,3.8f,0);gateCollider.size=new Vector3(8,7.6f,.6f);exploration.gate=gate;
        // Open only the center of the original arena's southern wall.
        var south=GameObject.Find("South Wall");if(south!=null){Undo.RecordObject(south,"Open dungeon entrance");south.SetActive(false);}
        Box("Arena south west wall",new Vector3(-20,14,-55.5f),new Vector3(32,28,3),stone,true);
        Box("Arena south east wall",new Vector3(20,14,-55.5f),new Vector3(32,28,3),stone,true);
        Box("Arena entrance lintel",new Vector3(0,18,-55.5f),new Vector3(8,20,3),stone,true);
        var player=UnityEngine.Object.FindFirstObjectByType<DungeonArcher>();Undo.RecordObject(player.transform,"Move dungeon spawn");player.transform.position=new Vector3(0,.08f,-208);player.transform.rotation=Quaternion.identity;
        Combine();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.gameObject.scene);EditorSceneManager.SaveScene(root.gameObject.scene);
        Debug.Log("Fixed dungeon built: "+cells.Count+" walkable cells.");
    }

    static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Dungeon/Sanctum/"+name+".mat");
    static Material NewMaterial(string name,Color color){var m=new Material(stone);m.name=name;m.SetColor("_BaseColor",color);AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");return m;}
    static void Room(int xmin,int xmax,int zmin,int zmax,float height){for(int x=xmin/4;x<xmax/4;x++)for(int z=zmin/4;z<zmax/4;z++){var key=new Vector2Int(x,z);cells[key]=cells.ContainsKey(key)?Mathf.Max(cells[key],height):height;}}
    static Transform Box(string name,Vector3 position,Vector3 size,Material material,bool collision=false,bool bevel=true)
    {
        var g=new GameObject(name);g.transform.SetParent(root,false);g.transform.position=position;g.transform.localScale=size;g.isStatic=true;
        var f=g.AddComponent<MeshFilter>();f.sharedMesh=bevel?block:cube;g.AddComponent<MeshRenderer>().sharedMaterial=material;geometry.Add(f);
        if(collision)g.AddComponent<BoxCollider>();return g.transform;
    }
    static void BuildShell()
    {
        Vector2Int[] dirs={Vector2Int.left,Vector2Int.right,Vector2Int.down,Vector2Int.up};
        foreach(var cell in cells){
            var c=new Vector3(cell.Key.x*4+2,0,cell.Key.y*4+2);float h=cell.Value;
            Box("Floor foundation",c+Vector3.down*.3f,new Vector3(4,.6f,4),dark,true,false);
            Box("Worn floor tile",c+Vector3.up*.025f,new Vector3(3.94f,.05f,3.94f),random.Next(3)==0?floorA:floorB,false,false);
            Box("Vault canopy",c+Vector3.up*(h+.3f),new Vector3(4,.6f,4),dark,true,false);
            foreach(var d in dirs){
                var n=cell.Key+d;bool alongX=d.x!=0;
                if(cells.ContainsKey(n)){
                    if(cells[n]<h)Box("Vault height transition",c+new Vector3(d.x*2,(h+cells[n])*.5f,d.y*2),alongX?new Vector3(.5f,h-cells[n],4):new Vector3(4,h-cells[n],.5f),dark,true,false);
                    continue;
                }
                // Northern mouth joins the existing arena rather than closing the path.
                if(cell.Key.y==-14 && d==Vector2Int.up)continue;
                Vector3 p=c+new Vector3(d.x*2,0,d.y*2);
                Box("Solid boundary wall",p+Vector3.up*h*.5f,alongX?new Vector3(.65f,h,4):new Vector3(4,h,.65f),dark,true,false);
                for(int row=0;row<Mathf.CeilToInt(h/1.25f);row++)for(int k=0;k<2;k++){
                    float y=Mathf.Min(h-.3f,.6f+row*1.25f);Vector3 offset=alongX?new Vector3(-d.x*.12f,y,-1+k*2):new Vector3(-1+k*2,y,-d.y*.12f);
                    Box("Dressed wall block",p+offset,alongX?new Vector3(.65f,1.18f,1.94f):new Vector3(1.94f,1.18f,.65f),random.Next(5)==0?edge:stone);
                }
                Box("Wall foot course",p+Vector3.up*.25f,alongX?new Vector3(.9f,.5f,4):new Vector3(4,.5f,.9f),edge);
                Box("Wall cornice",p+Vector3.up*(h-.25f),alongX?new Vector3(1,.5f,4):new Vector3(4,.5f,1),edge);
            }
        }
    }
    static void Column(Vector3 p,float h){Box("Column plinth",p+Vector3.up*.3f,new Vector3(1.8f,.6f,1.8f),edge,true);Box("Fluted pier",p+Vector3.up*(h*.5f),new Vector3(1.15f,h,1.15f),stone,true);Box("Column capital",p+Vector3.up*(h-.3f),new Vector3(1.9f,.6f,1.9f),edge);foreach(float s in new[]{-1f,1f})Box("Pier flute",p+new Vector3(s*.59f,h*.5f,0),new Vector3(.05f,h-1,.3f),dark);}
    static void Arch(float x,float z,float radius,float spring){foreach(float s in new[]{-1f,1f})Column(new Vector3(x+s*radius,0,z),spring);for(int i=0;i<12;i++){float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;var p=new Vector3(x+Mathf.Cos(a)*radius,spring+Mathf.Sin(a)*radius*.65f,z);var q=new Vector3(x+Mathf.Cos(b)*radius,spring+Mathf.Sin(b)*radius*.65f,z);var t=Box("Arch voussoir",(p+q)*.5f,new Vector3(.52f,Vector3.Distance(p,q)+.08f,.85f),i==5?gold:edge);t.rotation=Quaternion.FromToRotation(Vector3.up,q-p);}}
    static void Torch(Vector3 p){var torch=DungeonWallTorches.Place(root,p);LightAt(torch.transform.TransformPoint(new Vector3(0,.35f,1)),new Color(1,.52f,.23f),4,12);}
    static void LightAt(Vector3 p,Color c,float intensity,float range){var g=new GameObject("Local chamber light");g.transform.SetParent(root);g.transform.position=p;var l=g.AddComponent<Light>();l.type=LightType.Point;l.color=c;l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;}
    static Transform Seal(Vector3 p){var g=new GameObject("Ancient seal").transform;g.SetParent(root);g.position=p;var plinth=Box("Seal pedestal",p+Vector3.up*.7f,new Vector3(1.6f,1.4f,1.6f),dark,true);plinth.SetParent(g,true);var crystal=Box("Seal Crystal",p+Vector3.up*1.85f,new Vector3(.7f,1.2f,.7f),glow);crystal.rotation=Quaternion.Euler(0,45,12);crystal.SetParent(g,true);geometry.Remove(crystal.GetComponent<MeshFilter>());crystal.gameObject.isStatic=false;LightAt(p+Vector3.up*2.5f,new Color(.15f,.65f,1),3,8);return g;}
    static void Combine(){int index=0;foreach(var batch in geometry.GroupBy(f=>new {m=f.GetComponent<Renderer>().sharedMaterial,x=Mathf.FloorToInt(f.transform.position.x/24),z=Mathf.FloorToInt(f.transform.position.z/24)})){
        var mesh=new Mesh();mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.CombineMeshes(batch.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=f.transform.localToWorldMatrix}).ToArray(),true,true);mesh.name="Dungeon masonry chunk";AssetDatabase.CreateAsset(mesh,Folder+"/Chunk"+(index++)+".asset");var g=new GameObject("Masonry "+batch.Key.x+" "+batch.Key.z+" "+batch.Key.m.name);g.transform.SetParent(root);g.isStatic=true;g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=batch.Key.m;
    }foreach(var f in geometry){if(f.GetComponent<Collider>()!=null){UnityEngine.Object.DestroyImmediate(f.GetComponent<Renderer>());UnityEngine.Object.DestroyImmediate(f);}else UnityEngine.Object.DestroyImmediate(f.gameObject);}}
}
