using UnityEngine;

// Presentation only. Damage, weak-point collision and attack selection stay in the boss scripts.
public class DungeonGuardianVisuals : MonoBehaviour
{
    [System.Serializable]
    public class PartVisual
    {
        public DungeonBossPart part;
        public Renderer[] runes;
        [System.NonSerialized] public bool shattered;
    }

    public PartVisual[] parts;
    public Mesh fragmentMesh;
    public Material fragmentMaterial;
    MaterialPropertyBlock properties;

    void Awake() { properties = new MaterialPropertyBlock(); }

    void LateUpdate()
    {
        if (parts == null) return;
        foreach (var visual in parts)
        {
            if (visual.part == null) continue;
            bool broken = visual.part.Broken;
            float pulse = .8f + .2f * Mathf.Sin(Time.time * 2.5f + visual.part.transform.position.y);
            properties.Clear();
            properties.SetColor("_BaseColor", broken ? new Color(.12f, .13f, .14f) : new Color(1f, .24f, .035f));
            properties.SetColor("_EmissionColor", broken ? Color.black : new Color(1f, .12f, .008f) * pulse * 1.8f);
            foreach (var rune in visual.runes)
                if (rune != null) rune.SetPropertyBlock(properties);
            if (!broken || visual.shattered) continue;
            visual.shattered = true;
            ShedFragments(visual.part);
        }
    }

    void ShedFragments(DungeonBossPart part)
    {
        if (fragmentMesh == null || fragmentMaterial == null) return;
        Vector3 center = part.transform.position;
        // The head cap is already at the front; other weak points are centered in their limb.
        if (part.PartName != "Head") center -= transform.forward * (part.Body.lossyScale.z * .5f + .25f);
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * .25f;
            Vector3 radial = transform.right * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle);
            var fragment = new GameObject("Guardian Cosmetic Chip");
            fragment.transform.position = center + radial * .45f;
            fragment.transform.rotation = Quaternion.Euler(i * 37, i * 53, i * 19);
            fragment.transform.localScale = new Vector3(.16f, .23f, .12f) * (1f + i % 3 * .3f);
            fragment.AddComponent<MeshFilter>().sharedMesh = fragmentMesh;
            fragment.AddComponent<MeshRenderer>().sharedMaterial = fragmentMaterial;
            fragment.AddComponent<DungeonStoneFragment>().Initialize(transform,
                radial * 2.5f + Vector3.up * 2f - transform.forward * 3f);
        }
    }
}
