using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LostFamiliar.Battle
{
    public sealed class IceSpearBehavior : ISkillBehavior
    {
        public IEnumerator Execute(SkillData skill, SkillExecutionContext context)
        {
            context.PlaySfx("SFX_IceSpear_Cast", 1f);
            Vector3 origin = context.FirePoint != null
                ? context.FirePoint.position
                : context.PlayerTransform.position;
            Vector3 forward = context.GetFacingDirection();
            const float halfArc = 38f;
            int count = Mathf.Max(1, skill.projectileCount);
            float distance = Mathf.Max(6f, skill.radius);
            float travelDuration = Mathf.Max(.05f, skill.projectileTravelDuration);
            HashSet<EnemyActor> hit = new();
            List<GameObject> projectiles = new(count);
            List<Vector3> directions = new(count);
            List<Vector3> spawnPositions = new(count);
            List<Vector3> previousPositions = new(count);

            for (int i = 0; i < count; i++)
            {
                float angle = count <= 1 ? 0f : Mathf.Lerp(-halfArc, halfArc, i / (float)(count - 1));
                Vector3 direction = Quaternion.Euler(0f, 0f, angle) * forward;
                Vector3 spawnPosition = origin + skill.projectileSpawnOffset;
                GameObject projectile = skill.projectileEffectPrefab != null
                    ? context.CreatePrefabEffect(
                        skill.projectileEffectPrefab,
                        spawnPosition,
                        context.GetProjectileRotation(
                            spawnPosition, spawnPosition + direction, skill.projectileRotationOffset),
                        travelDuration + .1f,
                        SkillExecutionContext.DefaultEffectSortingOrder)
                    : context.CreatePrimitiveEffect(
                        spawnPosition, Vector3.one * .3f, skill.effectColor,
                        travelDuration + .1f, null);

                projectiles.Add(projectile);
                directions.Add(direction.normalized);
                spawnPositions.Add(spawnPosition);
                previousPositions.Add(spawnPosition);
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
                    Vector3 current = Vector3.Lerp(
                        spawnPositions[i], spawnPositions[i] + directions[i] * distance, progress);
                    projectile.transform.position = current;
                    previousPositions[i] = current;

                    foreach (EnemyActor enemy in EnemyActor.Active.ToArray())
                    {
                        if (enemy == null || enemy.CombatGroup != context.CombatGroup || hit.Contains(enemy))
                            continue;
                        if (!context.SegmentIntersectsEnemy(
                                previous, current, enemy, skill.projectileImpactDistance))
                            continue;
                        hit.Add(enemy);
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
