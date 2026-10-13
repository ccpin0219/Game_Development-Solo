using UnityEngine;

// Visual pose is driven by the shared melee states, including their single impact check.
public sealed class DungeonSkeletonBrawler : MonoBehaviour
{
    Transform[] bones=new Transform[4];
    Transform[] hands=new Transform[2];
    public void Initialize(DungeonSkeleton body)
    {
        body.axeArm.gameObject.SetActive(false);body.freeArm.gameObject.SetActive(false);
        Material bone=body.boneRenderers[0].sharedMaterial;
        for(int i=0;i<4;i++)bones[i]=Part(body.model,"Brawler arm bone",bone);
        for(int i=0;i<2;i++)
        {
            hands[i]=Part(body.model,"Clenched bone fist",bone);
            hands[i].localScale=new Vector3(.25f,.22f,.28f);
            for(int knuckle=0;knuckle<4;knuckle++)
            {
                var joint=Part(hands[i],"Knuckle",bone);
                joint.localPosition=new Vector3(-.36f+knuckle*.24f,0,.48f);
                joint.localScale=new Vector3(.21f,.7f,.24f);
            }
        }
        body.boneRenderers=body.model.GetComponentsInChildren<Renderer>();
        Pose(0);
    }
    public void Pose(float strike)
    {
        for(int side=0;side<2;side++)
        {
            float sign=side==0?-1:1;
            Vector3 shoulder=new Vector3(sign*.35f,1.45f,0);
            Vector3 hand=side==0?new Vector3(-.26f,1.42f,.35f):
                new Vector3(Mathf.Lerp(.3f,.1f,Mathf.Max(0,strike)),1.35f,.35f+strike*.6f);
            Vector3 elbow=new Vector3(sign*.46f,1.1f,side==0?.1f:.1f+strike*.35f);
            Segment(bones[side*2],shoulder,elbow);
            Segment(bones[side*2+1],elbow,hand);
            hands[side].localPosition=hand;
        }
    }
    static Transform Part(Transform parent,string name,Material material)
    {
        var obj=new GameObject(name);obj.transform.SetParent(parent,false);
        obj.AddComponent<MeshFilter>().sharedMesh=Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        obj.AddComponent<MeshRenderer>().sharedMaterial=material;return obj.transform;
    }
    static void Segment(Transform bone,Vector3 start,Vector3 end)
    {
        bone.localPosition=(start+end)*.5f;bone.localRotation=Quaternion.FromToRotation(Vector3.up,end-start);
        bone.localScale=new Vector3(.075f,(end-start).magnitude,.075f);
    }
}
