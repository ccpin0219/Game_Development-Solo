using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DungeonWallTorches
{
    const string PrefabPath = "Assets/Dungeon/FixedDungeon/Wall Torch.prefab";

    public static GameObject Place(Transform root, Vector3 previousPosition)
    {
        var probe = new Vector3(previousPosition.x, 3.1f, previousPosition.z);
        var walls = root.GetComponentsInChildren<BoxCollider>()
            .Where(c => c.name == "Solid boundary wall").ToArray();
        if (walls.Length == 0) throw new System.InvalidOperationException("No dungeon boundary walls found.");
        Vector3 contact = Vector3.zero, inward = Vector3.zero;
        float nearest = float.PositiveInfinity;
        foreach (var wall in walls)
        {
            var bounds = wall.bounds;
            bool alongX = bounds.size.x < bounds.size.z;
            int normalAxis = alongX ? 0 : 2, tangentAxis = alongX ? 2 : 0;
            Vector3 normal = Vector3.zero;
            normal[normalAxis] = probe[normalAxis] < bounds.center[normalAxis] ? -1 : 1;
            Vector3 point = probe;
            point[normalAxis] = bounds.center[normalAxis] + normal[normalAxis] * bounds.extents[normalAxis];
            // Keep the mounting plate on a flat wall face rather than across a corner.
            point[tangentAxis] = Mathf.Clamp(probe[tangentAxis], bounds.min[tangentAxis] + .65f, bounds.max[tangentAxis] - .65f);
            float distance = (point - probe).sqrMagnitude;
            if (distance >= nearest) continue;
            nearest = distance; contact = point; inward = normal;
        }
        var prefab = EnsurePrefab();
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
        Undo.RegisterCreatedObjectUndo(instance, "Mount dungeon torch");
        instance.name = "Wall Torch";
        instance.transform.SetPositionAndRotation(contact + inward * .14f, Quaternion.LookRotation(inward));
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        return instance;
    }

    [MenuItem("Tools/Dungeon/Mount Exploration Torches On Walls")]
    public static void ConvertExisting()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Use edit mode.");
        var root = GameObject.Find("Fixed Sanctum Dungeon").transform;
        var old = root.GetComponentsInChildren<DungeonBrazierAmbience>(true);
        // Validate every placement before removing any existing object.
        if (!root.GetComponentsInChildren<BoxCollider>().Any(c => c.name == "Solid boundary wall"))
            throw new System.InvalidOperationException("No boundary walls found.");
        foreach (var torch in old)
        {
            Vector3 position = torch.transform.position;
            Vector3 previousLight = torch.name == "Wall Torch"
                ? torch.transform.TransformPoint(new Vector3(0, .35f, 1)) : position + Vector3.up * 3;
            var replacement = Place(root, position);
            var fill = root.GetComponentsInChildren<Light>()
                .FirstOrDefault(l => l.name == "Local chamber light" &&
                    Vector3.Distance(l.transform.position, previousLight) < .05f);
            if (fill != null)
            {
                Undo.RecordObject(fill.transform, "Move torch fill light");
                fill.transform.position = replacement.transform.TransformPoint(new Vector3(0, .35f, 1));
            }
            Undo.DestroyObjectImmediate(torch.gameObject);
        }
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Debug.Log("Converted " + old.Length + " exploration braziers to wall torches.");
    }

    static GameObject EnsurePrefab()
    {
        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (saved != null) return saved;
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dungeon/Sanctum/Braziers/Sanctum Brazier.prefab");
        var root = new GameObject("Wall Torch");
        var block = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Dungeon/Guardian/Bevel Block.asset");
        var iron = AssetDatabase.LoadAssetAtPath<Material>("Assets/Dungeon/Sanctum/Recessed Basalt.mat");
        var brass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Dungeon/Sanctum/Antique Brass Inlay.mat");
        AddMesh(root, "Iron Wall Plate", block, iron, new Vector3(0, -.1f, 0), new Vector3(.5f, 1.2f, .16f));
        AddMesh(root, "Plate Inlay", block, brass, new Vector3(0, -.1f, .095f), new Vector3(.3f, .9f, .06f));
        AddMesh(root, "Projecting Bracket", block, iron, new Vector3(0, -.42f, .38f), new Vector3(.14f, .16f, .75f));
        var brace = AddMesh(root, "Diagonal Brace", block, brass, new Vector3(0, -.55f, .32f), new Vector3(.09f, .62f, .1f));
        brace.transform.localRotation = Quaternion.Euler(48, 0, 0);
        foreach (float y in new[] { -.55f, .35f })
            AddMesh(root, "Mounting Bolt", block, iron, new Vector3(0, y, .15f), new Vector3(.13f, .13f, .1f));
        foreach (var name in new[] { "Open Fire Bowl", "Bowl Brass Lip", "Ember Bed" })
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Dungeon/Sanctum/Braziers/" + name + ".asset");
            var material = name == "Open Fire Bowl" ? iron : name == "Bowl Brass Lip" ? brass :
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Dungeon/Sanctum/Braziers/Living Amber.mat");
            AddMesh(root, name, mesh, material, new Vector3(0, -3.32f * .65f, .68f), Vector3.one * .65f);
        }
        var flame = Object.Instantiate(source.transform.Find("Flickering Flame").gameObject, root.transform);
        flame.name = "Flickering Flame"; flame.transform.localPosition = new Vector3(0, 0, .68f); flame.transform.localScale = Vector3.one * .65f;
        var sparks = Object.Instantiate(source.transform.Find("Rising Embers").gameObject, root.transform);
        sparks.name = "Rising Embers"; sparks.transform.localPosition = new Vector3(0, .22f, .68f); sparks.transform.localScale = Vector3.one * .65f;
        var lightObject = new GameObject("Torch Firelight"); lightObject.transform.SetParent(root.transform, false);
        lightObject.transform.localPosition = new Vector3(0, .45f, .9f);
        var light = lightObject.AddComponent<Light>(); light.type = LightType.Point;
        light.color = new Color(1, .43f, .12f); light.intensity = 4.5f; light.range = 10; light.shadows = LightShadows.None;
        var ambience = root.AddComponent<DungeonBrazierAmbience>(); ambience.flame = flame.transform; ambience.fireLight = light;
        saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    static GameObject AddMesh(GameObject root, string name, Mesh mesh, Material material, Vector3 p, Vector3 scale)
    {
        var obj = new GameObject(name); obj.transform.SetParent(root.transform, false);
        obj.transform.localPosition = p; obj.transform.localScale = scale;
        obj.AddComponent<MeshFilter>().sharedMesh = mesh; obj.AddComponent<MeshRenderer>().sharedMaterial = material;
        return obj;
    }
}
