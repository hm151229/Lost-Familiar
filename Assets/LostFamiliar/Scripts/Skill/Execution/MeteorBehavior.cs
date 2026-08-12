using System.Collections;
using UnityEngine;

namespace LostFamiliar.Battle
{
    public sealed class MeteorBehavior : ISkillBehavior
    {
        public IEnumerator Execute(SkillData skill, SkillExecutionContext context)
        {
            Vector3 center = context.GetDensestEnemyPosition(skill.radius);
            for (int i = 0; i < Mathf.Max(1, skill.projectileCount); i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * skill.radius * .55f;
                Vector3 impact = center + new Vector3(randomOffset.x, randomOffset.y, 0f);
                float impactDelay = .35f;
                GameObject meteor;
                if (skill.projectileEffectPrefab != null)
                {
                    meteor = context.CreateStationaryEffect(
                        skill.projectileEffectPrefab,
                        impact + skill.projectileSpawnOffset,
                        skill.projectileRotationOffset,
                        out _);
                    impactDelay = GetImpactDelay(meteor);
                }
                else
                {
                    meteor = context.CreatePrimitiveEffect(
                        impact, Vector3.one * skill.radius,
                        skill.effectColor, impactDelay, null);
                }

                context.StartRoutine(ResolveImpact(skill, context, impact, impactDelay, meteor));
                yield return new WaitForSeconds(.18f);
            }
        }

        private static IEnumerator ResolveImpact(
            SkillData skill, SkillExecutionContext context,
            Vector3 impact, float delay, GameObject meteor)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, delay));
            context.PlaySfx("SFX_Meteor_Impact", 1f);
            if (meteor != null)
                context.DestroyProjectile(meteor);
            if (skill.explosionEffectPrefab != null)
                context.CreateExplosionEffect(skill, impact);
            context.DamageArea(
                skill, impact, skill.radius, skill.damageMultiplier,
                null, false, true);
        }

        private static float GetImpactDelay(GameObject meteor)
        {
            if (meteor == null)
                return .35f;
            Transform hitController = FindChildByName(meteor.transform, "hit_controller");
            ParticleSystem particles = hitController != null
                ? hitController.GetComponent<ParticleSystem>()
                : null;
            return particles != null
                ? Mathf.Max(.01f, particles.main.startDelay.constantMax)
                : .8f;
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
                return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                    return child;
                Transform nested = FindChildByName(child, childName);
                if (nested != null)
                    return nested;
            }
            return null;
        }
    }
}
