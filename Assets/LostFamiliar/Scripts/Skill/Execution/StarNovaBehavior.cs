using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LostFamiliar.Battle
{
    public sealed class StarNovaBehavior : ISkillBehavior
    {
        public IEnumerator Execute(SkillData skill, SkillExecutionContext context)
        {
            const float chargeDuration = 1.5f;
            Transform playerTransform = context.PlayerTransform;
            GameObject playerAreaEffect = null;
            if (skill.playerAreaEffectPrefab != null)
            {
                float fullSkillLifetime = chargeDuration +
                    Mathf.Max(.05f, skill.explosionEffectLifetime) +
                    Mathf.Max(.05f, skill.projectileTravelDuration) + .25f;
                float effectLifetime = Mathf.Max(skill.playerAreaEffectLifetime, fullSkillLifetime);
                playerAreaEffect = context.CreatePrefabEffect(
                    skill.playerAreaEffectPrefab,
                    playerTransform.position + skill.playerAreaEffectOffset,
                    Quaternion.identity,
                    effectLifetime,
                    SkillExecutionContext.PlayerAreaEffectSortingOrder);
                playerAreaEffect.transform.SetParent(playerTransform, false);
                playerAreaEffect.transform.localPosition = skill.playerAreaEffectOffset;
                playerAreaEffect.transform.localRotation = Quaternion.identity;
                context.RegisterPlayerAttachedEffect(playerAreaEffect);
            }

            yield return new WaitForSeconds(chargeDuration);
            context.PlaySfx("SFX_StarNova_Explosion", 1f);

            GameObject explosionEffect = skill.explosionEffectPrefab != null
                ? context.CreateExplosionEffect(skill, playerTransform.position)
                : context.CreatePrimitiveEffect(
                    playerTransform.position,
                    Vector3.one * skill.radius * 1.8f,
                    skill.effectColor,
                    skill.explosionEffectLifetime,
                    null);

            yield return new WaitForSeconds(Mathf.Max(.05f, skill.explosionEffectLifetime));
            if (explosionEffect != null)
                context.DestroyProjectile(explosionEffect);

            context.PlaySfx("SFX_StarNova_Fragment_Fly", 1f);

            List<EnemyActor> fallbackTargets = new();
            foreach (EnemyActor enemy in EnemyActor.Active.ToArray())
            {
                if (enemy != null && enemy.Health > 0f && enemy.CombatGroup == context.CombatGroup)
                    fallbackTargets.Add(enemy);
            }

            context.DamageArea(
                skill, playerTransform.position, skill.radius, skill.damageMultiplier,
                null, true, true);

            List<EnemyActor> targets = new();
            foreach (EnemyActor enemy in EnemyActor.Active.ToArray())
            {
                if (enemy != null && enemy.Health > 0f && enemy.CombatGroup == context.CombatGroup)
                    targets.Add(enemy);
            }

            targets.Sort((left, right) =>
                Vector3.SqrMagnitude(left.transform.position - playerTransform.position).CompareTo(
                    Vector3.SqrMagnitude(right.transform.position - playerTransform.position)));

            if (targets.Count == 0)
                targets.AddRange(fallbackTargets);

            if (targets.Count == 0)
            {
                ClearPlayerEffect(context, playerAreaEffect);
                yield break;
            }

            int count = Mathf.Max(1, skill.projectileCount);
            Vector3 projectileOrigin = playerTransform.position;
            for (int i = 0; i < count; i++)
            {
                EnemyActor target = targets[i % targets.Count];
                context.StartRoutine(context.LaunchDirectProjectile(
                    skill,
                    target,
                    projectileOrigin,
                    skill.secondaryDamageMultiplier,
                    SkillExecutionContext.StarNovaProjectileSortingOrder));
            }

            yield return new WaitForSeconds(Mathf.Max(.05f, skill.projectileTravelDuration));
            ClearPlayerEffect(context, playerAreaEffect);
        }

        private static void ClearPlayerEffect(
            SkillExecutionContext context, GameObject playerAreaEffect)
        {
            if (playerAreaEffect == null)
                return;
            context.DestroyProjectile(playerAreaEffect);
            context.UnregisterPlayerAttachedEffect(playerAreaEffect);
        }
    }
}
