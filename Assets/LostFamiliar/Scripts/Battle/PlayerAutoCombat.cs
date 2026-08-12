using LostFamiliar.Core;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class PlayerAutoCombat : MonoBehaviour
    {
        // Enemies use orders 0~1. Keep the player well above every enemy sprite,
        // including boss visuals and their hit-flash overlays.
        private const int PlayerSortingOrder = 100;

        [Header("기본 능력치")]
        [FormerlySerializedAs("maxHealth")]
        [SerializeField, Min(1f)] private float baseMaxHealth = 100f;
        [FormerlySerializedAs("attackDamage")]
        [SerializeField, Min(.1f)] private float baseAttackDamage = 10f;
        [SerializeField, Min(.1f)] private float attackRange = 1.5f;
        [SerializeField, Min(.1f)] private float moveSpeed = 2.8f;
        [SerializeField, Min(.1f)] private float stoppingDistance = 1.4f;
        [SerializeField, Min(.1f)] private float bossStoppingDistance = 2.4f;
        [FormerlySerializedAs("attacksPerSecond")]
        [SerializeField, Min(.1f)] private float baseAttacksPerSecond = 1f;

        [Header("Animation")]
        [SerializeField, Min(.05f)] private float attackAnimationDuration = .48f;
        [SerializeField, Range(0f, .25f)] private float animationCrossFadeDuration = .05f;

        [Header("Idle Breathing")]
        [SerializeField, Min(.1f)] private float idleBreathSpeed = 2.2f;
        [SerializeField, Range(0f, .1f)] private float idleBreathScaleAmount = .025f;
        [SerializeField, Range(0f, .15f)] private float idleBreathMoveAmount = .025f;

        [Header("Walk Motion")]
        [SerializeField, Min(.1f)] private float walkBobSpeed = 8f;
        [SerializeField, Range(0f, .1f)] private float walkScaleAmount = .015f;
        [SerializeField, Range(0f, .15f)] private float walkMoveAmount = .03f;

        [Header("Attack Motion")]
        [SerializeField, Min(.1f)] private float attackBobSpeed = 7f;
        [SerializeField, Range(0f, .1f)] private float attackScaleAmount = .025f;
        [SerializeField, Range(0f, .15f)] private float attackMoveAmount = .04f;

        [Header("장착 스킬")]
        [SerializeField] private SkillData[] equippedSkills;
        [SerializeField] private PlayerSkillExecutor skillExecutor;

        [Header("스킬 발사 위치")]
        [Tooltip("표적을 향해 날아가는 스킬 이펙트가 생성되는 위치입니다. 비어 있으면 자식 FirePoint를 자동으로 찾습니다.")]
        [SerializeField] private Transform firePoint;

        [Header("Player Health Bar")]
        [SerializeField] private Image playerHealthBarFill;

        [Header("Runtime Health (Debug)")]
        [SerializeField, Min(0f)] private float currentHealth;

        [Header("Damage Reception")]
        [SerializeField, Min(0f)] private float damageInvulnerabilityDuration = .25f;
        private float _nextDamageAllowedTime;

        public float MaxHealth { get; private set; }
        public float Health => currentHealth;
        public float AttackDamage { get; private set; }
        public float AttacksPerSecond { get; private set; }
        public float CriticalChance { get; private set; } = .05f;
        public float CriticalMultiplier { get; private set; } = 1.5f;
        public float SkillDamageMultiplier { get; private set; } = 1f;
        public float BossDamageMultiplier { get; private set; } = 1f;
        public bool LastAttackWasCritical { get; private set; }
        public bool IsAlive => Health > 0f;
        public int CombatGroup => combatGroup;
        public SkillData[] EquippedSkills =>
            _skillController?.EquippedSkills ??
            System.Array.Empty<SkillData>();
        public float SeparationFootprintRadius => _separationFootprintRadius;

        private float _attackTimer;
        private PlayerSkillController _skillController;
        private Vector3 _initialPosition;
        private SpriteRenderer _visualRenderer;
        private Animator _visualAnimator;
        private Transform _visualTransform;
        private Vector3 _visualBaseLocalPosition;
        private Vector3 _visualBaseLocalScale;
        private ProceduralMotion _proceduralMotion;
        private EnemyActor _currentTarget;
        private float _attackAnimationUntil;
        private int _requestedAnimationState;
        private float _separationFootprintRadius = 0.55f;
        [SerializeField, Min(0)] private int combatGroup;

        public void SetCombatGroup(int group)
        {
            combatGroup = Mathf.Max(0, group);
            _currentTarget = null;
        }

        private void PlayCombatSfx(string id, float volume = 1f)
        {
            GameAudioManager audio = GameAudioManager.Instance;
            if (audio.IsBattleAudioAllowed(CombatGroup))
                audio.PlaySfx(id, volume);
        }

        private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Anim_Idle");
        private static readonly int WalkStateHash = Animator.StringToHash("Base Layer.Anim_Walk");
        private static readonly int AttackStateHash = Animator.StringToHash("Base Layer.Anim_Attack");

        private enum ProceduralMotion
        {
            None,
            Idle,
            Walk,
            Attack
        }

        private void Awake()
        {
            _skillController = new PlayerSkillController();
            MaxHealth = baseMaxHealth;
            AttackDamage = baseAttackDamage;
            AttacksPerSecond = baseAttacksPerSecond;
            currentHealth = MaxHealth;
            _initialPosition = transform.position;
            _visualRenderer = GetComponentInChildren<SpriteRenderer>(true);
            _visualAnimator = GetComponentInChildren<Animator>(true);
            _visualTransform = _visualRenderer != null ? _visualRenderer.transform :
                (_visualAnimator != null ? _visualAnimator.transform : null);
            if (firePoint == null)
                firePoint = FindChildByName(transform, "FirePoint");
            if (skillExecutor == null)
                skillExecutor = GetComponent<PlayerSkillExecutor>();
            if (skillExecutor == null)
            {
                Debug.LogError(
                    "PlayerSkillExecutor가 연결되지 않았습니다.",
                    this);
            }
            else
            {
                skillExecutor.Initialize(
                    this,
                    _skillController,
                    firePoint,
                    _visualRenderer);
            }
            AutoFindHealthBar();
            if (_visualTransform != null)
            {
                _visualBaseLocalPosition = _visualTransform.localPosition;
                _visualBaseLocalScale = _visualTransform.localScale;
            }
            ApplyPlayerSortingOrder();
            CacheSeparationFootprint();
            _skillController.SetEquippedSkills(equippedSkills);
            UpdateHealthBar();
            PlayIdleAnimation(true);
        }

        private void AutoFindHealthBar()
        {
            if (playerHealthBarFill == null)
            {
                foreach (Image image in GetComponentsInChildren<Image>(true))
                {
                    if (image != null && image.name == "Fill")
                    {
                        playerHealthBarFill = image;
                        break;
                    }
                }
            }

            if (playerHealthBarFill != null)
            {
                playerHealthBarFill.type = Image.Type.Filled;
                playerHealthBarFill.fillMethod = Image.FillMethod.Horizontal;
                playerHealthBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                playerHealthBarFill.fillClockwise = true;
            }
        }

        private void UpdateHealthBar()
        {
            float health01 = MaxHealth <= 0f ? 0f : Mathf.Clamp01(Health / MaxHealth);
            if (playerHealthBarFill != null)
                playerHealthBarFill.fillAmount = health01;
        }

        private void ApplyPlayerSortingOrder()
        {
            if (_visualRenderer != null)
                _visualRenderer.sortingOrder = PlayerSortingOrder;
        }

        private void CacheSeparationFootprint()
        {
            if (_visualRenderer == null || _visualRenderer.sprite == null)
            {
                _separationFootprintRadius = 0.55f;
                return;
            }
            Vector2 size = _visualRenderer.bounds.size;
            _separationFootprintRadius = Mathf.Clamp(
                Mathf.Max(size.x * 0.42f, size.y * 0.16f),
                0.45f,
                2f);
        }

        private void OnValidate()
        {
            baseMaxHealth = Mathf.Max(1f, baseMaxHealth);
            baseAttackDamage = Mathf.Max(.1f, baseAttackDamage);
            attackRange = Mathf.Max(.1f, attackRange);
            moveSpeed = Mathf.Max(.1f, moveSpeed);
            stoppingDistance = Mathf.Clamp(stoppingDistance, .1f, attackRange);
            bossStoppingDistance = Mathf.Max(.1f, bossStoppingDistance);
            baseAttacksPerSecond = Mathf.Max(.1f, baseAttacksPerSecond);
            idleBreathSpeed = Mathf.Max(.1f, idleBreathSpeed);
            walkBobSpeed = Mathf.Max(.1f, walkBobSpeed);
            attackBobSpeed = Mathf.Max(.1f, attackBobSpeed);
        }

        private void Update()
        {
            if (!IsAlive)
                return;

            bool isMoving = UpdateMovement();
            UpdateBasicAttack();
            UpdatePlayerAnimation(isMoving);
            UpdateSkills();
        }

        private void LateUpdate()
        {
            if (_visualTransform == null)
                return;

            if (_proceduralMotion == ProceduralMotion.None || !IsAlive)
            {
                ResetIdleBreathing();
                return;
            }

            float speed;
            float scaleAmount;
            float moveAmount;
            switch (_proceduralMotion)
            {
                case ProceduralMotion.Walk:
                    speed = walkBobSpeed;
                    scaleAmount = walkScaleAmount;
                    moveAmount = walkMoveAmount;
                    break;
                case ProceduralMotion.Attack:
                    speed = attackBobSpeed;
                    scaleAmount = attackScaleAmount;
                    moveAmount = attackMoveAmount;
                    break;
                default:
                    speed = idleBreathSpeed;
                    scaleAmount = idleBreathScaleAmount;
                    moveAmount = idleBreathMoveAmount;
                    break;
            }

            float wave = (Mathf.Sin(Time.time * speed) + 1f) * .5f;
            float scaleY = Mathf.Lerp(1f - scaleAmount, 1f + scaleAmount, wave);
            float scaleX = Mathf.Lerp(1f + scaleAmount * .25f,
                1f - scaleAmount * .15f, wave);
            _visualTransform.localScale = Vector3.Scale(
                _visualBaseLocalScale, new Vector3(scaleX, scaleY, 1f));
            _visualTransform.localPosition = _visualBaseLocalPosition +
                                             Vector3.up * (wave * moveAmount);
        }

        private void OnDisable()
        {
            ResetIdleBreathing();
        }

        private bool UpdateMovement()
        {
            // Do not slide toward a target while the attack animation is playing.
            if (Time.time < _attackAnimationUntil)
                return false;

            EnemyActor target = GetOrAcquireTarget();
            if (target == null)
                return false;
            if (target.IsBeingKnockedBack)
                return false;

            Vector3 difference = target.transform.position - transform.position;
            difference.z = 0f;
            if (_visualRenderer != null && Mathf.Abs(difference.x) > .01f)
                _visualRenderer.flipX = difference.x < 0f;

            float distance = difference.magnitude;
            float stopDistance = target.IsBoss
                ? bossStoppingDistance
                : Mathf.Max(stoppingDistance,
                    SeparationFootprintRadius + target.SeparationFootprintRadius);
            if (target.IsBoss && distance < stopDistance - .05f)
            {
                Vector3 away = distance > Mathf.Epsilon ? -difference.normalized : Vector3.left;
                Vector3 separationPoint = target.transform.position + away * stopDistance;
                separationPoint.z = transform.position.z;
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    separationPoint,
                    moveSpeed * Time.deltaTime);
                return true;
            }

            if (distance <= stopDistance + .05f || distance <= Mathf.Epsilon)
                return false;

            Vector3 destination = target.transform.position - difference.normalized * stopDistance;
            destination.z = transform.position.z;
            transform.position = Vector3.MoveTowards(
                transform.position,
                destination,
                moveSpeed * Time.deltaTime);
            return true;
        }

        private void UpdateBasicAttack()
        {
            _attackTimer += Time.deltaTime;
            if (_attackTimer < 1f / AttacksPerSecond)
                return;

            EnemyActor target = GetOrAcquireTarget();
            if (target == null)
                return;

            float targetDistance = (target.transform.position - transform.position).magnitude;
            if (target.IsBoss && targetDistance < bossStoppingDistance - .05f)
                return;

            float targetAttackRange = target.IsBoss
                ? Mathf.Max(attackRange, bossStoppingDistance + .1f)
                : Mathf.Max(attackRange,
                    SeparationFootprintRadius + target.SeparationFootprintRadius + .1f);
            if (targetDistance > targetAttackRange)
                return;

            _attackTimer = 0f;
            PlayCombatSfx("SFX_Player_BasicAttack");
            PlayAttackAnimation();
            LastAttackWasCritical = Random.value < CriticalChance;
            float damage = AttackDamage * (LastAttackWasCritical ? CriticalMultiplier : 1f);
            Vector3 attackDirection = target.transform.position - transform.position;
            attackDirection.z = 0f;
            if (attackDirection.sqrMagnitude <= Mathf.Epsilon)
                attackDirection = _visualRenderer != null && _visualRenderer.flipX ? Vector3.left : Vector3.right;
            attackDirection.Normalize();

            foreach (EnemyActor enemy in EnemyActor.Active.ToArray())
            {
                if (enemy == null || enemy.CombatGroup != CombatGroup || enemy.Health <= 0f)
                    continue;

                Vector3 offset = enemy.transform.position - transform.position;
                offset.z = 0f;
                float enemyAttackRange = enemy.IsBoss
                    ? Mathf.Max(attackRange, bossStoppingDistance + .1f)
                    : Mathf.Max(attackRange,
                        SeparationFootprintRadius + enemy.SeparationFootprintRadius + .1f);
                if (offset.sqrMagnitude > enemyAttackRange * enemyAttackRange ||
                    offset.sqrMagnitude <= Mathf.Epsilon)
                    continue;
                if (Vector3.Dot(attackDirection, offset.normalized) < 0.2f)
                    continue;

                enemy.TakeDamage(ApplyBossDamage(damage, enemy));
            }
        }

        private float ApplyBossDamage(
            float damage,
            EnemyActor enemy)
        {
            return enemy != null && enemy.IsBoss
                ? damage * BossDamageMultiplier
                : damage;
        }

        private void PlayAttackAnimation()
        {
            if (_visualAnimator == null || _visualAnimator.runtimeAnimatorController == null)
                return;

            _proceduralMotion = ProceduralMotion.Attack;
            float attackInterval = 1f / Mathf.Max(.1f, AttacksPerSecond);
            float playbackDuration = Mathf.Min(attackAnimationDuration, attackInterval * .9f);
            _visualAnimator.speed = attackAnimationDuration / Mathf.Max(.05f, playbackDuration);
            _visualAnimator.CrossFade(AttackStateHash, animationCrossFadeDuration, 0, 0f);
            _requestedAnimationState = AttackStateHash;
            _attackAnimationUntil = Time.time + playbackDuration;
        }

        private void UpdatePlayerAnimation(bool isMoving)
        {
            if (_visualAnimator == null || _visualAnimator.runtimeAnimatorController == null ||
                Time.time < _attackAnimationUntil)
                return;

            if (isMoving)
                PlayWalkAnimation(false);
            else
                PlayIdleAnimation(false);
        }

        private void PlayWalkAnimation(bool restart)
        {
            if (_visualAnimator == null || _visualAnimator.runtimeAnimatorController == null)
                return;

            if (restart || _requestedAnimationState != WalkStateHash)
            {
                _visualAnimator.CrossFade(WalkStateHash, animationCrossFadeDuration, 0, 0f);
                _requestedAnimationState = WalkStateHash;
            }
            _visualAnimator.speed = 1f;
            _proceduralMotion = ProceduralMotion.Walk;
        }

        private void PlayIdleAnimation(bool restart)
        {
            if (_visualAnimator == null || _visualAnimator.runtimeAnimatorController == null)
                return;

            if (restart || _requestedAnimationState != IdleStateHash)
            {
                _visualAnimator.CrossFade(IdleStateHash, animationCrossFadeDuration, 0, 0f);
                _requestedAnimationState = IdleStateHash;
            }
            _visualAnimator.speed = 1f;
            _proceduralMotion = ProceduralMotion.Idle;
        }

        private void ResetIdleBreathing()
        {
            if (_visualTransform == null)
                return;

            _visualTransform.localPosition = _visualBaseLocalPosition;
            _visualTransform.localScale = _visualBaseLocalScale;
        }

        private void UpdateSkills()
        {
            _skillController?.Update(
                Time.deltaTime,
                CanUseSkill,
                UseSkill);
        }

        private bool CanUseSkill(SkillData skill)
        {
            if (skill.targetType == SkillTargetType.Self) return true;
            foreach (EnemyActor enemy in EnemyActor.Active)
                if (enemy != null && enemy.CombatGroup == CombatGroup) return true;
            return false;
        }

        private void UseSkill(SkillData skill)
        {
            skillExecutor?.Execute(skill);
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

        public void ClearActiveSkills()
        {
            skillExecutor?.Clear();
        }

        private EnemyActor FindNearestEnemy(float range)
        {
            return FindNearestEnemy(transform.position, range);
        }

        private EnemyActor FindNearestEnemy(Vector3 center, float range)
        {
            EnemyActor nearest = null;
            float nearestDistance = range * range;
            foreach (EnemyActor enemy in EnemyActor.Active)
            {
                if (enemy == null || enemy.CombatGroup != CombatGroup)
                    continue;

                float distance = (enemy.transform.position - center).sqrMagnitude;
                if (distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                nearest = enemy;
            }

            return nearest;
        }

        private EnemyActor GetOrAcquireTarget()
        {
            if (_currentTarget == null || _currentTarget.Health <= 0f ||
                !_currentTarget.isActiveAndEnabled || _currentTarget.CombatGroup != CombatGroup ||
                !EnemyActor.Active.Contains(_currentTarget))
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
            currentHealth = Mathf.Min(currentHealth, MaxHealth);
            UpdateHealthBar();
        }

        public void TakeDamage(float damage)
        {
            if (!IsAlive || damage <= 0f || Time.time < _nextDamageAllowedTime)
                return;

            _nextDamageAllowedTime = Time.time + damageInvulnerabilityDuration;
            currentHealth = Mathf.Max(0f, currentHealth - damage);
            UpdateHealthBar();
            if (Health > 0f)
                return;

            _currentTarget = null;
            PlayCombatSfx("SFX_Player_Death");
            ClearActiveSkills();
            PlayIdleAnimation(true);
        }

        public void Revive()
        {
            currentHealth = MaxHealth;
            _attackTimer = 0f;
            _nextDamageAllowedTime = 0f;
            UpdateHealthBar();
        }

        public void ResetPosition()
        {
            ResetPosition(_initialPosition);
        }

        public void ResetPosition(Vector3 position)
        {
            _currentTarget = null;
            transform.position = position;
        }

        public void SetEquippedSkills(SkillData[] skills, int[] levels = null)
        {
            equippedSkills = skills ?? System.Array.Empty<SkillData>();
            _skillController?.SetEquippedSkills(
                equippedSkills,
                levels);
        }

        public double EstimateOfflineKillsPerSecond(double averageEnemyHealth, double spawnLimitPerSecond)
        {
            if (averageEnemyHealth <= 0d || spawnLimitPerSecond <= 0d)
                return 0d;

            double criticalMultiplier = System.Math.Max(1d, CriticalMultiplier);
            double expectedBasicHit = AttackDamage *
                (1d + Mathf.Clamp01(CriticalChance) * (criticalMultiplier - 1d));
            double totalDamagePerSecond = expectedBasicHit * AttacksPerSecond;
            double damagingHitsPerSecond = AttacksPerSecond;

            int skillCount =
                _skillController?.Count ?? 0;

            for (int i = 0;
                 i < skillCount;
                 i++)
            {
                SkillData skill =
                    _skillController.GetSkill(i);

                if (skill == null ||
                    skill.cooldown <= 0f)
                    continue;

                GetOfflineSkillHitProfile(
                    skill,
                    out double hitsPerCast,
                    out double damageMultiplierPerCast);

                if (hitsPerCast <= 0d ||
                    damageMultiplierPerCast <= 0d)
                    continue;

                int level =
                    _skillController.GetLevel(i);
                double levelMultiplier =
                    SkillBalance.EquippedEffectMultiplier(level);
                double cooldown =
                    System.Math.Max(.1d, skill.cooldown);
                totalDamagePerSecond += AttackDamage * SkillDamageMultiplier * levelMultiplier *
                                        damageMultiplierPerCast / cooldown;
                damagingHitsPerSecond += hitsPerCast / cooldown;
            }

            // Damage/HP estimates throughput, while the hit-rate cap prevents a very large
            // overkill hit from being counted as several defeated enemies.
            double damageLimitedKills = totalDamagePerSecond / averageEnemyHealth;
            return System.Math.Max(0d, System.Math.Min(
                spawnLimitPerSecond,
                System.Math.Min(damageLimitedKills, damagingHitsPerSecond)));
        }

        private static void GetOfflineSkillHitProfile(
            SkillData skill,
            out double hitsPerCast,
            out double damageMultiplierPerCast)
        {
            int projectiles = Mathf.Max(1, skill.projectileCount);
            int ticks = Mathf.Max(0, Mathf.CeilToInt(
                skill.duration / Mathf.Max(.05f, skill.tickInterval)));

            switch (skill.behavior)
            {
                case SkillBehavior.MagicMissile:
                case SkillBehavior.LightningBolt:
                case SkillBehavior.WindCutter:
                case SkillBehavior.Meteor:
                    hitsPerCast = projectiles;
                    damageMultiplierPerCast = skill.damageMultiplier * projectiles;
                    break;
                case SkillBehavior.ArcaneOrb:
                case SkillBehavior.Blizzard:
                    hitsPerCast = ticks;
                    damageMultiplierPerCast = skill.damageMultiplier * ticks;
                    break;
                case SkillBehavior.BlackHole:
                    hitsPerCast = ticks + (skill.secondaryDamageMultiplier > 0f ? 1 : 0);
                    damageMultiplierPerCast = skill.damageMultiplier * ticks + skill.secondaryDamageMultiplier;
                    break;
                case SkillBehavior.StarNova:
                    hitsPerCast = 1 + (skill.secondaryDamageMultiplier > 0f ? 1 : 0);
                    damageMultiplierPerCast = skill.damageMultiplier + skill.secondaryDamageMultiplier;
                    break;
                default:
                    hitsPerCast = 1d;
                    damageMultiplierPerCast = skill.damageMultiplier;
                    break;
            }
        }

        public float GetSkillCooldown01(int index)
        {
            return _skillController?.GetCooldown01(index) ?? 0f;
        }
    }
}
