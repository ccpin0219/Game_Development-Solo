using UnityEngine;

// Short-lived, collider-free effects. All lines share the existing transparent trail material.
public class DungeonPowerShotVfx : MonoBehaviour
{
    LineRenderer ring;
    LineRenderer[] sparks;
    Light flash;
    float age;
    bool impact;
    const float Lifetime = .24f;

    public static LineRenderer Line(Transform parent, string name, float width, Color color, int count)
    {
        var obj = new GameObject(name); obj.transform.SetParent(parent, false);
        var line = obj.AddComponent<LineRenderer>();
        line.useWorldSpace = false; line.positionCount = count;
        line.sharedMaterial = Resources.Load<Material>("ArrowTrail");
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = color;
        line.numCapVertices = 3;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }
    public static void AddBolt(Transform arrow)
    {
        var root = new GameObject("Power Bolt").transform; root.SetParent(arrow, false);
        var aura = Line(root, "Crimson Lance", .2f, new Color(2, .04f, .01f, .75f), 2);
        aura.SetPosition(0, new Vector3(0, 0, -1.15f)); aura.SetPosition(1, new Vector3(0, 0, .55f));
        var core = Line(root, "Incandescent Core", .055f, new Color(4, 2.3f, 1.4f, 1), 2);
        core.SetPosition(0, new Vector3(0, 0, -.85f)); core.SetPosition(1, new Vector3(0, 0, .65f));
    }
    public static void Burst(Vector3 position, Vector3 normal, bool onImpact)
    {
        var obj = new GameObject(onImpact ? "Power Impact" : "Power Shot Flash");
        obj.transform.SetPositionAndRotation(position + normal * .04f, Quaternion.LookRotation(normal));
        var effect = obj.AddComponent<DungeonPowerShotVfx>(); effect.impact = onImpact;
        effect.ring = Line(obj.transform, "Expanding Shock Ring", .06f, Color.red, 40);
        effect.ring.loop = true;
        effect.sparks = new LineRenderer[onImpact ? 10 : 6];
        for (int i = 0; i < effect.sparks.Length; i++)
            effect.sparks[i] = Line(obj.transform, "Hot Spark", .045f, Color.white, 2);
        effect.flash = obj.AddComponent<Light>();
        effect.flash.color = new Color(1, .12f, .025f); effect.flash.range = onImpact ? 5 : 3;
        effect.flash.shadows = LightShadows.None;
        effect.Pose(0);
        Destroy(obj, Lifetime + .05f);
    }
    void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;
        age += Time.deltaTime; Pose(Mathf.Clamp01(age / Lifetime));
        if (age >= Lifetime) Destroy(gameObject);
    }
    void Pose(float t)
    {
        float radius = Mathf.Lerp(.12f, impact ? 1.1f : .55f, t);
        var red = new Color(2, .08f, .015f, (1 - t) * .8f);
        ring.startColor = ring.endColor = red;
        ring.startWidth = ring.endWidth = .06f * (1 - t);
        for (int i = 0; i < ring.positionCount; i++)
        {
            float a = i * Mathf.PI * 2 / ring.positionCount;
            ring.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius);
        }
        for (int i = 0; i < sparks.Length; i++)
        {
            float a = i * Mathf.PI * 2 / sparks.Length;
            var direction = new Vector3(Mathf.Cos(a), Mathf.Sin(a), .35f);
            float reach = radius * (1.2f + (i % 3) * .25f);
            sparks[i].SetPosition(0, direction * reach * .65f);
            sparks[i].SetPosition(1, direction * reach);
            sparks[i].startColor = sparks[i].endColor = new Color(3, 1.1f, .25f, 1 - t);
        }
        flash.intensity = 4 * (1 - t) * (1 - t);
    }
}
