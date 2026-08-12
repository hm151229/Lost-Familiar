using System.Collections;
using UnityEngine;

namespace LostFamiliar.Battle
{
    public sealed class LightningBoltBehavior : ISkillBehavior
    {
        public IEnumerator Execute(SkillData skill, SkillExecutionContext context)
        {
            EnemyActor target = context.GetRandomEnemy();
            if (target == null)
                yield break;

            Vector3 point = target.AimPosition;
            float effectLifetime;
            if (skill.projectileEffectPrefab != null)
            {
                context.CreateStationaryEffect(
                    skill.projectileEffectPrefab,
                    point + skill.projectileSpawnOffset,
                    skill.projectileRotationOffset,
                    out effectLifetime);
            }
            else
            {
                effectLifetime = .18f;
                context.CreatePrimitiveEffect(
                    point + Vector3.up * 1.5f,
                    new Vector3(.25f, 3f, .25f),
                    skill.effectColor,
                    effectLifetime,
                    null);
            }

            context.PlayLoop("SFX_LightningBolt_Cast", effectLifetime, 1f);

            float requestedInterval = Mathf.Max(.05f, skill.tickInterval);
            int tickCount = Mathf.Max(1, Mathf.CeilToInt(effectLifetime / requestedInterval));
            float actualInterval = effectLifetime / tickCount;
            float damagePerTick = skill.damageMultiplier / tickCount;
            for (int tick = 0; tick < tickCount; tick++)
            {
                context.DamageArea(skill, point, skill.radius, damagePerTick, null, true, true);
                if (tick + 1 < tickCount)
                    yield return new WaitForSeconds(actualInterval);
            }
        }
    }
}
