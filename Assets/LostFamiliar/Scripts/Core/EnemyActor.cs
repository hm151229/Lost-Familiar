using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LostFamiliar.Battle
{
    public sealed class EnemyActor : MonoBehaviour
    {
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

        [Header("피격 마스크")]
        [SerializeField] private Color hitFlashColor = new Color(1f, 0f, 0f, .58f);
        [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.14f;

        public EnemyData Data { get; private set; }
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public bool IsBoss { get; private set; }
        public bool IsBeingKnockedBack => _isKnockedBack;

        public event Action<EnemyActor> Died;

        private PlayerAutoCombat _target;
        private float _attackDamage;
        private float _attackTimer;
        private bool _isKnockedBack;
        private bool _isDead;
        private Coroutine _knockbackRoutine;
        private Coroutine _hitFlashRoutine;
        private SpriteRenderer _hitFlashRenderer;
        private float _moveSpeedMultiplier = 1f;
        private float _slowUntil;

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
            IsBoss = boss;

            double health = data.baseHealth * Math.Max(1d, healthMultiplier) * (boss ? bossHealthMultiplier : 1f);
            double attack = data.baseAttack * Math.Max(1d, attackMultiplier) * (boss ? bossAttackMultiplier : 1f);
            MaxHealth = (float)Math.Min(float.MaxValue, health);
            Health = MaxHealth;
            _attackDamage = (float)Math.Min(float.MaxValue, attack);
            _isDead = false;
            _isKnockedBack = false;
            _moveSpeedMultiplier = 1f;
            _slowUntil = 0f;

            ApplyVisualData(data, boss);
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
                EnsureHitFlashRenderer();
            }

            if (visualAnimator != null)
            {
                visualAnimator.runtimeAnimatorController = data.animatorController;
                visualAnimator.enabled = data.animatorController != null;
            }

            if (healthBarAnchor != null)
                healthBarAnchor.localPosition = data.healthBarOffset;
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

            UpdateFacing();
            if (_isKnockedBack)
                return;

            if (_moveSpeedMultiplier < 1f && Time.time >= _slowUntil)
                _moveSpeedMultiplier = 1f;

            float distance = Vector3.Distance(transform.position, _target.transform.position);
            float stopDistance = Mathf.Max(Data.attackRange, minimumPlayerDistance);
            if (!IsBoss && distance < minimumPlayerDistance)
            {
                Vector3 away = transform.position - _target.transform.position;
                away.z = 0f;
                if (away.sqrMagnitude <= Mathf.Epsilon)
                    away = visualRenderer != null && visualRenderer.flipX ? Vector3.left : Vector3.right;

                Vector3 separationPoint = _target.transform.position + away.normalized * minimumPlayerDistance;
                separationPoint.z = transform.position.z;
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    separationPoint,
                    Data.moveSpeed * _moveSpeedMultiplier * Time.deltaTime);
                return;
            }

            if (distance > stopDistance)
            {
                if (IsBoss)
                    return;

                Vector3 direction = (_target.transform.position - transform.position).normalized;
                Vector3 destination = _target.transform.position - direction * stopDistance;
                destination.z = transform.position.z;
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    destination,
                    Data.moveSpeed * _moveSpeedMultiplier * Time.deltaTime);
                return;
            }

            _attackTimer += Time.deltaTime;
            if (_attackTimer < Data.attackInterval)
                return;

            _attackTimer = 0f;
            _target.TakeDamage(_attackDamage);
        }

        private void LateUpdate()
        {
            if (_hitFlashRenderer == null || visualRenderer == null)
                return;

            _hitFlashRenderer.sprite = visualRenderer.sprite;
            _hitFlashRenderer.flipX = visualRenderer.flipX;
            _hitFlashRenderer.flipY = visualRenderer.flipY;
        }

        public void TakeDamage(float amount)
        {
            if (_isDead || Health <= 0f)
                return;

            Health -= Mathf.Max(0f, amount);
            UpdateHealthBar();
            PlayHitFlash();
            if (Health > 0f)
            {
                if (!IsBoss)
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
            _hitFlashRenderer.sortingOrder = visualRenderer.sortingOrder + 1;
            _hitFlashRenderer.flipX = visualRenderer.flipX;
            _hitFlashRenderer.flipY = visualRenderer.flipY;
            _hitFlashRenderer.color = new Color(hitFlashColor.r, hitFlashColor.g, hitFlashColor.b, 0f);
            _hitFlashRenderer.enabled = false;
        }

        private void PlayHitFlash()
        {
            EnsureHitFlashRenderer();
            if (_hitFlashRenderer == null || !isActiveAndEnabled)
                return;

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
