using System.Collections;
using UnityEngine;

namespace LostFamiliar.Battle
{
    public sealed class ArcaneOrbBehavior : ISkillBehavior
    {
        public IEnumerator Execute(SkillData skill, SkillExecutionContext context)
        {
            context.PlayLoop("SFX_ArcaneOrb_Loop", skill.duration, 1f);
            bool usesPrefab = skill.playerAreaEffectPrefab != null;
            float effectLifetime = skill.playerAreaEffectLifetime > 0f
                ? skill.playerAreaEffectLifetime
                : skill.duration + .25f;
            Vector3 playerPosition = context.PlayerTransform.position;
            GameObject orb = usesPrefab
                ? context.CreatePrefabEffect(
                    skill.playerAreaEffectPrefab,
                    playerPosition + skill.playerAreaEffectOffset,
                    Quaternion.identity,
                    effectLifetime,
                    SkillExecutionContext.PlayerAreaEffectSortingOrder)
                : context.CreatePrimitiveEffect(
                    playerPosition, Vector3.one * .45f, skill.effectColor,
                    skill.duration + .25f, null);

            float elapsed = 0f;
            float interval = Mathf.Max(.05f, skill.tickInterval);
            while (elapsed < skill.duration)
            {
                playerPosition = context.PlayerTransform.position;
                if (orb != null)
                {
                    if (usesPrefab)
                        orb.transform.position = playerPosition + skill.playerAreaEffectOffset;
                    else
                    {
                        float angle = elapsed * 240f * Mathf.Deg2Rad;
                        orb.transform.position = playerPosition +
                            new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 1.2f;
                    }
                }

                EnemyActor target = FindNearestEnemy(
                    playerPosition, Mathf.Max(1f, skill.radius), context.CombatGroup);
                if (target != null)
                {
                    Vector3 shotOrigin = orb != null
                        ? orb.transform.position
                        : playerPosition + skill.playerAreaEffectOffset;
                    context.StartRoutine(context.LaunchDirectProjectile(
                        skill, target, shotOrigin, skill.damageMultiplier,
                        SkillExecutionContext.DefaultEffectSortingOrder));
                }

                yield return new WaitForSeconds(interval);
                elapsed += interval;
            }
        }

        private static EnemyActor FindNearestEnemy(Vector3 center, float range, int combatGroup)
        {
            EnemyActor nearest = null;
            float nearestDistance = range * range;
            foreach (EnemyActor enemy in EnemyActor.Active)
            {
                if (enemy == null || enemy.CombatGroup != combatGroup)
                    continue;
                float distance = (enemy.transform.position - center).sqrMagnitude;
                if (distance >= nearestDistance)
                    continue;
                nearestDistance = distance;
                nearest = enemy;
            }
            return nearest;
        }
    }
}
