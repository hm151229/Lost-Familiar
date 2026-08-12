using System.Collections;
using UnityEngine;

namespace LostFamiliar.Battle
{
    public sealed class MagicMissileBehavior : ISkillBehavior
    {
        public IEnumerator Execute(
            SkillData skill,
            SkillExecutionContext context)
        {
            int count = Mathf.Max(1, skill.projectileCount);

            for (int i = 0; i < count; i++)
            {
                EnemyActor target =
                    context.FindNearestEnemy(float.MaxValue);

                if (target == null)
                    yield break;

                yield return context.LaunchProjectile(
                    skill,
                    target,
                    skill.damageMultiplier,
                    0f,
                    skill.projectileTravelDuration);

                yield return new WaitForSeconds(.08f);
            }
        }
    }
}
