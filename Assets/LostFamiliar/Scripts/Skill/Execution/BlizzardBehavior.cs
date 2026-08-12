using System.Collections;
using System.Linq;
using UnityEngine;

namespace LostFamiliar.Battle
{
    public sealed class BlizzardBehavior : ISkillBehavior
    {
        public IEnumerator Execute(SkillData skill, SkillExecutionContext context)
        {
            context.PlayLoop("SFX_Blizzard_Loop", skill.duration, 1f);
            Vector3 center = context.GetDensestEnemyPosition(skill.radius);
            if (skill.worldAreaEffectPrefab != null)
            {
                float effectLifetime = skill.worldAreaEffectLifetime > 0f
                    ? skill.worldAreaEffectLifetime
                    : skill.duration;
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
                    center, Vector3.one * skill.radius * 1.6f,
                    skill.effectColor, skill.duration, null);
            }

            float interval = Mathf.Max(.05f, skill.tickInterval);
            for (float elapsed = 0f; elapsed < skill.duration; elapsed += interval)
            {
                foreach (EnemyActor enemy in EnemyActor.Active.ToArray())
                {
                    if (enemy == null || enemy.CombatGroup != context.CombatGroup ||
                        Vector3.Distance(center, enemy.transform.position) > skill.radius)
                        continue;
                    context.DealDamage(skill, enemy, skill.damageMultiplier, null, true, true);
                    enemy.ApplySlow(skill.slowPercent, interval + .1f);
                }
                yield return new WaitForSeconds(interval);
            }
        }
    }
}
