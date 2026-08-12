using System;
using UnityEngine;

namespace LostFamiliar.Battle
{
    public sealed class PlayerSkillController
    {
        private SkillData[] _equippedSkills =
            Array.Empty<SkillData>();

        private int[] _skillLevels =
            Array.Empty<int>();

        private float[] _skillTimers =
            Array.Empty<float>();

        public SkillData[] EquippedSkills =>
            _equippedSkills;

        public int Count =>
            _equippedSkills?.Length ?? 0;

        public void SetEquippedSkills(
            SkillData[] skills,
            int[] levels = null)
        {
            _equippedSkills =
                skills ?? Array.Empty<SkillData>();

            _skillLevels =
                new int[_equippedSkills.Length];

            for (int i = 0;
                 i < _skillLevels.Length;
                 i++)
            {
                _skillLevels[i] =
                    levels != null &&
                    i < levels.Length
                        ? Mathf.Max(1, levels[i])
                        : 1;
            }

            RebuildTimers();
        }

        public int GetLevel(SkillData skill)
        {
            if (skill == null)
                return 1;

            for (int i = 0;
                 i < _equippedSkills.Length;
                 i++)
            {
                SkillData equipped =
                    _equippedSkills[i];

                if (equipped != skill &&
                    (equipped == null ||
                     equipped.id != skill.id))
                    continue;

                return i < _skillLevels.Length
                    ? Mathf.Max(1, _skillLevels[i])
                    : 1;
            }

            return 1;
        }

        public int GetLevel(int index)
        {
            if (index < 0 ||
                index >= _skillLevels.Length)
                return 1;

            return Mathf.Max(
                1,
                _skillLevels[index]);
        }

        public SkillData GetSkill(int index)
        {
            if (index < 0 ||
                index >= Count)
                return null;

            return _equippedSkills[index];
        }

        public float GetCooldown01(int index)
        {
            if (_skillTimers == null ||
                index < 0 ||
                index >= _skillTimers.Length)
                return 0f;

            SkillData skill =
                _equippedSkills[index];

            if (skill == null ||
                skill.cooldown <= 0f)
                return 0f;

            return Mathf.Clamp01(
                _skillTimers[index] /
                skill.cooldown);
        }

        public void Update(
            float deltaTime,
            Func<SkillData, bool> canUse,
            Action<SkillData> useSkill)
        {
            if (_equippedSkills == null)
                return;

            if (_skillTimers == null ||
                _skillTimers.Length !=
                _equippedSkills.Length)
            {
                RebuildTimers();
            }

            for (int i = 0;
                 i < _equippedSkills.Length;
                 i++)
            {
                SkillData skill =
                    _equippedSkills[i];

                if (skill == null)
                    continue;

                _skillTimers[i] += deltaTime;

                if (_skillTimers[i] <
                    skill.cooldown)
                    continue;

                if (canUse != null &&
                    !canUse(skill))
                    continue;

                _skillTimers[i] = 0f;

                useSkill?.Invoke(skill);
            }
        }

        private void RebuildTimers()
        {
            _skillTimers =
                new float[
                    _equippedSkills?.Length ?? 0];

            for (int i = 0;
                 i < _skillTimers.Length;
                 i++)
            {
                SkillData skill =
                    _equippedSkills[i];

                _skillTimers[i] =
                    skill != null
                        ? skill.cooldown
                        : 0f;
            }
        }
    }
}
