using UnityEngine;

// Decorative motion only; attack timing and projectile origins remain with DungeonFlameTraps.
public class DungeonBrazierAmbience : MonoBehaviour
{
    public Transform flame;
    public Light fireLight;
    Vector3 restScale;
    float phase;

    void Awake()
    {
        if (flame != null) restScale = flame.localScale;
        phase = transform.position.x * .71f + transform.position.z * 1.13f;
    }

    void Update()
    {
        float wave = Mathf.Sin(Time.time * 7f + phase);
        float flutter = Mathf.Sin(Time.time * 13f + phase * 2f);
        if (flame != null)
        {
            flame.localScale = Vector3.Scale(restScale,
                new Vector3(1f + wave * .035f, 1f + wave * .07f + flutter * .025f, 1f - wave * .035f));
            flame.localRotation = Quaternion.Euler(0f, wave * 7f, flutter * 2f);
        }
        if (fireLight != null) fireLight.intensity = 4.5f + wave * .35f + flutter * .15f;
    }
}
