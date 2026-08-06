using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    public sealed class EnemyActor : MonoBehaviour
    {
        private const int BossSortingOrder = 0;
        private const int BossHitFlashSortingOrder = 1;
        private const int NormalEnemySortingOrder = 10;
        private const int NormalEnemyHitFlashSortingOrder = 11;
        private const float NormalEnemyDamageMultiplier = .05f;

        public static readonly List<EnemyActor> Active = new();

        [Header("공통 프리팹 연결")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer visualRenderer;
        [SerializeField] private Animator visualAnimator;
        [SerializeField] private Transform healthBarAnchor;
        [SerializeField] private Image healthBarFill;

        [Header("피격 및 사망 연출")]
        [SerializeField, Min(0f)] private float knockbackDistance = 0.28f;
        [SerializeField, Min(0.01f)] private float knockbackDuration = 0.12f;
        [SerializeField, Min(0.1f)] private float minimumPlayerDistance = 1.4f;
        [SerializeField, Min(0f)] private float deathSlideDistance = 0.65f;
        [SerializeField, Min(0.01f)] private float deathDuration = 0.5f;

        [Header("Walk Visual Motion")]
        [SerializeField, Min(0f)] private float walkBounceHeight = 0.1f;
        [SerializeField, Min(0.1f)] private float walkBounceSpeed = 7f;
        [SerializeField, Range(0f, 0.2f)] private float walkSquashAmount = 0.06f;
        [SerializeField, Min(0.1f)] private float walkMotionSmoothness = 18f;

        [Header("Enemy Separation")]
        [SerializeField, Min(0.1f)] private float separationRadius = 0.55f;
        [SerializeField, Min(0.1f)] private float bossSeparationRadius = 1.2f;
        [SerializeField, Min(0.1f)] private float separationSpeed = 4f;

        [Header("피격 마스크")]
        [SerializeField] private Color hitFlashColor = new Color(1f, 0f, 0f, .58f);
        [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.14f;
        [SerializeField, Min(0f)] private float hitFlashCooldown = 1f;

        public EnemyData Data { get; private set; }
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public bool IsBoss { get; private set; }
        public int CombatGroup { get; private set; }
        public bool IsBeingKnockedBack => _isKnockedBack;
        public float SeparationFootprintRadius => GetSeparationFootprintRadius();
        public Vector3 AimPosition => visualRenderer != null ? visualRenderer.bounds.center : transform.position;
        public Bounds VisualBounds => visualRenderer != null
            ? visualRenderer.bounds
            : new Bounds(transform.position, Vector3.one);

        public event Action<EnemyActor> Died;

        public void SetWorldHealthBarVisible(bool visible)
        {
            if (healthBarAnchor != null)
                healthBarAnchor.gameObject.SetActive(visible && !_isDead);
        }

        private PlayerAutoCombat _target;
        private float _attackDamage;
        private float _attackTimer;
        private bool _isKnockedBack;
        private bool _isDead;
        private Coroutine _knockbackRoutine;
        private Coroutine _hitFlashRoutine;
        private float _nextHitFlashTime;
        private SpriteRenderer _hitFlashRenderer;
        private float _moveSpeedMultiplier = 1f;
        private float _slowUntil;
        private float _externalMovementUntil;
        private Vector3 _visualBaseLocalPosition;
        private Vector3 _visualBaseLocalScale = Vector3.one;
        private float _walkPhaseOffset;
        private float _walkBlend;
        private bool _isMoving;
        private float _separationFootprintRadius;
        private Vector3 _healthBarBaseOffset;

        private static readonly int AttackStateHash = Animator.StringToHash("Base Layer.Anim_Attack");

        public void Initialize(
            EnemyData data,
            PlayerAutoCombat target,
            double healthMultiplier,
            double attackMultiplier,
            bool boss,
            float bossHealthMultiplier,
            float bossAttackMultiplier)
        {
            Data = data;
            _target = target;
            CombatGroup = target != null ? target.CombatGroup : 0;
            IsBoss = boss;

            double health = data.baseHealth * Math.Max(1d, healthMultiplier) * (boss ? bossHealthMultiplier : 1f);
            double attack = data.baseAttack * Math.Max(1d, attackMultiplier) * (boss ? bossAttackMultiplier : 1f);
            MaxHealth = (float)Math.Min(float.MaxValue, health);
            Health = MaxHealth;
            _attackDamage = (float)Math.Min(float.MaxValue, attack) *
                            (boss ? 1f : NormalEnemyDamageMultiplier);
            _isDead = false;
            _isKnockedBack = false;
            _attackTimer = data.attackInterval * UnityEngine.Random.Range(.5f, 1f);
            _moveSpeedMultiplier = 1f;
            _slowUntil = 0f;
            _externalMovementUntil = 0f;
            _nextHitFlashTime = 0f;

            ApplyVisualData(data, boss);
            CacheSeparationFootprint();
            CacheWalkVisualDefaults();
            if (healthBarAnchor != null)
                healthBarAnchor.gameObject.SetActive(!boss);
            UpdateHealthBar();
            UpdateFacing();
            gameObject.name = boss ? $"Boss_{data.displayName}" : data.displayName;
        }

        private void ApplyVisualData(EnemyData data, bool boss)
        {
            AutoFindVisualReferences();

            float bossScale = boss ? 1.8f : 1f;
            if (visualRoot != null)
            {
                visualRoot.localPosition = data.visualOffset;
                visualRoot.localScale = data.visualScale * bossScale;
            }
            else
            {
                transform.localScale = data.visualScale * bossScale;
            }

            if (visualRenderer != null)
            {
                if (data.visualSprite != null)
                    visualRenderer.sprite = data.visualSprite;
                visualRenderer.color = data.visualColor;
                // Fixed depth: boss < normal enemy < player (player order 100).
                visualRenderer.sortingOrder = boss ? BossSortingOrder : NormalEnemySortingOrder;
                EnsureHitFlashRenderer();
            }

            if (visualAnimator != null)
            {
                visualAnimator.runtimeAnimatorController = data.animatorController;
                visualAnimator.enabled = data.animatorController != null;
            }

            if (healthBarAnchor != null)
            {
                _healthBarBaseOffset = data.healthBarOffset;
                healthBarAnchor.localPosition = _healthBarBaseOffset;
            }
        }

        [ContextMenu("Auto Find Visual References")]
        private void AutoFindVisualReferences()
        {
            if (visualRenderer == null)
                visualRenderer = GetComponentInChildren<SpriteRenderer>(true);
            if (visualAnimator == null)
                visualAnimator = GetComponentInChildren<Animator>(true);
            if (visualRoot == null && visualRenderer != null)
                visualRoot = visualRenderer.transform;
            if (healthBarAnchor == null)
                healthBarAnchor = FindChildByName(transform, "HealthBarAnchor");
            if (healthBarFill == null && healthBarAnchor != null)
            {
                foreach (Image image in healthBarAnchor.GetComponentsInChildren<Image>(true))
                {
                    if (image.name == "Fill")
                    {
                        healthBarFill = image;
                        break;
                    }
                }
            }
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            foreach (Transform child in root)
            {
                if (child.name == childName)
                    return child;

                Transform nested = FindChildByName(child, childName);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private void OnEnable()
        {
            if (!Active.Contains(this))
                Active.Add(this);
        }

        private void OnDisable() => Active.Remove(this);

        private void Update()
        {
            if (_isDead || Data == null || _target == null || !_target.IsAlive)
                return;

            // Bosses stay in place, but keep the same bounce/squash visual motion
            // as moving normal enemies so they do not look frozen during battle.
            _isMoving = IsBoss;
            UpdateFacing();
            if (_isKnockedBack || Time.time < _externalMovementUntil)
                return;

            if (_moveSpeedMultiplier < 1f && Time.time >= _slowUntil)
                _moveSpeedMultiplier = 1f;

            float distance = Vector3.Distance(transform.position, _target.transform.position);
            float playerSeparationDistance = !IsBoss
                ? Mathf.Max(minimumPlayerDistance,
                    GetSeparationFootprintRadius() + _target.SeparationFootprintRadius)
                : minimumPlayerDistance;
            float stopDistance = Mathf.Max(Data.attackRange, playerSeparationDistance);
            if (!IsBoss && distance < playerSeparationDistance)
            {
                Vector3 away = transform.position - _target.transform.position;
                away.z = 0f;
                if (away.sqrMagnitude <= Mathf.Epsilon)
                    away = visualRenderer != null && visualRenderer.flipX ? Vector3.left : Vector3.right;

                Vector3 separationPoint = _target.transform.position + away.normalized * playerSeparationDistance;
                separationPoint.z = transform.position.z;
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    separationPoint,
                    Data.moveSpeed * _moveSpeedMultiplier * Time.deltaTime);
                _isMoving = true;
                return;
            }

            if (distance > stopDistance)
            {
                if (!IsBoss)
                {
                    Vector3 direction = (_target.transform.position - transform.position).normalized;
                    Vector3 destination = _target.transform.position - direction * stopDistance;
                    destination.z = transform.position.z;
                    transform.position = Vector3.MoveTowards(
                        transform.position,
                        destination,
                        Data.moveSpeed * _moveSpeedMultiplier * Time.deltaTime);
                    _isMoving = true;
                    return;
                }
            }

            _attackTimer -= Time.deltaTime;
            if (_attackTimer > 0f)
                return;

            _attackTimer = Mathf.Max(.1f, Data.attackInterval);
            if (visualAnimator != null && visualAnimator.enabled)
                visualAnimator.CrossFade(AttackStateHash, .05f);
            _target.TakeDamage(_attackDamage);
        }

        private void LateUpdate()
        {
            ApplyEnemySeparation();
            UpdateWalkVisualMotion();

            if (_hitFlashRenderer != null && visualRenderer != null)
            {
                _hitFlashRenderer.sprite = visualRenderer.sprite;
                _hitFlashRenderer.flipX = visualRenderer.flipX;
                _hitFlashRenderer.flipY = visualRenderer.flipY;
            }
        }

        private void ApplyEnemySeparation()
        {
            if (_isDead || IsBoss || !isActiveAndEnabled || Time.time < _externalMovementUntil)
                return;

            Vector3 push = Vector3.zero;
            float ownRadius = GetSeparationFootprintRadius();
            foreach (EnemyActor other in Active.ToArray())
            {
                if (other == null || other == this || other._isDead ||
                    other.CombatGroup != CombatGroup)
                    continue;

                Vector3 offset = transform.position - other.transform.position;
                offset.z = 0f;
                float requiredDistance = ownRadius + other.GetSeparationFootprintRadius();
                float distance = offset.magnitude;
                if (distance >= requiredDistance)
                    continue;

                if (distance <= 0.001f)
                {
                    float angle = Mathf.Abs(GetInstanceID() - other.GetInstanceID()) % 360;
                    offset = Quaternion.Euler(0f, 0f, angle) * Vector3.right;
                    distance = 0f;
                }

                float penetration = requiredDistance - distance;
                push += offset.normalized * penetration;
            }

            if (push.sqrMagnitude <= 0.0001f)
                return;

            float moveDistance = Mathf.Min(push.magnitude, separationSpeed * Time.deltaTime);
            transform.position += push.normalized * moveDistance;
            _isMoving = true;
        }

        private void CacheSeparationFootprint()
        {
            float minimum = IsBoss ? bossSeparationRadius : separationRadius;
            if (visualRenderer == null || visualRenderer.sprite == null)
            {
                _separationFootprintRadius = minimum;
                return;
            }

            // Use the rendered world size, but weight height lightly because tall
            // sprites are generally foot-pivoted and should not reserve their full
            // vertical height on the ground.
            Vector2 size = visualRenderer.bounds.size;
            float visualFootprint = Mathf.Max(size.x * 0.42f, size.y * 0.16f);
            _separationFootprintRadius = Mathf.Clamp(
                visualFootprint,
                minimum,
                IsBoss ? 4f : 2f);
        }

        private float GetSeparationFootprintRadius()
        {
            if (_separationFootprintRadius <= 0f)
                CacheSeparationFootprint();
            return _separationFootprintRadius;
        }

        private void CacheWalkVisualDefaults()
        {
            if (visualRoot == null)
                return;
            Transform target = visualRoot;
            _visualBaseLocalPosition = target.localPosition;
            _visualBaseLocalScale = target.localScale;
            _walkPhaseOffset = Mathf.Abs(GetInstanceID() % 1000) * 0.017f;
            _walkBlend = 0f;
            _isMoving = false;
        }

        private void UpdateWalkVisualMotion()
        {
            if (_isDead || visualRoot == null)
                return;

            Transform target = visualRoot;
            float desiredBlend = _isMoving && !_isKnockedBack ? 1f : 0f;
            _walkBlend = Mathf.MoveTowards(
                _walkBlend,
                desiredBlend,
                Time.deltaTime * walkMotionSmoothness);

            float phase = Time.time * walkBounceSpeed + _walkPhaseOffset;
            float lift = Mathf.Abs(Mathf.Sin(phase));
            Vector3 desiredPosition = _visualBaseLocalPosition +
                                      Vector3.up * (lift * walkBounceHeight * _walkBlend);

            float contact = 1f - lift;
            float scaleX = 1f + walkSquashAmount * contact - walkSquashAmount * 0.35f * lift;
            float scaleY = 1f - walkSquashAmount * contact + walkSquashAmount * 0.55f * lift;
            Vector3 motionScale = new Vector3(scaleX, scaleY, 1f);
            Vector3 desiredScale = Vector3.Scale(
                _visualBaseLocalScale,
                Vector3.Lerp(Vector3.one, motionScale, _walkBlend));

            float smoothing = 1f - Mathf.Exp(-walkMotionSmoothness * Time.deltaTime);
            target.localPosition = Vector3.Lerp(target.localPosition, desiredPosition, smoothing);
            target.localScale = Vector3.Lerp(target.localScale, desiredScale, smoothing);
        }

        public void TakeDamage(float amount, bool applyKnockback = true)
        {
            if (_isDead || Health <= 0f)
                return;

            Health -= Mathf.Max(0f, amount);
            UpdateHealthBar();
            PlayHitFlash();
            if (Health > 0f)
            {
                if (applyKnockback && !IsBoss)
                    PlayKnockback();
                return;
            }

            Health = 0f;
            BeginDeath();
        }

        public void ApplySlow(float slowPercent, float duration)
        {
            if (_isDead || duration <= 0f)
                return;

            float multiplier = 1f - Mathf.Clamp(slowPercent, 0f, .95f);
            _moveSpeedMultiplier = Mathf.Min(_moveSpeedMultiplier, multiplier);
            _slowUntil = Mathf.Max(_slowUntil, Time.time + duration);
        }

        public void PullTowards(Vector3 center, float distance, float movementLockDuration)
        {
            if (_isDead || distance <= 0f)
                return;

            center.z = transform.position.z;
            transform.position = Vector3.MoveTowards(transform.position, center, distance);
            _externalMovementUntil = Mathf.Max(
                _externalMovementUntil,
                Time.time + Mathf.Max(0f, movementLockDuration));
            _isMoving = true;
        }

        private void UpdateHealthBar()
        {
            if (healthBarFill == null)
                return;

            healthBarFill.fillAmount = MaxHealth <= 0f
                ? 0f
                : Mathf.Clamp01(Health / MaxHealth);
        }

        private void EnsureHitFlashRenderer()
        {
            if (_hitFlashRenderer != null || visualRenderer == null)
                return;

            Transform existing = visualRenderer.transform.Find("HitFlashOverlay");
            if (existing != null)
                _hitFlashRenderer = existing.GetComponent<SpriteRenderer>();

            if (_hitFlashRenderer == null)
            {
                GameObject overlay = new GameObject("HitFlashOverlay");
                overlay.layer = visualRenderer.gameObject.layer;
                overlay.transform.SetParent(visualRenderer.transform, false);
                overlay.transform.localPosition = Vector3.zero;
                overlay.transform.localRotation = Quaternion.identity;
                overlay.transform.localScale = Vector3.one;
                _hitFlashRenderer = overlay.AddComponent<SpriteRenderer>();
            }

            _hitFlashRenderer.sprite = visualRenderer.sprite;
            _hitFlashRenderer.sharedMaterial = visualRenderer.sharedMaterial;
            _hitFlashRenderer.sortingLayerID = visualRenderer.sortingLayerID;
            _hitFlashRenderer.sortingOrder = IsBoss
                ? BossHitFlashSortingOrder
                : NormalEnemyHitFlashSortingOrder;
            _hitFlashRenderer.flipX = visualRenderer.flipX;
            _hitFlashRenderer.flipY = visualRenderer.flipY;
            _hitFlashRenderer.color = new Color(hitFlashColor.r, hitFlashColor.g, hitFlashColor.b, 0f);
            _hitFlashRenderer.enabled = false;
        }

        private void PlayHitFlash()
        {
            if (Time.time < _nextHitFlashTime)
                return;

            EnsureHitFlashRenderer();
            if (_hitFlashRenderer == null || !isActiveAndEnabled)
                return;

            _nextHitFlashTime = Time.time + hitFlashCooldown;
            if (_hitFlashRoutine != null)
                StopCoroutine(_hitFlashRoutine);
            _hitFlashRoutine = StartCoroutine(HitFlashRoutine());
        }

        private IEnumerator HitFlashRoutine()
        {
            _hitFlashRenderer.enabled = true;
            float elapsed = 0f;

            while (elapsed < hitFlashDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / hitFlashDuration);
                Color color = hitFlashColor;
                color.a = hitFlashColor.a * (1f - progress);
                _hitFlashRenderer.color = color;
                yield return null;
            }

            _hitFlashRenderer.enabled = false;
            _hitFlashRoutine = null;
        }

        private void UpdateFacing()
        {
            if (visualRenderer == null || _target == null)
                return;

            // 모든 기본 스프라이트가 왼쪽을 바라본다는 기준이다.
            // SpriteRenderer만 반전하여 자식 체력바는 뒤집히지 않게 한다.
            visualRenderer.flipX = _target.transform.position.x > transform.position.x;
            if (healthBarAnchor != null)
            {
                Vector3 offset = _healthBarBaseOffset;
                offset.x = visualRenderer.flipX ? -_healthBarBaseOffset.x : _healthBarBaseOffset.x;
                healthBarAnchor.localPosition = offset;
            }
            if (_hitFlashRenderer != null)
                _hitFlashRenderer.flipX = visualRenderer.flipX;
        }

        private void PlayKnockback()
        {
            if (!isActiveAndEnabled || knockbackDistance <= 0f)
                return;

            if (_knockbackRoutine != null)
                StopCoroutine(_knockbackRoutine);
            _knockbackRoutine = StartCoroutine(KnockbackRoutine());
        }

        private IEnumerator KnockbackRoutine()
        {
            _isKnockedBack = true;
            Vector3 start = transform.position;
            Vector3 end = start + Vector3.right * GetAwayDirection() * knockbackDistance;
            float elapsed = 0f;

            while (elapsed < knockbackDuration && !_isDead)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / knockbackDuration);
                float eased = 1f - (1f - progress) * (1f - progress);
                transform.position = Vector3.LerpUnclamped(start, end, eased);
                yield return null;
            }

            if (!_isDead)
                transform.position = end;
            _isKnockedBack = false;
            _knockbackRoutine = null;
        }

        private void BeginDeath()
        {
            _isDead = true;
            _isMoving = false;
            _isKnockedBack = false;
            if (_knockbackRoutine != null)
            {
                StopCoroutine(_knockbackRoutine);
                _knockbackRoutine = null;
            }

            Active.Remove(this);
            if (healthBarAnchor != null)
                healthBarAnchor.gameObject.SetActive(false);
            if (visualAnimator != null)
                visualAnimator.enabled = false;

            Died?.Invoke(this);
            StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            Transform rollingTransform = visualRoot != null ? visualRoot : transform;
            Vector3 startPosition = transform.position;
            Vector3 endPosition = startPosition + Vector3.right * GetAwayDirection() * deathSlideDistance;
            Quaternion startRotation = rollingTransform.localRotation;
            float rollAngle = -180f * GetAwayDirection();
            Color startColor = visualRenderer != null ? visualRenderer.color : Color.white;
            float elapsed = 0f;

            while (elapsed < deathDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / deathDuration);
                float eased = 1f - (1f - progress) * (1f - progress);
                transform.position = Vector3.LerpUnclamped(startPosition, endPosition, eased);
                rollingTransform.localRotation = startRotation * Quaternion.Euler(0f, 0f, rollAngle * eased);

                if (visualRenderer != null)
                {
                    Color faded = startColor;
                    faded.a = startColor.a * (1f - progress);
                    visualRenderer.color = faded;
                }

                yield return null;
            }

            Destroy(gameObject);
        }

        private float GetAwayDirection()
        {
            if (_target == null)
                return visualRenderer != null && visualRenderer.flipX ? -1f : 1f;

            float difference = transform.position.x - _target.transform.position.x;
            return Mathf.Approximately(difference, 0f) ? 1f : Mathf.Sign(difference);
        }
    }
}
