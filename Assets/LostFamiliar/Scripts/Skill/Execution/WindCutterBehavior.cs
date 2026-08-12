using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LostFamiliar.Battle
{
    public sealed class WindCutterBehavior : ISkillBehavior
    {
        public IEnumerator Execute(SkillData skill, SkillExecutionContext context)
        {
            context.PlaySfx("SFX_WindCutter_Fly", 1f);
            int count = Mathf.Max(1, skill.projectileCount);
            float halfDistance = Mathf.Max(6f, skill.radius);
            float travelDuration = Mathf.Max(.05f, skill.projectileTravelDuration);
            Vector3 center = context.GetDensestEnemyPosition(skill.radius);
            const float laneSpacing = 1.1f;
            List<GameObject> projectiles = new(count);
            List<Vector3> startPositions = new(count);
            List<Vector3> endPositions = new(count);
            List<Vector3> previousPositions = new(count);
            List<HashSet<EnemyActor>> hitByProjectile = new(count);

            for (int i = 0; i < count; i++)
            {
                float laneOffset = (i - (count - 1) * .5f) * laneSpacing;
                Vector3 start = center + new Vector3(-halfDistance, laneOffset, 0f) + skill.projectileSpawnOffset;
                Vector3 end = center + new Vector3(halfDistance, laneOffset, 0f) + skill.projectileSpawnOffset;
                GameObject projectile = skill.projectileEffectPrefab != null
                    ? context.CreatePrefabEffect(
                        skill.projectileEffectPrefab,
                        start,
                        context.GetProjectileRotation(start, end, skill.projectileRotationOffset),
                        travelDuration + .1f,
                        SkillExecutionContext.DefaultEffectSortingOrder)
                    : context.CreatePrimitiveEffect(
                        start, Vector3.one * .3f, skill.effectColor,
                        travelDuration + .1f, null);

                projectiles.Add(projectile);
                startPositions.Add(start);
                endPositions.Add(end);
                previousPositions.Add(start);
                hitByProjectile.Add(new HashSet<EnemyActor>());
            }

            float elapsed = 0f;
            while (elapsed < travelDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / travelDuration);
                for (int i = 0; i < projectiles.Count; i++)
                {
                    GameObject projectile = projectiles[i];
                    if (projectile == null)
                        continue;
                    Vector3 previous = previousPositions[i];
                    Vector3 current = Vector3.Lerp(startPositions[i], endPositions[i], progress);
                    projectile.transform.position = current;
                    previousPositions[i] = current;

                    foreach (EnemyActor enemy in EnemyActor.Active.ToArray())
                    {
                        if (enemy == null || enemy.CombatGroup != context.CombatGroup ||
                            hitByProjectile[i].Contains(enemy))
                            continue;
                        if (!context.SegmentIntersectsEnemy(
                                previous, current, enemy, skill.projectileImpactDistance))
                            continue;
                        hitByProjectile[i].Add(enemy);
                        context.DealDamage(skill, enemy, skill.damageMultiplier, null, true, true);
                    }
                }
                yield return null;
            }

            foreach (GameObject projectile in projectiles)
                if (projectile != null) context.DestroyProjectile(projectile);
        }
    }
}
