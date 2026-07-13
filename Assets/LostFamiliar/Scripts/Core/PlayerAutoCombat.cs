using UnityEngine;

namespace LostFamiliar.Battle
{
    /// <summary>
    /// 플레이어에게 붙이는 자동 전투 컴포넌트입니다.
    /// 가장 가까운 적을 기본 공격하고 장착 스킬을 쿨타임마다 자동 사용합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAutoCombat : MonoBehaviour
    {
        [Header("생존 능력치")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        [Header("기본 공격")]
        [SerializeField, Min(0.1f)] private float attackDamage = 10f;
        [SerializeField, Min(0.1f)] private float attackRange = 5f;
        [SerializeField, Min(0.1f)] private float attacksPerSecond = 1f;

        [Header("장착 스킬")]
        [Tooltip("장착된 스킬은 쿨타임이 끝나면 자동으로 사용됩니다.")]
        [SerializeField] private SkillData[] equippedSkills;

        public float MaxHealth => maxHealth;
        public float Health { get; private set; }
        public float AttackDamage => attackDamage;
        public bool IsAlive => Health > 0f;
        public SkillData[] EquippedSkills => equippedSkills;

        public event System.Action Defeated;

        private float _attackTimer;
        private float[] _skillTimers;

        private void Awake()
        {
            Health = maxHealth;
            RebuildSkillTimers();
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            attackDamage = Mathf.Max(0.1f, attackDamage);
            attackRange = Mathf.Max(0.1f, attackRange);
            attacksPerSecond = Mathf.Max(0.1f, attacksPerSecond);
        }

        private void Update()
        {
            if (!IsAlive)
                return;

            UpdateBasicAttack();
            UpdateSkills();
        }

        private void UpdateBasicAttack()
        {
            _attackTimer += Time.deltaTime;
            if (_attackTimer < 1f / attacksPerSecond)
                return;

            EnemyActor target = FindNearestEnemy(attackRange);
            if (target == null)
                return;

            _attackTimer = 0f;
            target.TakeDamage(attackDamage);
        }

        private void UpdateSkills()
        {
            if (equippedSkills == null)
                return;

            if (_skillTimers == null || _skillTimers.Length != equippedSkills.Length)
                RebuildSkillTimers();

            for (int i = 0; i < equippedSkills.Length; i++)
            {
                SkillData skill = equippedSkills[i];
                if (skill == null)
                    continue;

                _skillTimers[i] += Time.deltaTime;
                if (_skillTimers[i] < skill.cooldown || !CanUse(skill))
                    continue;

                _skillTimers[i] = 0f;
                UseSkill(skill);
            }
        }

        private static bool CanUse(SkillData skill)
        {
            return skill.targetType == SkillTargetType.Self || EnemyActor.Active.Count > 0;
        }

        private void UseSkill(SkillData skill)
        {
            switch (skill.targetType)
            {
                case SkillTargetType.NearestEnemy:
                    EnemyActor nearest = FindNearestEnemy(float.MaxValue);
                    if (nearest != null)
                        nearest.TakeDamage(attackDamage * skill.damageMultiplier);
                    break;

                case SkillTargetType.AllEnemies:
                    EnemyActor[] enemies = EnemyActor.Active.ToArray();
                    foreach (EnemyActor enemy in enemies)
                    {
                        if (enemy != null && Vector3.Distance(transform.position, enemy.transform.position) <= skill.radius)
                            enemy.TakeDamage(attackDamage * skill.damageMultiplier);
                    }
                    break;

                case SkillTargetType.Self:
                    // 버프/회복 스킬 구현 지점입니다.
                    break;
            }

            CreateTemporarySkillEffect(skill);
        }

        private void CreateTemporarySkillEffect(SkillData skill)
        {
            GameObject effect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            effect.name = $"SkillFX_{skill.displayName}";
            effect.transform.position = transform.position;
            effect.transform.localScale = Vector3.one * Mathf.Max(0.5f, skill.radius * 0.25f);

            Renderer effectRenderer = effect.GetComponent<Renderer>();
            effectRenderer.material.color = skill.effectColor;

            Destroy(effect.GetComponent<Collider>());
            Destroy(effect, 0.25f);
        }

        private EnemyActor FindNearestEnemy(float range)
        {
            EnemyActor nearest = null;
            float nearestDistance = range * range;

            foreach (EnemyActor enemy in EnemyActor.Active)
            {
                if (enemy == null)
                    continue;

                float distance = (enemy.transform.position - transform.position).sqrMagnitude;
                if (distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                nearest = enemy;
            }

            return nearest;
        }

        public void TakeDamage(float damage)
        {
            if (!IsAlive)
                return;

            Health = Mathf.Max(0f, Health - Mathf.Max(0f, damage));
            if (!IsAlive)
                Defeated?.Invoke();
        }

        public void Revive()
        {
            Health = maxHealth;
            _attackTimer = 0f;
        }

        public void SetEquippedSkills(SkillData[] skills)
        {
            equippedSkills = skills ?? System.Array.Empty<SkillData>();
            RebuildSkillTimers();
        }

        public float GetSkillCooldown01(int index)
        {
            if (_skillTimers == null || index < 0 || index >= _skillTimers.Length)
                return 0f;

            SkillData skill = equippedSkills[index];
            return skill == null ? 0f : Mathf.Clamp01(_skillTimers[index] / skill.cooldown);
        }

        private void RebuildSkillTimers()
        {
            _skillTimers = new float[equippedSkills?.Length ?? 0];
        }
    }
}
