using System.Collections;

namespace LostFamiliar.Battle
{
    public interface ISkillBehavior
    {
        IEnumerator Execute(
            SkillData skill,
            SkillExecutionContext context);
    }
}
