using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class DungeonEncounterBuilder
{
    [MenuItem("Tools/Dungeon/Add Room Encounters")]
    public static void Build()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Use edit mode.");
        if (Object.FindFirstObjectByType<DungeonRoomEncounter>() != null)
            throw new System.InvalidOperationException("Room encounters already exist.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dungeon/Skeleton/Axe Skeleton.prefab");
        if (prefab == null) throw new System.InvalidOperationException("Missing skeleton prefab.");
        var root = new GameObject("Dungeon Room Encounters").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Add room encounters");
        var original = GameObject.Find("Skeleton Encounters");
        if (original != null) { Undo.RecordObject(original, "Disable roaming encounters"); original.SetActive(false); }
        var nave = Room(root, "순례자의 홀", new Vector3(0, 3, -168), new Vector3(22, 8, 22));
        Enemies(nave, prefab, new Vector3(-5, .08f, -170), new Vector3(5, .08f, -163));
        Doors(nave, new Vector3(0, 0, -180), new Vector3(0, 0, -156));
        var crypt = Room(root, "잊힌 자의 납골당", new Vector3(-32, 3, -146), new Vector3(14, 8, 26));
        Enemies(crypt, prefab, new Vector3(-32, .08f, -145), new Vector3(-34, .08f, -150));
        Doors(crypt, new Vector3(-32, 0, -132), new Vector3(-24, 90, -144));
        var crossing = Room(root, "갈림 회랑", new Vector3(0, 3, -112), new Vector3(22, 8, 22));
        Enemies(crossing, prefab, new Vector3(-5, .08f, -111), new Vector3(5, .08f, -113));
        Doors(crossing, new Vector3(0, 0, -124), new Vector3(0, 0, -100), new Vector3(-12, 90, -112), new Vector3(12, 90, -112));
        var shrine = Room(root, "푸른 봉인실", new Vector3(34, 3, -112), new Vector3(18, 8, 22));
        Enemies(shrine, prefab, new Vector3(33, .08f, -106), new Vector3(39, .08f, -111));
        Doors(shrine, new Vector3(24, 90, -112));
        var arena = Room(root, "수호자의 전당", new Vector3(0, 8, 1), new Vector3(68, 20, 100));
        arena.boss = Object.FindFirstObjectByType<DungeonBoss>();
        if (arena.boss == null) throw new System.InvalidOperationException("Missing boss.");
        Undo.RecordObject(arena.boss.gameObject, "Hide dormant boss");
        arena.boss.gameObject.SetActive(false);
        Doors(arena, new Vector3(0, 0, -54));
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        EditorSceneManager.SaveScene(root.gameObject.scene);
    }
    static DungeonRoomEncounter Room(Transform root, string name, Vector3 center, Vector3 size)
    {
        var room = new GameObject(name).AddComponent<DungeonRoomEncounter>();
        room.transform.SetParent(root, false);
        room.roomName = name; room.entryBounds = new Bounds(center, size);
        room.enemies = new DungeonSkeleton[0];
        return room;
    }
    static void Enemies(DungeonRoomEncounter room, GameObject prefab, params Vector3[] points)
    {
        room.enemies = new DungeonSkeleton[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, room.transform);
            enemy.transform.position = points[i]; enemy.transform.rotation = Quaternion.Euler(0, 180, 0);
            room.enemies[i] = enemy.GetComponent<DungeonSkeleton>();
            room.enemies[i].sightRange = 40; room.enemies[i].leashRange = 45;
            enemy.SetActive(false);
        }
    }
    // A portal's y component stores its yaw; the doorway itself is at floor height.
    static void Doors(DungeonRoomEncounter room, params Vector3[] portals)
    {
        var leaves = new List<Transform>(); var blockers = new List<Collider>();
        var iron = AssetDatabase.LoadAssetAtPath<Material>("Assets/Dungeon/Sanctum/Recessed Basalt.mat");
        var brass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Dungeon/Sanctum/Antique Brass Inlay.mat");
        foreach (var portal in portals)
        {
            var door = new GameObject("Encounter Portcullis").transform; door.SetParent(room.transform, false);
            door.position = new Vector3(portal.x, 0, portal.z); door.rotation = Quaternion.Euler(0, portal.y, 0);
            var collider = door.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, 3.9f, 0); collider.size = new Vector3(8, 7.8f, .6f);
            collider.enabled = false; blockers.Add(collider);
            var leaf = new GameObject("Sliding Iron Gate").transform; leaf.SetParent(door, false);
            for (int i = -7; i <= 7; i++) Box(leaf, new Vector3(i * .52f, 3.9f, 0), new Vector3(.14f, 7.8f, .22f), iron);
            foreach (float y in new[] { .3f, 2.7f, 5.1f, 7.5f })
                Box(leaf, new Vector3(0, y, 0), new Vector3(8, .18f, .3f), brass);
            leaf.localPosition = Vector3.up * room.doorTravel; leaves.Add(leaf);
            foreach (float x in new[] { -4.05f, 4.05f })
                Box(door, new Vector3(x, 3.9f, 0), new Vector3(.25f, 7.8f, .55f), brass);
        }
        room.doorLeaves = leaves.ToArray(); room.doorBlockers = blockers.ToArray();
    }
    static void Box(Transform parent, Vector3 pos, Vector3 size, Material material)
    {
        var obj = new GameObject("Gate ironwork"); obj.transform.SetParent(parent, false);
        obj.transform.localPosition = pos; obj.transform.localScale = size;
        obj.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        obj.AddComponent<MeshRenderer>().sharedMaterial = material;
    }
}
