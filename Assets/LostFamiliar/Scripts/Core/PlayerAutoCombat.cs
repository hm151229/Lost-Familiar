using System.Collections;
using System.Collections.Generic;
using LostFamiliar.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class PlayerAutoCombat : MonoBehaviour
    {
        [Header("기본 능력치")]
        [FormerlySerializedAs("maxHealth")]
        [SerializeField, Min(1f)] private float baseMaxHealth = 100f;
        [FormerlySerializedAs("attackDamage")]
        [SerializeField, Min(.1f)] private float baseAttackDamage = 10f;
        [SerializeField, Min(.1f)] private float attackRange = 1.5f;
        [SerializeField, Min(.1f)] private float moveSpeed = 2.8f;
        [SerializeField, Min(.1f)] private float stoppingDistance = 1.4f;
        [FormerlySerializedAs("attacksPerSecond")]
        [SerializeField, Min(.1f)] private float baseAttacksPerSecond = 1f;

        [Header("장착 스킬")]
        [SerializeField] private SkillData[] equippedSkills;

        public float MaxHealth { get; private set; }
        public float Health { get; private set; }
        public float AttackDamage { get; private set; }
        public float AttacksPerSecond { get; private set; }
        public float CriticalChance { get; private set; } = .05f;
        public float CriticalMultiplier { get; private set; } = 1.5f;
        public float SkillDamageMultiplier { get; private set; } = 1f;
        public float BossDamageMultiplier { get; private set; } = 1f;
        public bool LastAttackWasCritical { get; private set; }
        public bool IsAlive => Health > 0f;
        public SkillData[] EquippedSkills => equippedSkills;

        public event System.Action Defeated;

        private float _attackTimer;
        private float[] _skillTimers;
        private Vector3 _initialPosition;
        private SpriteRenderer _visualRenderer;
        private EnemyActor _currentTarget;

        private void Awake()
        {
            MaxHealth = baseMaxHealth;
            AttackDamage = baseAttackDamage;
            AttacksPerSecond = baseAttacksPerSecond;
            Health = MaxHealth;
            _initialPosition = transform.position;
            _visualRenderer = GetComponentInChildren<SpriteRenderer>(true);
            RebuildSkillTimers();
        }

        private void OnValidate()
        {
            baseMaxHealth = Mathf.Max(1f, baseMaxHealth);
            baseAttackDamage = Mathf.Max(.1f, baseAttackDamage);
            attackRange = Mathf.Max(.1f, attackRange);
            moveSpeed = Mathf.Max(.1f, moveSpeed);
            stoppingDistance = Mathf.Clamp(stoppingDistance, .1f, attackRange);
            baseAttacksPerSecond = Mathf.Max(.1f, baseAttacksPerSecond);
        }

        private void Update()
        {
            if (!IsAlive)
                return;

            UpdateMovement();
            UpdateBasicAttack();
            UpdateSkills();
        }

        private void UpdateMovement()
        {
            EnemyActor target = GetOrAcquireTarget();
            if (target == null)
                return;
            if (target.IsBeingKnockedBack)
                return;

            Vector3 difference = target.transform.position - transform.position;
            difference.z = 0f;
            if (_visualRenderer != null && Mathf.Abs(difference.x) > .01f)
                _visualRenderer.flipX = difference.x < 0f;

            float distance = difference.magnitude;
            float stopDistance = Mathf.Min(stoppingDistance, attackRange);
            if (distance <= stopDistance || distance <= Mathf.Epsilon)
                return;

            Vector3 destination = target.transform.position - difference.normalized * stopDistance;
            destination.z = transform.position.z;
            transform.position = Vector3.MoveTowards(
                transform.position,
                destination,
                moveSpeed * Time.deltaTime);
        }

        private void UpdateBasicAttack()
        {
            _attackTimer += Time.deltaTime;
            if (_attackTimer < 1f / AttacksPerSecond)
                return;

            EnemyActor target = GetOrAcquireTarget();
            if (target == null)
                return;

            if ((target.transform.position - transform.position).sqrMagnitude > attackRange * attackRange)
                return;

            _attackTimer = 0f;
            LastAttackWasCritical = Random.value < CriticalChance;
            float damage = AttackDamage * (LastAttackWasCritical ? CriticalMultiplier : 1f);
            Vector3 attackDirection = target.transform.position - transform.position;
            attackDirection.z = 0f;
            if (attackDirection.sqrMagnitude <= Mathf.Epsilon)
                attackDirection = _visualRenderer != null && _visualRenderer.flipX ? Vector3.left : Vector3.right;
            attackDirection.Normalize();

            foreach (EnemyActor enemy in EnemyActor.Active.ToArray())
            {
                if (enemy == null || enemy.Health <= 0f)
                    continue;

                Vector3 offset = enemy.transform.position - transform.position;
                offset.z = 0f;
                if (offset.sqrMagnitude > attackRange * attackRange || offset.sqrMagnitude <= Mathf.Epsilon)
                    continue;
                if (Vector3.Dot(attackDirection, offset.normalized) < 0.2f)
                    continue;

                enemy.TakeDamage(ApplyBossDamage(damage, enemy));
            }
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

        private void UseSkill(SkillData skill) => StartCoroutine(ExecuteSkill(skill));

        private IEnumerator ExecuteSkill(SkillData skill)
        {
            switch (skill.behavior)
            {
                case SkillBehavior.MagicMissile: yield return CastMagicMissile(skill); break;
                case SkillBehavior.FireBall: yield return CastFireBall(skill); break;
                case SkillBehavior.IceSpear: CastIceSpear(skill); break;
                case SkillBehavior.LightningBolt: yield return CastLightningBolt(skill); break;
                case SkillBehavior.ArcaneOrb: yield return CastArcaneOrb(skill); break;
                case SkillBehavior.WindCutter: yield return CastWindCutter(skill); break;
                case SkillBehavior.Meteor: yield return CastMeteor(skill); break;
                case SkillBehavior.Blizzard: yield return CastBlizzard(skill); break;
                case SkillBehavior.BlackHole: yield return CastBlackHole(skill); break;
                case SkillBehavior.StarNova: CastStarNova(skill); break;
            }
        }

        private IEnumerator CastMagicMissile(SkillData skill)
        {
            for (int i = 0; i < Mathf.Max(1, skill.projectileCount); i++)
            {
                EnemyActor target = FindNearestEnemy(float.MaxValue);
                if (target == null) yield break;
                yield return LaunchProjectile(skill, target, skill.damageMultiplier, 0f, .16f);
                yield return new WaitForSeconds(.08f);
            }
        }

        private IEnumerator CastFireBall(SkillData skill)
        {
            EnemyActor target = FindNearestEnemy(float.MaxValue);
            if (target != null)
                yield return LaunchProjectile(skill, target, skill.damageMultiplier, skill.radius, .25f);
        }

        private void CastIceSpear(SkillData skill)
        {
            EnemyActor nearest = FindNearestEnemy(float.MaxValue);
            Vector3 forward = nearest != null
                ? (nearest.transform.position - transform.position).normalized
                : (_visualRenderer != null && _visualRenderer.flipX ? Vector3.left : Vector3.right);
            const float halfArc = 38f;
            int count = Mathf.Max(1, skill.projectileCount);
            HashSet<EnemyActor> hit = new HashSet<EnemyActor>();
            for (int i = 0; i < count; i++)
            {
                float angle = count <= 1 ? 0f : Mathf.Lerp(-halfArc, halfArc, i / (float)(count - 1));
                Vector3 direction = Quaternion.Euler(0f, 0f, angle) * forward;
                CreateEffect(transform.position + direction * 2.5f, new Vector3(.2f, 2.5f, .2f),
                    skill.effectColor, .25f, Quaternion.FromToRotation(Vector3.up, direction));
                foreach (EnemyActor enemy in EnemyActor.Active.ToArray())
                {
                    if (enemy == null || hit.Contains(enemy)) continue;
                    Vector3 offset = enemy.transform.position - transform.position;
                    float forwardDistance = Vector3.Dot(offset, direction);
                    float perpendicular = Vector3.Cross(direction, offset).magnitude;
                    if (forwardDistance < 0f || forwardDistance > Mathf.Max(6f, skill.radius) || perpendicular > .45f)
                        continue;
                    hit.Add(enemy);
                    DealSkillDamage(skill, enemy, skill.damageMultiplier);
                }
            }
        }

        private IEnumerator CastLightningBolt(SkillData skill)
        {
            for (int i = 0; i < Mathf.Max(1, skill.projectileCount); i++)
            {
                EnemyActor target = GetRandomEnemy();
                if (target == null) yield break;
                Vector3 point = target.transform.position;
                CreateEffect(point + Vector3.up * 1.5f, new Vector3(.25f, 3f, .25f), skill.effectColor, .18f);
                DamageArea(skill, point, skill.radius, skill.damageMultiplier);
                yield return new WaitForSeconds(.16f);
            }
        }

        private IEnumerator CastArcaneOrb(SkillData skill)
        {
            GameObject orb = CreateEffect(transform.position, Vector3.one * .45f, skill.effectColor, skill.duration + .25f);
            float elapsed = 0f;
            float interval = Mathf.Max(.05f, skill.tickInterval);
            while (elapsed < skill.duration)
            {
                if (orb != null)
                {
                    float angle = elapsed * 240f * Mathf.Deg2Rad;
                    orb.transform.position = transform.position +
                                             new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 1.2f;
                }
                EnemyActor target = FindNearestEnemy(transform.position, Mathf.Max(1f, skill.radius));
                if (target != null) DealSkillDamage(skill, target, skill.damageMultiplier);
                yield return new WaitForSeconds(interval);
                elapsed += interval;
            }
        }

        private IEnumerator CastWindCutter(SkillData skill)
        {
            for (int i = 0; i < Mathf.Max(1, skill.projectileCount); i++)
            {
                CreateEffect(transform.position, new Vector3(7f, .35f, .35f), skill.effectColor, .28f);
                foreach (EnemyActor enemy in EnemyActor.Active.ToArray())
                    if (enemy != null) DealSkillDamage(skill, enemy, skill.damageMultiplier);
                yield return new WaitForSeconds(.22f);
            }
        }

        private IEnumerator CastMeteor(SkillData skill)
        {
            Vector3 center = GetDensestEnemyPosition(skill.radius);
            for (int i = 0; i < Mathf.Max(1, skill.projectileCount); i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * skill.radius * .55f;
                Vector3 impact = center + new Vector3(randomOffset.x, randomOffset.y, 0f);
                CreateEffect(impact, Vector3.one * skill.radius, skill.effectColor, .35f);
                DamageArea(skill, impact, skill.radius, skill.damageMultiplier);
                yield return new WaitForSeconds(.18f);
            }
        }

        private IEnumerator CastBlizzard(SkillData skill)
        {
            Vector3 center = GetDensestEnemyPosition(skill.radius);
            CreateEffect(center, Vector3.one * skill.radius * 1.6f, skill.effectColor, skill.duration);
            float interval = Mathf.Max(.05f, skill.tickInterval);
            for (float elapsed = 0f; elapsed < skill.duration; elapsed += interval)
            {
                foreach (EnemyActor enemy in EnemyActor.Active.ToArray())
                {
                    if (enemy == null || Vector3.Distance(center, enemy.transform.position) > skill.radius) continue;
                    DealSkillDamage(skill, enemy, skill.damageMultiplier);
                    enemy.ApplySlow(skill.slowPercent, interval + .1f);
                }
                yield return new WaitForSeconds(interval);
            }
        }

        private IEnumerator CastBlackHole(SkillData skill)
        {
            Vector3 center = GetDensestEnemyPosition(skill.radius);
            CreateEffect(center, Vector3.one * skill.radius * 1.4f, skill.effectColor, skill.duration + .25f);
            float interval = Mathf.Max(.05f, skill.tickInterval);
            for (float elapsed = 0f; elapsed < skill.duration; elapsed += interval)
            {
                foreach (EnemyActor enemy in EnemyActor.Active.ToArray())
                {
                    if (enemy == null || Vector3.Distance(center, enemy.transform.position) > skill.radius) continue;
                    Vector3 pulled = Vector3.MoveTowards(enemy.transform.position, center, skill.pullStrength * interval);
                    pulled.z = enemy.transform.position.z;
                    enemy.transform.position = pulled;
                    DealSkillDamage(skill, enemy, skill.damageMultiplier);
                }
                yield return new WaitForSeconds(interval);
            }
            DamageArea(skill, center, skill.radius, skill.secondaryDamageMultiplier);
        }

        private void CastStarNova(SkillData skill)
        {
            CreateEffect(transform.position, Vector3.one * skill.radius * 1.8f, skill.effectColor, .55f);
            DamageArea(skill, transform.position, skill.radius, skill.damageMultiplier);
            int count = Mathf.Max(1, skill.projectileCount);
            foreach (EnemyActor enemy in EnemyActor.Active.ToArray())
            {
                if (enemy == null) continue;
                Vector3 offset = enemy.transform.position - transform.position;
                if (offset.magnitude > skill.radius * 1.6f || offset.sqrMagnitude <= Mathf.Epsilon) continue;
                float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
                float step = 360f / count;
                float nearestRay = Mathf.Round(angle / step) * step;
                if (Mathf.Abs(Mathf.DeltaAngle(angle, nearestRay)) <= 8f)
                    DealSkillDamage(skill, enemy, skill.secondaryDamageMultiplier);
            }
        }

        private IEnumerator LaunchProjectile(
            SkillData skill, EnemyActor target, float multiplier, float explosionRadius, float travelDuration)
        {
            if (target == null) yield break;
            GameObject projectile = CreateEffect(
                transform.position, Vector3.one * .3f, skill.effectColor, travelDuration + .1f);
            float elapsed = 0f;
            Vector3 destination = target.transform.position;
            while (elapsed < travelDuration)
            {
                if (target != null && target.Health > 0f) destination = target.transform.position;
                elapsed += Time.deltaTime;
                if (projectile != null)
                    projectile.transform.position = Vector3.Lerp(
                        transform.position, destination, Mathf.Clamp01(elapsed / travelDuration));
                yield return null;
            }
            if (explosionRadius > 0f) DamageArea(skill, destination, explosionRadius, multiplier);
            else if (target != null && target.Health > 0f) DealSkillDamage(skill, target, multiplier);
        }

        private void DamageArea(SkillData skill, Vector3 center, float radius, float multiplier)
        {
            foreach (EnemyActor enemy in EnemyActor.Active.ToArray())
                if (enemy != null && Vector3.Distance(center, enemy.transform.position) <= radius)
                    DealSkillDamage(skill, enemy, multiplier);
        }

        private void DealSkillDamage(SkillData skill, EnemyActor enemy, float multiplier)
        {
            if (enemy == null || multiplier <= 0f) return;
            float damage = AttackDamage * multiplier * SkillDamageMultiplier;
            enemy.TakeDamage(ApplyBossDamage(damage, enemy));
        }

        private float ApplyBossDamage(float damage, EnemyActor enemy)
        {
            return enemy != null && enemy.IsBoss ? damage * BossDamageMultiplier : damage;
        }

        private GameObject CreateEffect(
            Vector3 position,
            Vector3 scale,
            Color color,
            float lifetime,
            Quaternion? rotation = null)
        {
            GameObject effect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            effect.name = "SkillEffect";
            effect.transform.position = position;
            effect.transform.localScale = scale;
            effect.transform.rotation = rotation ?? Quaternion.identity;
            effect.GetComponent<Renderer>().material.color = color;
            Destroy(effect.GetComponent<Collider>());
            Destroy(effect, Mathf.Max(.05f, lifetime));
            return effect;
        }

        private EnemyActor FindNearestEnemy(float range)
        {
            return FindNearestEnemy(transform.position, range);
        }

        private static EnemyActor FindNearestEnemy(Vector3 center, float range)
        {
            EnemyActor nearest = null;
            float nearestDistance = range * range;
            foreach (EnemyActor enemy in EnemyActor.Active)
            {
                if (enemy == null)
                    continue;

                float distance = (enemy.transform.position - center).sqrMagnitude;
                if (distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                nearest = enemy;
            }

            return nearest;
        }

        private static EnemyActor GetRandomEnemy()
        {
            EnemyActor[] enemies = EnemyActor.Active.ToArray();
            if (enemies.Length == 0)
                return null;

            int start = Random.Range(0, enemies.Length);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyActor enemy = enemies[(start + i) % enemies.Length];
                if (enemy != null && enemy.Health > 0f)
                    return enemy;
            }
            return null;
        }

        private Vector3 GetDensestEnemyPosition(float radius)
        {
            EnemyActor[] enemies = EnemyActor.Active.ToArray();
            Vector3 bestPosition = transform.position;
            int bestCount = 0;
            float radiusSquared = radius * radius;
            foreach (EnemyActor candidate in enemies)
            {
                if (candidate == null)
                    continue;
                int count = 0;
                foreach (EnemyActor other in enemies)
                {
                    if (other != null &&
                        (other.transform.position - candidate.transform.position).sqrMagnitude <= radiusSquared)
                        count++;
                }
                if (count <= bestCount)
                    continue;
                bestCount = count;
                bestPosition = candidate.transform.position;
            }
            return bestPosition;
        }

        private EnemyActor GetOrAcquireTarget()
        {
            if (_currentTarget == null || _currentTarget.Health <= 0f ||
                !_currentTarget.isActiveAndEnabled || !EnemyActor.Active.Contains(_currentTarget))
            {
                _currentTarget = FindNearestEnemy(float.MaxValue);
            }

            return _currentTarget;
        }

        public void ApplyProgression(GameSaveData data, EquipmentBonuses equipmentBonuses = default)
        {
            if (data == null)
                return;

            float levelAttackBonus = 1f + Mathf.Max(0, data.playerLevel - 1) * .05f;
            float levelHealthBonus = 1f + Mathf.Max(0, data.playerLevel - 1) * .03f;
            AttackDamage = (baseAttackDamage + (float)GameBalance.StatValue(StatType.Attack, data.attackLevel)) * levelAttackBonus *
                           (1f + equipmentBonuses.attackPercent / 100f);
            MaxHealth = baseMaxHealth * levelHealthBonus *
                        (1f + equipmentBonuses.maxHealthPercent / 100f);
            AttacksPerSecond = Mathf.Max(.1f,
                baseAttacksPerSecond * (1f + equipmentBonuses.attackSpeedPercent / 100f));
            CriticalChance = Mathf.Min(.95f,
                (float)GameBalance.StatValue(StatType.CriticalChance, data.criticalChanceLevel) / 100f +
                equipmentBonuses.criticalChancePercentPoint / 100f);
            CriticalMultiplier = (float)GameBalance.StatValue(StatType.CriticalDamage, data.criticalDamageLevel) / 100f +
                                 equipmentBonuses.criticalDamagePercent / 100f;
            SkillDamageMultiplier = (float)GameBalance.StatValue(StatType.SkillDamage, data.skillDamageLevel) / 100f +
                                    equipmentBonuses.skillDamagePercent / 100f;
            BossDamageMultiplier = (float)GameBalance.StatValue(StatType.BossDamage, data.bossDamageLevel) / 100f +
                                   equipmentBonuses.bossDamagePercent / 100f;
            Health = Mathf.Min(Health, MaxHealth);
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
            Health = MaxHealth;
            _attackTimer = 0f;
        }

        public void ResetPosition()
        {
            _currentTarget = null;
            transform.position = _initialPosition;
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
            for (int i = 0; i < _skillTimers.Length; i++)
            {
                SkillData skill = equippedSkills[i];
                _skillTimers[i] = skill != null ? skill.cooldown : 0f;
            }
        }
    }
}
