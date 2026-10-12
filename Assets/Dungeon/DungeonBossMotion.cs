using UnityEngine;

// Samples the combat clocks: pause, overlapping attacks and cancellations share one pose owner.
// Unit-scale joints avoid rotating the non-uniformly scaled stone blocks about their centers.
public sealed class DungeonBossMotion
{
    readonly DungeonBoss boss;
    readonly Transform leftArm, rightArm, leftLeg, rightLeg, head;
    readonly Transform weapon;
    readonly Vector3 restPosition;
    readonly Quaternion restRotation;
    float clock;

    public DungeonBossMotion(DungeonBoss owner, Transform hammer)
    {
        boss = owner;
        weapon = hammer;
        restPosition = boss.upperBody.localPosition;
        restRotation = boss.upperBody.localRotation;
        leftArm = Joint("Left Arm", .45f);
        rightArm = Joint("Right Arm", .45f);
        leftLeg = Joint("Left Leg", .48f);
        rightLeg = Joint("Right Leg", .48f);
        head = Joint("Head", -.42f);
        // Keep the weapon in the hand while the shoulder moves; its existing swing remains additive.
        if (rightArm != null) hammer.SetParent(rightArm, true);
    }

    Transform Joint(string label, float height)
    {
        foreach (var part in boss.Parts)
        {
            if (part.PartName != label) continue;
            var body = part.Body;
            var joint = new GameObject(label + " Motion Joint").transform;
            joint.SetParent(body.parent, false);
            joint.position = body.TransformPoint(new Vector3(0, height, 0));
            joint.rotation = body.parent.rotation;
            body.SetParent(joint, true);
            return joint;
        }
        return null;
    }

    bool Intact(string label)
    {
        foreach (var part in boss.Parts)
            if (part.PartName == label) return !part.Broken;
        return false;
    }

    static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }

    // Lift, hold, then plant exactly when the attack resolves. Follow through over its red-area window.
    static float Stomp(float t, float impact)
    {
        if (t < impact * .7f) return Ease(t / (impact * .7f));
        if (t < impact - .16f) return 1;
        if (t < impact) return 1 - Ease((t - impact + .16f) / .16f);
        return -.12f * Mathf.Sin(Mathf.Clamp01((t - impact) / .3f) * Mathf.PI);
    }

    static void Rotate(Transform joint, Vector3 euler, float blend)
    {
        if (joint != null) joint.localRotation = Quaternion.Slerp(joint.localRotation, Quaternion.Euler(euler), blend);
    }

    public void Apply(float dt, bool ended, int hammerPhase, float hammerTime, float impactTimer,
        DungeonRockVolley volley, DungeonBossSpecialAttacks line, DungeonBossSpecialAttacks circle,
        DungeonBossSpecialAttacks laser, DungeonBossFloorAttack floor, DungeonBossLeap leap)
    {
        if (dt <= 0) return;
        clock += dt;
        Vector3 la = Vector3.zero, ra = Vector3.zero, ll = Vector3.zero, rl = Vector3.zero, neck = Vector3.zero;
        Vector3 body = Vector3.zero;
        float dip = 0;
        if (!ended)
        {
            if (hammerPhase != 0 && Intact("Right Arm"))
            {
                float lift = hammerPhase == 1 ? Ease(hammerTime / boss.windupSeconds) : 1 - Ease(hammerTime / .18f);
                ra = new Vector3(24 * lift, -8 * lift, -12 * lift);
                body += new Vector3(-9, -9, -4) * lift;
                dip -= .3f * lift;
            }
            if (impactTimer > 0)
            {
                float recoil = Mathf.Sin(Mathf.Clamp01(impactTimer / .3f) * Mathf.PI);
                body.x += 7 * recoil;
                dip -= .45f * recoil;
            }
            if (volley != null && Intact("Left Arm"))
            {
                float t = volley.Elapsed;
                float throwPose = t < .6f ? Ease(t / .6f) : 1 - Ease((t - .6f) / .8f);
                la = new Vector3(72 * throwPose, 12 * throwPose, 22 * throwPose);
                body.y += 9 * throwPose;
            }
            if (line.ActivePattern != "" && Intact("Left Leg"))
            {
                float lift = Stomp(line.Elapsed, 1.2f);
                ll.x = 32 * lift;
                body.z -= 5 * lift;
                dip -= .18f * lift;
            }
            if (circle.ActivePattern != "" && Intact("Right Leg"))
            {
                float lift = Stomp(circle.Elapsed, 1.5f);
                rl = new Vector3(38, 0, -9) * lift;
                body.z += 6 * lift;
                dip -= .25f * lift;
            }
            if (laser.ActivePattern != "" && Intact("Head"))
            {
                float charge = Ease(laser.Elapsed / 1.1f);
                float firing = laser.Elapsed >= 1.1f ? 1 : 0;
                neck.x = -12 * charge + firing * 16;
                body.x -= 3 * charge;
                neck.z = firing * Mathf.Sin(clock * 22) * .6f;
            }
            if (floor.Active && Intact("Torso"))
            {
                float channel = floor.Dangerous ? 1 - Ease((floor.Elapsed - floor.warningSeconds) / floor.activeSeconds)
                    : Ease(floor.Elapsed / floor.warningSeconds);
                // Add restrained open-arm channeling so concurrent arm attacks retain their silhouette.
                la.z += 13 * channel; ra.z -= 13 * channel;
                body.x += 6 * channel; dip -= .45f * channel;
            }
            if (leap.Active)
            {
                float t = leap.Elapsed;
                float crouch = t < leap.preparation ? Ease(t / leap.preparation) : 0;
                float flight = Mathf.Clamp01((t - leap.preparation) / leap.flightTime);
                float tuck = Mathf.Sin(flight * Mathf.PI);
                float landing = t >= leap.preparation + leap.flightTime
                    ? 1 - Ease((t - leap.preparation - leap.flightTime) / .35f) : 0;
                dip = -.85f * crouch - .8f * landing;
                body = new Vector3(9 * crouch - 6 * tuck + 10 * landing, 0, 0);
                la = new Vector3(25 * tuck, 0, 18 * (crouch + tuck));
                ra = new Vector3(15 * tuck, 0, -18 * (crouch + tuck));
                ll.x = rl.x = 22 * tuck;
            }
        }
        float blend = 1 - Mathf.Exp(-24 * dt);
        if (ended || !Intact("Right Arm"))
            weapon.localRotation = Quaternion.Slerp(weapon.localRotation, Quaternion.identity, blend);
        Rotate(leftArm, Intact("Left Arm") ? la : Vector3.zero, blend);
        Rotate(rightArm, Intact("Right Arm") ? ra : Vector3.zero, blend);
        Rotate(leftLeg, Intact("Left Leg") ? ll : Vector3.zero, blend);
        Rotate(rightLeg, Intact("Right Leg") ? rl : Vector3.zero, blend);
        Rotate(head, Intact("Head") ? neck : Vector3.zero, blend);
        // The existing defeat animation owns the upper body after death.
        if (boss.Defeated) return;
        body.x = Mathf.Clamp(body.x, -14, 14);
        body.y = Mathf.Clamp(body.y, -14, 14);
        body.z = Mathf.Clamp(body.z, -9, 9);
        var target = restPosition + Vector3.up * (dip + (ended ? 0 : Mathf.Sin(clock * 1.3f) * .09f));
        boss.upperBody.localPosition = Vector3.Lerp(boss.upperBody.localPosition, target, blend);
        boss.upperBody.localRotation = Quaternion.Slerp(boss.upperBody.localRotation, restRotation * Quaternion.Euler(body), blend);
    }
}
