using UnityEngine;

/// <summary>
/// Процедурная анимация <see cref="AvatarRig"/>: покой с дыханием, ходьба/бег (мах рук и ног, сгиб колен,
/// подпрыгивание таза, наклон на бегу), прыжок, взмах рукой при стройке/сносе, голова следует за взглядом.
/// Без клипов и Animator — суставы крутятся напрямую, переходы сглажены.
/// Угол по X у конечности: минус — вперёд, плюс — назад (конечность смотрит вниз).
/// </summary>
public class AvatarAnimator
{
    public struct Motion
    {
        /// <summary>0 — стоит, 1 — шаг, 2 — бег.</summary>
        public float speed01;
        public bool grounded;
        public float lookPitch;
    }

    const float ActionTime = 0.38f;

    readonly AvatarRig rig;
    float phase;
    float moveWeight;
    float runWeight;
    float airWeight;
    float time;
    float action = -1f;
    float lookPitch;

    public AvatarAnimator(AvatarRig rig)
    {
        this.rig = rig;
    }

    /// <summary>Взмах рукой: поставил или снёс здание.</summary>
    public void TriggerAction()
    {
        action = 0f;
    }

    public void Tick(float dt, Motion m)
    {
        if (rig == null || rig.hips == null)
            return;
        dt = Mathf.Min(dt, 0.1f);
        time += dt;

        float k = 1f - Mathf.Exp(-10f * dt);
        float targetMove = m.grounded ? Mathf.Clamp01(m.speed01) : 0f;
        float targetRun = m.grounded ? Mathf.Clamp01(m.speed01 - 1f) : 0f;
        moveWeight = Mathf.Lerp(moveWeight, targetMove, k);
        runWeight = Mathf.Lerp(runWeight, targetRun, k);
        airWeight = Mathf.Lerp(airWeight, m.grounded ? 0f : 1f, 1f - Mathf.Exp(-12f * dt));
        lookPitch = Mathf.Lerp(lookPitch, Mathf.Clamp(m.lookPitch, -70f, 70f), 1f - Mathf.Exp(-14f * dt));

        // Шаг: ~1.7 Гц пешком, ~2.6 Гц бегом. Фаза идёт, только пока движемся.
        float freq = Mathf.Lerp(1.7f, 2.6f, runWeight);
        phase += dt * freq * Mathf.PI * 2f * Mathf.Max(moveWeight, 0.001f);
        float sin = Mathf.Sin(phase);
        float cos = Mathf.Cos(phase);

        float legAmp = Mathf.Lerp(28f, 46f, runWeight) * moveWeight;
        float armAmp = Mathf.Lerp(24f, 50f, runWeight) * moveWeight;
        float breathe = Mathf.Sin(time * 1.7f);

        // ----- таз и корпус -----
        float bob = Mathf.Abs(sin) * Mathf.Lerp(0.03f, 0.06f, runWeight) * moveWeight;
        rig.hips.localPosition = new Vector3(0f, rig.hipHeight + bob - 0.06f * airWeight, 0f);
        rig.hips.localRotation = Quaternion.Euler(0f, sin * 6f * moveWeight, 0f);
        float lean = 3f * moveWeight + 9f * runWeight + breathe * 1.2f * (1f - moveWeight);
        rig.spine.localRotation = Quaternion.Euler(lean, -sin * 8f * moveWeight, 0f);

        // ----- ноги -----
        float legL = -sin * legAmp;
        float legR = sin * legAmp;
        float kneeL = Mathf.Max(0f, cos) * Mathf.Lerp(30f, 60f, runWeight) * moveWeight;
        float kneeR = Mathf.Max(0f, -cos) * Mathf.Lerp(30f, 60f, runWeight) * moveWeight;
        // В прыжке ноги поджаты, одна чуть впереди.
        legL = Mathf.Lerp(legL, -32f, airWeight);
        legR = Mathf.Lerp(legR, -8f, airWeight);
        kneeL = Mathf.Lerp(kneeL, 55f, airWeight);
        kneeR = Mathf.Lerp(kneeR, 35f, airWeight);
        rig.thighL.localRotation = Quaternion.Euler(legL, 0f, 0f);
        rig.thighR.localRotation = Quaternion.Euler(legR, 0f, 0f);
        rig.kneeL.localRotation = Quaternion.Euler(kneeL, 0f, 0f);
        rig.kneeR.localRotation = Quaternion.Euler(kneeR, 0f, 0f);

        // ----- руки (против ног) -----
        float idleOut = 4f + breathe * 1.5f;
        float armOut = Mathf.Lerp(idleOut, 38f, airWeight);
        float armL = sin * armAmp;
        float armR = -sin * armAmp;
        float elbowBend = Mathf.Lerp(12f, 70f, runWeight) * moveWeight + 8f * (1f - moveWeight);
        armL = Mathf.Lerp(armL, -25f, airWeight);
        armR = Mathf.Lerp(armR, -25f, airWeight);
        Quaternion shoulderL = Quaternion.Euler(armL, 0f, -armOut);
        Quaternion shoulderR = Quaternion.Euler(armR, 0f, armOut);
        Quaternion elbowL = Quaternion.Euler(-elbowBend, 0f, 0f);
        Quaternion elbowR = Quaternion.Euler(-elbowBend, 0f, 0f);

        // Взмах правой: замах назад-вверх и удар вперёд-вниз.
        if (action >= 0f)
        {
            action += dt;
            float t = Mathf.Clamp01(action / ActionTime);
            float swing = t < 0.35f
                ? Mathf.Lerp(0f, -150f, Mathf.SmoothStep(0f, 1f, t / 0.35f))
                : Mathf.Lerp(-150f, -40f, Mathf.SmoothStep(0f, 1f, (t - 0.35f) / 0.65f));
            float weight = t < 0.85f ? 1f : 1f - (t - 0.85f) / 0.15f;
            shoulderR = Quaternion.Slerp(shoulderR, Quaternion.Euler(swing, 0f, 12f), weight);
            elbowR = Quaternion.Slerp(elbowR, Quaternion.Euler(-35f, 0f, 0f), weight);
            if (action >= ActionTime)
                action = -1f;
        }

        rig.shoulderL.localRotation = shoulderL;
        rig.shoulderR.localRotation = shoulderR;
        rig.elbowL.localRotation = elbowL;
        rig.elbowR.localRotation = elbowR;

        // ----- голова: половина наклона взгляда, корпус уже наклонён -----
        float headPitch = lookPitch * 0.6f - lean * 0.5f;
        rig.head.localRotation = Quaternion.Euler(headPitch, sin * 4f * moveWeight, 0f);
    }
}
