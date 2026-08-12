using System.Collections;

namespace LostFamiliar.Battle
{
    public sealed class FireBallBehavior : ISkillBehavior
    {
        public IEnumerator Execute(
            SkillData skill,
            SkillExecutionContext context)
        {
            EnemyActor target =
                context.FindNearestEnemy(float.MaxValue);

            if (target == null)
                yield break;

            yield return context.LaunchProjectile(
                skill,
                target,
                skill.damageMultiplier,
                skill.radius,
                skill.projectileTravelDuration);
        }
    }
}
