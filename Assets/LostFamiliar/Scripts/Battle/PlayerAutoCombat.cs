using System.Collections;
using System.Collections.Generic;
using LostFamiliar.Core;
using UnityEngine;
using UnityEngine.Rendering;
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
        private const int SkillEffectSortingOrder = 50;
        private const int PlayerAreaEffectSortingOrder = 150;
        private const int StarNovaProjectileSortingOrder = 200;

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
        private Transform _skillEffectRoot;
        private readonly List<GameObject> _playerAttachedSkillEffects = new List<GameObject>();
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

        private void PlayCombatLoop(string id, float duration, float volume = 1f)
        {
            GameAudioManager audio = GameAudioManager.Instance;
            if (audio.IsBattleAudioAllowed(CombatGroup))
                audio.PlayLoopForDuration(id, duration, volume);
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
            EnsureSkillEffectRoot();
            _visualRenderer = GetComponentInChildren<SpriteRenderer>(true);
            _visualAnimator = GetComponentInChildren<Animator>(true);
            _visualTransform = _visualRenderer != null ? _visualRenderer.transform :
                (_visualAnimator != null ? _visualAnimator.transform : null);
            if (firePoint == null)
                firePoint = FindChildByName(transform, "FirePoint");
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

        private void OnDestroy()
        {
            if (_skillEffectRoot != null)
                Destroy(_skillEffectRoot.gameObject);
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

        private void UseSkill(SkillData skill) => StartCoroutine(ExecuteSkill(skill));

        private IEnumerator ExecuteSkill(SkillData skill)
        {
            switch (skill.behavior)
            {
                case SkillBehavior.MagicMissile: yield return CastMagicMissile(skill); break;
                case SkillBehavior.FireBall: yield return CastFireBall(skill); break;
                case SkillBehavior.IceSpear: yield return CastIceSpear(skill); break;
                case SkillBehavior.LightningBolt: yield return CastLightningBolt(skill); break;
                case SkillBehavior.ArcaneOrb: yield return CastArcaneOrb(skill); break;
                case SkillBehavior.WindCutter: yield return CastWindCutter(skill); break;
                case SkillBehavior.Meteor: yield return CastMeteor(skill); break;
                case SkillBehavior.Blizzard: yield return CastBlizzard(skill); break;
                case SkillBehavior.BlackHole: yield return CastBlackHole(skill); break;
                case SkillBehavior.StarNova: yield return CastStarNova(skill); break;
            }
        }

        private IEnumerator CastMagicMissile(SkillData skill)
        {
            for (int i = 0; i < Mathf.Max(1, skill.projectileCount); i++)
            {
                EnemyActor target = FindNearestEnemy(float.MaxValue);
                if (target == null) yield break;
                yield return LaunchProjectile(
                    skill, target, skill.damageMultiplier, 0f, skill.projectileTravelDuration);
                yield return new WaitForSeconds(.08f);
            }
        }

        private IEnumerator CastFireBall(SkillData skill)
        {
            EnemyActor target = FindNearestEnemy(float.MaxValue);
            if (target != null)
                yield return LaunchProjectile(
                    skill, target, skill.damageMultiplier, skill.radius, skill.projectileTravelDuration);
        }

        private IEnumerator CastIceSpear(SkillData skill)
        {
            PlayCombatSfx("SFX_IceSpear_Cast");
            Vector3 origin = firePoint != null ? firePoint.position : transform.position;
            Vector3 forward = GetFacingDirection();
            const float halfArc = 38f;
            int count = Mathf.Max(1, skill.projectileCount);
            float distance = Mathf.Max(6f, skill.radius);
            float travelDuration = Mathf.Max(.05f, skill.projectileTravelDuration);
            HashSet<EnemyActor> hit = new HashSet<EnemyActor>();
            List<GameObject> projectiles = new List<GameObject>(count);
            List<Vector3> directions = new List<Vector3>(count);
            List<Vector3> spawnPositions = new List<Vector3>(count);
            List<Vector3> previousPositions = new List<Vector3>(count);

            for (int i = 0; i < count; i++)
            {
                float angle = count <= 1
                    ? 0f
                    : Mathf.Lerp(-halfArc, halfArc, i / (float)(count - 1));
                Vector3 direction = Quaternion.Euler(0f, 0f, angle) * forward;
                Vector3 spawnPosition = origin + skill.projectileSpawnOffset;
                GameObject projectile;
                if (skill.projectileEffectPrefab != null)
                {
                    Quaternion rotation = GetProjectileRotation(
                        spawnPosition, spawnPosition + direction, skill.projectileRotationOffset);
                    projectile = Instantiate(skill.projectileEffectPrefab, spawnPosition, rotation);
                    RegisterSkillEffect(projectile);
                    ApplySkillEffectSorting(projectile);
                }
                else
                {
                    projectile = CreateEffect(
                        spawnPosition, Vector3.one * .3f, skill.effectColor, travelDuration + .1f);
                }

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
                        if (enemy == null || enemy.CombatGroup != CombatGroup || hit.Contains(enemy))
                            continue;
                        if (!SegmentIntersectsEnemy(
                                previous, current, enemy, skill.projectileImpactDistance))
                            continue;

                        hit.Add(enemy);
                        DealSkillDamage(skill, enemy, skill.damageMultiplier);
                    }
                }
                yield return null;
            }

            foreach (GameObject projectile in projectiles)
                if (projectile != null) StopAndDestroyProjectile(projectile);
        }

        private static bool SegmentIntersectsEnemy(
            Vector3 start, Vector3 end, EnemyActor enemy, float padding)
        {
            if (enemy == null)
                return false;

            Bounds bounds = enemy.VisualBounds;
            float extra = Mathf.Max(0f, padding);
            Vector3 delta = end - start;
            float enter = 0f;
            float exit = 1f;
            return ClipSegmentAxis(
                       start.x, delta.x, bounds.min.x - extra, bounds.max.x + extra, ref enter, ref exit) &&
                   ClipSegmentAxis(
                       start.y, delta.y, bounds.min.y - extra, bounds.max.y + extra, ref enter, ref exit);
        }

        private static bool ClipSegmentAxis(
            float origin, float delta, float minimum, float maximum, ref float enter, ref float exit)
        {
            if (Mathf.Abs(delta) <= Mathf.Epsilon)
                return origin >= minimum && origin <= maximum;

            float first = (minimum - origin) / delta;
            float second = (maximum - origin) / delta;
            if (first > second)
                (first, second) = (second, first);
            enter = Mathf.Max(enter, first);
            exit = Mathf.Min(exit, second);
            return enter <= exit;
        }

        private IEnumerator CastLightningBolt(SkillData skill)
        {
            EnemyActor target = GetRandomEnemy();
            if (target == null)
                yield break;

            Vector3 point = target.AimPosition;
            float effectLifetime;
            if (skill.projectileEffectPrefab != null)
            {
                CreateStationaryProjectileEffect(
                    skill.projectileEffectPrefab,
                    point + skill.projectileSpawnOffset,
                    skill.projectileRotationOffset,
                    out effectLifetime);
            }
            else
            {
                effectLifetime = .18f;
                CreateEffect(
                    point + Vector3.up * 1.5f,
                    new Vector3(.25f, 3f, .25f),
                    skill.effectColor,
                    effectLifetime);
            }

            PlayCombatLoop("SFX_LightningBolt_Cast", effectLifetime);

            float requestedInterval = Mathf.Max(.05f, skill.tickInterval);
            int tickCount = Mathf.Max(1, Mathf.CeilToInt(effectLifetime / requestedInterval));
            float actualInterval = effectLifetime / tickCount;
            float damagePerTick = skill.damageMultiplier / tickCount;
            for (int tick = 0; tick < tickCount; tick++)
            {
                DamageArea(skill, point, skill.radius, damagePerTick);
                if (tick + 1 < tickCount)
                    yield return new WaitForSeconds(actualInterval);
            }
        }

        private IEnumerator CastArcaneOrb(SkillData skill)
        {
            PlayCombatLoop("SFX_ArcaneOrb_Loop", skill.duration);
            bool usesPrefab = skill.playerAreaEffectPrefab != null;
            float effectLifetime = skill.playerAreaEffectLifetime > 0f
                ? skill.playerAreaEffectLifetime
                : skill.duration + .25f;
            GameObject orb = usesPrefab
                ? CreatePrefabEffect(
                    skill.playerAreaEffectPrefab,
                    transform.position + skill.playerAreaEffectOffset,
                    Quaternion.identity,
                    effectLifetime)
                : CreateEffect(transform.position, Vector3.one * .45f, skill.effectColor, skill.duration + .25f);
            if (usesPrefab)
                ApplySkillEffectSorting(orb, PlayerAreaEffectSortingOrder);
            float elapsed = 0f;
            float interval = Mathf.Max(.05f, skill.tickInterval);
            while (elapsed < skill.duration)
            {
                if (orb != null)
                {
                    if (usesPrefab)
                    {
                        orb.transform.position = transform.position + skill.playerAreaEffectOffset;
                    }
                    else
                    {
                        float angle = elapsed * 240f * Mathf.Deg2Rad;
                        orb.transform.position = transform.position +
                                                 new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 1.2f;
                    }
                }
                EnemyActor target = FindNearestEnemy(transform.position, Mathf.Max(1f, skill.radius));
                if (target != null)
                {
                    Vector3 shotOrigin = orb != null
                        ? orb.transform.position
                        : transform.position + skill.playerAreaEffectOffset;
                    StartCoroutine(LaunchDirectProjectile(
                        skill, target, shotOrigin, skill.damageMultiplier));
                }
                yield return new WaitForSeconds(interval);
                elapsed += interval;
            }
        }

        private IEnumerator CastWindCutter(SkillData skill)
        {
            PlayCombatSfx("SFX_WindCutter_Fly");
            int count = Mathf.Max(1, skill.projectileCount);
            float halfDistance = Mathf.Max(6f, skill.radius);
            float travelDuration = Mathf.Max(.05f, skill.projectileTravelDuration);
            Vector3 center = GetDensestEnemyPosition(skill.radius);
            const float laneSpacing = 1.1f;
            List<GameObject> projectiles = new List<GameObject>(count);
            List<Vector3> startPositions = new List<Vector3>(count);
            List<Vector3> endPositions = new List<Vector3>(count);
            List<Vector3> previousPositions = new List<Vector3>(count);
            List<HashSet<EnemyActor>> hitByProjectile = new List<HashSet<EnemyActor>>(count);

            for (int i = 0; i < count; i++)
            {
                float laneOffset = (i - (count - 1) * .5f) * laneSpacing;
                Vector3 start = center + new Vector3(-halfDistance, laneOffset, 0f) + skill.projectileSpawnOffset;
                Vector3 end = center + new Vector3(halfDistance, laneOffset, 0f) + skill.projectileSpawnOffset;
                GameObject projectile;
                if (skill.projectileEffectPrefab != null)
                {
                    Quaternion rotation = GetProjectileRotation(start, end, skill.projectileRotationOffset);
                    projectile = Instantiate(skill.projectileEffectPrefab, start, rotation);
                    RegisterSkillEffect(projectile);
                    ApplySkillEffectSorting(projectile);
                }
                else
                {
                    projectile = CreateEffect(
                        start, Vector3.one * .3f, skill.effectColor, travelDuration + .1f);
                }

                projectiles.Add(projectile);
                startPositions.Add(start);
                endPositions.Add(end);
                previousPositions.Add(start);
                hitByProjectile.Add(new HashSet<EnemyActor>());
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
                    Vector3 current = Vector3.Lerp(startPositions[i], endPositions[i], progress);
                    projectile.transform.position = current;
                    previousPositions[i] = current;

                    foreach (EnemyActor enemy in EnemyActor.Active.ToArray())
                    {
                        if (enemy == null || enemy.CombatGroup != CombatGroup ||
                            hitByProjectile[i].Contains(enemy))
                            continue;
                        if (!SegmentIntersectsEnemy(
                                previous, current, enemy, skill.projectileImpactDistance))
                            continue;

                        hitByProjectile[i].Add(enemy);
                        DealSkillDamage(skill, enemy, skill.damageMultiplier);
                    }
                }
                yield return null;
            }

            foreach (GameObject projectile in projectiles)
                if (projectile != null) StopAndDestroyProjectile(projectile);
        }

        private IEnumerator CastMeteor(SkillData skill)
        {
            Vector3 center = GetDensestEnemyPosition(skill.radius);
            for (int i = 0; i < Mathf.Max(1, skill.projectileCount); i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * skill.radius * .55f;
                Vector3 impact = center + new Vector3(randomOffset.x, randomOffset.y, 0f);
                float impactDelay = .35f;
                GameObject meteor;
                if (skill.projectileEffectPrefab != null)
                {
                    meteor = CreateStationaryProjectileEffect(
                        skill.projectileEffectPrefab,
                        impact + skill.projectileSpawnOffset,
                        skill.projectileRotationOffset,
                        out _);
                    impactDelay = GetMeteorImpactDelay(meteor);
                }
                else
                {
                    meteor = CreateEffect(
                        impact, Vector3.one * skill.radius, skill.effectColor, impactDelay);
                }

                StartCoroutine(ResolveMeteorImpact(skill, impact, impactDelay, meteor));
                yield return new WaitForSeconds(.18f);
            }
        }

        private IEnumerator ResolveMeteorImpact(
            SkillData skill, Vector3 impact, float delay, GameObject meteor)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, delay));
            PlayCombatSfx("SFX_Meteor_Impact");
            if (meteor != null)
                StopAndDestroyProjectile(meteor);
            if (skill.explosionEffectPrefab != null)
                CreateExplosionEffect(skill, impact);
            DamageArea(skill, impact, skill.radius, skill.damageMultiplier, null, false);
        }

        private static float GetMeteorImpactDelay(GameObject meteor)
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

        private IEnumerator CastBlizzard(SkillData skill)
        {
            PlayCombatLoop("SFX_Blizzard_Loop", skill.duration);
            Vector3 center = GetDensestEnemyPosition(skill.radius);
            if (skill.worldAreaEffectPrefab != null)
            {
                float effectLifetime = skill.worldAreaEffectLifetime > 0f
                    ? skill.worldAreaEffectLifetime
                    : skill.duration;
                CreatePrefabEffect(
                    skill.worldAreaEffectPrefab,
                    center + skill.worldAreaEffectOffset,
                    Quaternion.Euler(skill.worldAreaEffectRotation),
                    effectLifetime);
            }
            else
            {
                CreateEffect(center, Vector3.one * skill.radius * 1.6f, skill.effectColor, skill.duration);
            }
            float interval = Mathf.Max(.05f, skill.tickInterval);
            for (float elapsed = 0f; elapsed < skill.duration; elapsed += interval)
            {
                foreach (EnemyActor enemy in EnemyActor.Active.ToArray())
                {
                    if (enemy == null || enemy.CombatGroup != CombatGroup || Vector3.Distance(center, enemy.transform.position) > skill.radius) continue;
                    DealSkillDamage(skill, enemy, skill.damageMultiplier);
                    enemy.ApplySlow(skill.slowPercent, interval + .1f);
                }
                yield return new WaitForSeconds(interval);
            }
        }

        private IEnumerator CastBlackHole(SkillData skill)
        {
            PlayCombatLoop("SFX_BlackHole_Loop", skill.duration);
            Vector3 center = GetDensestEnemyPosition(skill.radius);
            if (skill.worldAreaEffectPrefab != null)
            {
                float effectLifetime = skill.worldAreaEffectLifetime > 0f
                    ? skill.worldAreaEffectLifetime
                    : skill.duration + .25f;
                CreatePrefabEffect(
                    skill.worldAreaEffectPrefab,
                    center + skill.worldAreaEffectOffset,
                    Quaternion.Euler(skill.worldAreaEffectRotation),
                    effectLifetime);
            }
            else
            {
                CreateEffect(center, Vector3.one * skill.radius * 1.4f, skill.effectColor, skill.duration + .25f);
            }
            float interval = Mathf.Max(.05f, skill.tickInterval);
            int tickCount = Mathf.Max(1, Mathf.CeilToInt(skill.duration / interval));
            float damagePerTick = skill.damageMultiplier / tickCount;
            float elapsed = 0f;
            float damageTimer = 0f;
            int appliedTicks = 0;
            while (elapsed < skill.duration)
            {
                yield return null;

                float deltaTime = Mathf.Min(Time.deltaTime, skill.duration - elapsed);
                elapsed += deltaTime;
                damageTimer += deltaTime;

                int damageTicksThisFrame = 0;
                while (damageTimer + .0001f >= interval && appliedTicks < tickCount)
                {
                    damageTimer -= interval;
                    appliedTicks++;
                    damageTicksThisFrame++;
                }

                foreach (EnemyActor enemy in EnemyActor.Active.ToArray())
                {
                    if (enemy == null || enemy.CombatGroup != CombatGroup || Vector3.Distance(center, enemy.transform.position) > skill.radius) continue;

                    float distanceToCenter = Vector3.Distance(enemy.transform.position, center);
                    float pullEase = 1f - Mathf.Exp(-Mathf.Max(.01f, skill.pullStrength) * .45f * deltaTime);
                    enemy.PullTowards(center, distanceToCenter * pullEase, deltaTime + .05f);

                    Vector3 closestPoint = enemy.VisualBounds.ClosestPoint(center);
                    bool touchesDamageCore = Vector3.Distance(center, closestPoint) <= skill.blackHoleDamageRadius;
                    if (!touchesDamageCore)
                        continue;

                    for (int tick = 0; tick < damageTicksThisFrame; tick++)
                        DealSkillDamage(skill, enemy, damagePerTick, null, true, false);
                }
            }
            DamageArea(skill, center, skill.radius, skill.secondaryDamageMultiplier, null, true, false);
        }

        private IEnumerator CastStarNova(SkillData skill)
        {
            const float chargeDuration = 1.5f;
            GameObject playerAreaEffect = null;
            if (skill.playerAreaEffectPrefab != null)
            {
                float fullSkillLifetime = chargeDuration +
                                          Mathf.Max(.05f, skill.explosionEffectLifetime) +
                                          Mathf.Max(.05f, skill.projectileTravelDuration) + .25f;
                float effectLifetime = Mathf.Max(skill.playerAreaEffectLifetime, fullSkillLifetime);
                playerAreaEffect = CreatePrefabEffect(
                    skill.playerAreaEffectPrefab,
                    transform.position + skill.playerAreaEffectOffset,
                    Quaternion.identity,
                    effectLifetime);
                playerAreaEffect.transform.SetParent(transform, false);
                playerAreaEffect.transform.localPosition = skill.playerAreaEffectOffset;
                playerAreaEffect.transform.localRotation = Quaternion.identity;
                _playerAttachedSkillEffects.Add(playerAreaEffect);
                ApplySkillEffectSorting(playerAreaEffect, PlayerAreaEffectSortingOrder);
            }

            yield return new WaitForSeconds(chargeDuration);
            PlayCombatSfx("SFX_StarNova_Explosion");

            GameObject explosionEffect;
            if (skill.explosionEffectPrefab != null)
            {
                explosionEffect = CreateExplosionEffect(skill, transform.position);
            }
            else
            {
                explosionEffect = CreateEffect(
                    transform.position,
                    Vector3.one * skill.radius * 1.8f,
                    skill.effectColor,
                    skill.explosionEffectLifetime);
            }

            yield return new WaitForSeconds(Mathf.Max(.05f, skill.explosionEffectLifetime));
            if (explosionEffect != null)
                StopAndDestroyProjectile(explosionEffect);

            PlayCombatSfx("SFX_StarNova_Fragment_Fly");

            List<EnemyActor> fallbackTargets = new List<EnemyActor>();
            foreach (EnemyActor enemy in EnemyActor.Active.ToArray())
            {
                if (enemy != null && enemy.Health > 0f && enemy.CombatGroup == CombatGroup)
                    fallbackTargets.Add(enemy);
            }

            DamageArea(skill, transform.position, skill.radius, skill.damageMultiplier);

            List<EnemyActor> targets = new List<EnemyActor>();
            foreach (EnemyActor enemy in EnemyActor.Active.ToArray())
            {
                if (enemy != null && enemy.Health > 0f && enemy.CombatGroup == CombatGroup)
                    targets.Add(enemy);
            }

            targets.Sort((left, right) =>
                Vector3.SqrMagnitude(left.transform.position - transform.position).CompareTo(
                    Vector3.SqrMagnitude(right.transform.position - transform.position)));

            if (targets.Count == 0)
                targets.AddRange(fallbackTargets);

            if (targets.Count == 0)
            {
                if (playerAreaEffect != null)
                {
                    StopAndDestroyProjectile(playerAreaEffect);
                    _playerAttachedSkillEffects.Remove(playerAreaEffect);
                }
                yield break;
            }

            int count = Mathf.Max(1, skill.projectileCount);
            Vector3 projectileOrigin = transform.position;
            for (int i = 0; i < count; i++)
            {
                EnemyActor target = targets[i % targets.Count];
                StartCoroutine(LaunchDirectProjectile(
                    skill,
                    target,
                    projectileOrigin,
                    skill.secondaryDamageMultiplier,
                    StarNovaProjectileSortingOrder));
            }

            yield return new WaitForSeconds(Mathf.Max(.05f, skill.projectileTravelDuration));
            if (playerAreaEffect != null)
            {
                StopAndDestroyProjectile(playerAreaEffect);
                _playerAttachedSkillEffects.Remove(playerAreaEffect);
            }
        }

        private IEnumerator LaunchProjectile(
            SkillData skill, EnemyActor target, float multiplier, float explosionRadius, float travelDuration)
        {
            if (target == null) yield break;
            Vector3 destination = target.AimPosition;
            Vector3 start = destination + skill.projectileSpawnOffset;
            GameObject projectile;
            if (skill.projectileEffectPrefab != null)
            {
                Quaternion rotation = GetProjectileRotation(start, destination, skill.projectileRotationOffset);
                projectile = Instantiate(skill.projectileEffectPrefab, start, rotation);
                RegisterSkillEffect(projectile);
                ApplySkillEffectSorting(projectile);
            }
            else
            {
                projectile = CreateEffect(start, Vector3.one * .3f, skill.effectColor, travelDuration + .1f);
            }

            float elapsed = 0f;
            Vector3 previousProjectilePosition = start;
            while (elapsed < travelDuration)
            {
                if (target != null && target.Health > 0f) destination = target.AimPosition;
                elapsed += Time.deltaTime;
                if (projectile != null)
                {
                    projectile.transform.position = Vector3.Lerp(
                        start, destination, Mathf.Clamp01(elapsed / travelDuration));
                    if (skill.projectileEffectPrefab != null)
                        projectile.transform.rotation = GetProjectileRotation(
                            projectile.transform.position, destination, skill.projectileRotationOffset);

                    if (target != null && target.Health > 0f &&
                        SegmentIntersectsEnemy(
                            previousProjectilePosition,
                            projectile.transform.position,
                            target,
                            skill.projectileImpactDistance))
                    {
                        destination = target.AimPosition;
                        break;
                    }
                    previousProjectilePosition = projectile.transform.position;
                }
                yield return null;
            }

            if (explosionRadius > 0f)
            {
                if (projectile != null)
                    StopAndDestroyProjectile(projectile);

                if (skill.explosionEffectPrefab != null)
                    CreateExplosionEffect(skill, destination);

                DamageArea(skill, destination, explosionRadius, multiplier);
            }
            else if (target != null && target.Health > 0f)
            {
                DealSkillDamage(skill, target, multiplier, projectile);
            }
            else if (projectile != null)
            {
                StopAndDestroyProjectile(projectile);
            }
        }

        private IEnumerator LaunchDirectProjectile(
            SkillData skill,
            EnemyActor target,
            Vector3 origin,
            float multiplier,
            int sortingOrder = SkillEffectSortingOrder)
        {
            if (skill == null || target == null)
                yield break;

            Vector3 start = origin + skill.projectileSpawnOffset;
            Vector3 destination = target.AimPosition;
            float travelDuration = Mathf.Max(.05f, skill.projectileTravelDuration);
            GameObject projectile;
            if (skill.projectileEffectPrefab != null)
            {
                Quaternion rotation = GetProjectileRotation(start, destination, skill.projectileRotationOffset);
                projectile = Instantiate(skill.projectileEffectPrefab, start, rotation);
                RegisterSkillEffect(projectile);
                ApplySkillEffectSorting(projectile, sortingOrder);
            }
            else
            {
                projectile = CreateEffect(
                    start, Vector3.one * .25f, skill.effectColor, travelDuration + .1f);
            }

            float elapsed = 0f;
            Vector3 previous = start;
            while (elapsed < travelDuration)
            {
                if (target != null && target.Health > 0f)
                    destination = target.AimPosition;

                elapsed += Time.deltaTime;
                if (projectile == null)
                    yield break;

                Vector3 current = Vector3.Lerp(
                    start, destination, Mathf.Clamp01(elapsed / travelDuration));
                projectile.transform.position = current;
                if (skill.projectileEffectPrefab != null)
                    projectile.transform.rotation = GetProjectileRotation(
                        current, destination, skill.projectileRotationOffset);

                if (target != null && target.Health > 0f &&
                    SegmentIntersectsEnemy(
                        previous, current, target, skill.projectileImpactDistance))
                {
                    DealSkillDamage(skill, target, multiplier, projectile);
                    yield break;
                }

                previous = current;
                yield return null;
            }

            if (projectile != null)
                StopAndDestroyProjectile(projectile);
        }

        private static void StopAndDestroyProjectile(GameObject projectile)
        {
            if (projectile == null)
                return;

            foreach (Renderer renderer in projectile.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            foreach (TrailRenderer trail in projectile.GetComponentsInChildren<TrailRenderer>(true))
                trail.Clear();
            foreach (ParticleSystem particles in projectile.GetComponentsInChildren<ParticleSystem>(true))
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            projectile.SetActive(false);
            Destroy(projectile);
        }

        private static Quaternion GetProjectileRotation(Vector3 origin, Vector3 destination, Vector3 rotationOffset)
        {
            Vector3 direction = destination - origin;
            Quaternion facing = direction.sqrMagnitude > Mathf.Epsilon
                ? Quaternion.FromToRotation(Vector3.right, direction.normalized)
                : Quaternion.identity;
            return facing * Quaternion.Euler(rotationOffset);
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

        private void DamageArea(
            SkillData skill,
            Vector3 center,
            float radius,
            float multiplier,
            GameObject impactProjectile = null,
            bool createHitEffect = true,
            bool applyKnockback = true)
        {
            GameObject projectileToStop = impactProjectile;
            foreach (EnemyActor enemy in EnemyActor.Active.ToArray())
            {
                if (enemy == null || enemy.CombatGroup != CombatGroup ||
                    Vector3.Distance(center, enemy.AimPosition) > radius)
                    continue;

                DealSkillDamage(skill, enemy, multiplier, projectileToStop, createHitEffect, applyKnockback);
                projectileToStop = null;
            }

            if (projectileToStop != null)
                StopAndDestroyProjectile(projectileToStop);
        }

        private void DealSkillDamage(
            SkillData skill,
            EnemyActor enemy,
            float multiplier,
            GameObject impactProjectile = null,
            bool createHitEffect = true,
            bool applyKnockback = true)
        {
            if (enemy == null || multiplier <= 0f)
            {
                if (impactProjectile != null)
                    StopAndDestroyProjectile(impactProjectile);
                return;
            }

            if (impactProjectile != null)
                StopAndDestroyProjectile(impactProjectile);

            if (createHitEffect && skill.hitEffectPrefab != null)
                CreateHitEffect(skill, enemy.AimPosition);

            if (skill.behavior == SkillBehavior.IceSpear)
                PlayCombatSfx("SFX_IceSpear_Hit");
            else if (skill.behavior == SkillBehavior.ArcaneOrb ||
                     skill.behavior == SkillBehavior.MagicMissile)
                PlayCombatSfx("SFX_ArcaneOrb_Hit");

            float levelMultiplier = SkillBalance.EquippedEffectMultiplier(GetEquippedSkillLevel(skill));
            float damage = AttackDamage * multiplier * SkillDamageMultiplier * levelMultiplier;
            enemy.TakeDamage(ApplyBossDamage(damage, enemy), applyKnockback);
        }

        private int GetEquippedSkillLevel(SkillData skill)
        {
            return _skillController?.GetLevel(skill) ?? 1;
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
            RegisterSkillEffect(effect);
            effect.name = "SkillEffect";
            effect.transform.position = position;
            effect.transform.localScale = scale;
            effect.transform.rotation = rotation ?? Quaternion.identity;
            Renderer renderer = effect.GetComponent<Renderer>();
            renderer.material.color = color;
            ApplySkillEffectSorting(effect);
            Destroy(effect.GetComponent<Collider>());
            Destroy(effect, Mathf.Max(.05f, lifetime));
            return effect;
        }

        private GameObject CreatePrefabEffect(
            GameObject prefab, Vector3 position, Quaternion rotation, float lifetime)
        {
            if (prefab == null) return null;
            GameObject effect = Instantiate(prefab, position, rotation);
            RegisterSkillEffect(effect);
            ApplySkillEffectSorting(effect);
            Destroy(effect, Mathf.Max(.05f, lifetime));
            return effect;
        }

        private GameObject CreateExplosionEffect(SkillData skill, Vector3 center)
        {
            if (skill == null || skill.explosionEffectPrefab == null)
                return null;

            GameObject prefab = skill.explosionEffectPrefab;
            if (skill.behavior == SkillBehavior.FireBall)
                PlayCombatSfx("SFX_FireBall_Explosion");
            Quaternion rotation = prefab.transform.rotation * Quaternion.Euler(skill.explosionEffectRotation);
            GameObject effect = Instantiate(prefab, center + skill.explosionEffectOffset, rotation);
            RegisterSkillEffect(effect);

            Vector3 multiplier = skill.explosionEffectScaleMultiplier;
            if (multiplier == Vector3.zero)
                multiplier = Vector3.one;
            effect.transform.localScale = Vector3.Scale(prefab.transform.localScale, multiplier);

            ApplySkillEffectSorting(effect);
            Destroy(effect, Mathf.Max(.05f, skill.explosionEffectLifetime));
            return effect;
        }

        private GameObject CreateHitEffect(SkillData skill, Vector3 center)
        {
            if (skill == null || skill.hitEffectPrefab == null)
                return null;

            GameObject prefab = skill.hitEffectPrefab;
            GameObject effect = Instantiate(
                prefab,
                center + skill.hitEffectOffset,
                prefab.transform.rotation);
            RegisterSkillEffect(effect);
            effect.transform.localScale = prefab.transform.localScale;
            ApplySkillEffectSorting(effect);
            Destroy(effect, Mathf.Max(.05f, skill.hitEffectLifetime));
            return effect;
        }

        private GameObject CreateStationaryProjectileEffect(
            GameObject prefab, Vector3 position, Vector3 rotationOffset, out float lifetime)
        {
            lifetime = .05f;
            if (prefab == null)
                return null;

            Quaternion rotation = prefab.transform.rotation * Quaternion.Euler(rotationOffset);
            GameObject effect = Instantiate(prefab, position, rotation);
            RegisterSkillEffect(effect);
            effect.transform.localScale = prefab.transform.localScale;
            ApplySkillEffectSorting(effect);

            foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particles.main;
                lifetime = Mathf.Max(
                    lifetime,
                    main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
            }
            Destroy(effect, lifetime);
            return effect;
        }

        public void ClearActiveSkills()
        {
            StopAllCoroutines();

            for (int i = _playerAttachedSkillEffects.Count - 1; i >= 0; i--)
            {
                GameObject effect = _playerAttachedSkillEffects[i];
                if (effect != null)
                {
                    effect.SetActive(false);
                    Destroy(effect);
                }
            }
            _playerAttachedSkillEffects.Clear();

            if (_skillEffectRoot == null)
                return;

            for (int i = _skillEffectRoot.childCount - 1; i >= 0; i--)
            {
                GameObject effect = _skillEffectRoot.GetChild(i).gameObject;
                if (effect == null)
                    continue;
                effect.SetActive(false);
                Destroy(effect);
            }
        }

        private void EnsureSkillEffectRoot()
        {
            if (_skillEffectRoot != null)
                return;

            GameObject root = new GameObject($"SkillEffectRoot_Group{CombatGroup}");
            _skillEffectRoot = root.transform;
        }

        private GameObject RegisterSkillEffect(GameObject effect)
        {
            if (effect == null)
                return null;
            EnsureSkillEffectRoot();
            effect.transform.SetParent(_skillEffectRoot, true);
            return effect;
        }

        private void ApplySkillEffectSorting(
            GameObject effect, int sortingOrder = SkillEffectSortingOrder)
        {
            if (effect == null)
                return;

            int sortingLayerId = _visualRenderer != null ? _visualRenderer.sortingLayerID : 0;
            foreach (SortingGroup group in effect.GetComponentsInChildren<SortingGroup>(true))
            {
                group.sortingLayerID = sortingLayerId;
                group.sortingOrder = sortingOrder;
            }

            foreach (Renderer renderer in effect.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sortingLayerID = sortingLayerId;
                renderer.sortingOrder = sortingOrder;
            }
        }

        private EnemyActor FindNearestEnemy(float range)
        {
            return FindNearestEnemy(transform.position, range);
        }

        private Vector3 GetFacingDirection()
        {
            return _visualRenderer != null && _visualRenderer.flipX ? Vector3.left : Vector3.right;
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

        private EnemyActor GetRandomEnemy()
        {
            EnemyActor[] enemies = EnemyActor.Active.ToArray();
            if (enemies.Length == 0)
                return null;

            int start = Random.Range(0, enemies.Length);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyActor enemy = enemies[(start + i) % enemies.Length];
                if (enemy != null && enemy.CombatGroup == CombatGroup && enemy.Health > 0f)
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
                if (candidate == null || candidate.CombatGroup != CombatGroup)
                    continue;
                int count = 0;
                foreach (EnemyActor other in enemies)
                {
                    if (other != null && other.CombatGroup == CombatGroup &&
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
