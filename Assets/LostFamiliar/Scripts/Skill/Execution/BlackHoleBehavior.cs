using System.Collections;
using System.Linq;
using UnityEngine;

namespace LostFamiliar.Battle
{
    public sealed class BlackHoleBehavior : ISkillBehavior
    {
        public IEnumerator Execute(SkillData skill, SkillExecutionContext context)
        {
            context.PlayLoop("SFX_BlackHole_Loop", skill.duration, 1f);
            Vector3 center = context.GetDensestEnemyPosition(skill.radius);
            if (skill.worldAreaEffectPrefab != null)
            {
                float effectLifetime = skill.worldAreaEffectLifetime > 0f
                    ? skill.worldAreaEffectLifetime
                    : skill.duration + .25f;
                context.CreatePrefabEffect(
                    skill.worldAreaEffectPrefab,
                    center + skill.worldAreaEffectOffset,
                    Quaternion.Euler(skill.worldAreaEffectRotation),
                    effectLifetime,
                    SkillExecutionContext.DefaultEffectSortingOrder);
            }
            else
            {
                context.CreatePrimitiveEffect(
                    center, Vector3.one * skill.radius * 1.4f,
                    skill.effectColor, skill.duration + .25f, null);
            }

            float interval = Mathf.Max(.05f, skill.tickInterval);
            int tickCount = Mathf.Max(1, Mathf.CeilToInt(skill.duration / interval));
            float damagePerTick = skill.damageMultiplier / tickCount;
            float elapsed = 0f;
            float damageTimer = 0f;
            int appliedTicks = 0;
            while (elapsed < skill.duration)
            {
                yield return null;
                float deltaTime = Mathf.Min(Time.deltaTime, skill.duration - elapsed);
                elapsed += deltaTime;
                damageTimer += deltaTime;

                int damageTicksThisFrame = 0;
                while (damageTimer + .0001f >= interval && appliedTicks < tickCount)
                {
                    damageTimer -= interval;
                    appliedTicks++;
                    damageTicksThisFrame++;
                }

                foreach (EnemyActor enemy in EnemyActor.Active.ToArray())
                {
                    if (enemy == null || enemy.CombatGroup != context.CombatGroup ||
                        Vector3.Distance(center, enemy.transform.position) > skill.radius)
                        continue;

                    float distanceToCenter = Vector3.Distance(enemy.transform.position, center);
                    float pullEase = 1f - Mathf.Exp(
                        -Mathf.Max(.01f, skill.pullStrength) * .45f * deltaTime);
                    enemy.PullTowards(center, distanceToCenter * pullEase, deltaTime + .05f);

                    Vector3 closestPoint = enemy.VisualBounds.ClosestPoint(center);
                    if (Vector3.Distance(center, closestPoint) > skill.blackHoleDamageRadius)
                        continue;

                    for (int tick = 0; tick < damageTicksThisFrame; tick++)
                        context.DealDamage(skill, enemy, damagePerTick, null, true, false);
                }
            }

            context.DamageArea(
                skill, center, skill.radius, skill.secondaryDamageMultiplier,
                null, true, false);
        }
    }
}
