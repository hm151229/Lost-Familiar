using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LostFamiliar.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace LostFamiliar.Battle
{
    [DisallowMultipleComponent]
    public sealed class PlayerSkillExecutor : MonoBehaviour
    {
        private const int SkillEffectSortingOrder = 50;

        private PlayerAutoCombat _player;
        private PlayerSkillController _skillController;
        private Transform _firePoint;
        private SpriteRenderer _visualRenderer;
        private Transform _skillEffectRoot;
        private SkillExecutionContext _context;

        private readonly Dictionary<SkillBehavior, ISkillBehavior>
            _behaviors = new();

        private readonly List<GameObject>
            _playerAttachedSkillEffects = new();

        public void Initialize(
            PlayerAutoCombat player,
            PlayerSkillController skillController,
            Transform firePoint,
            SpriteRenderer visualRenderer)
        {
            _player = player;
            _skillController = skillController;
            _firePoint = firePoint;
            _visualRenderer = visualRenderer;

            EnsureSkillEffectRoot();
            BuildExecutionContext();
            RegisterBehaviors();
        }

        private void BuildExecutionContext()
        {
            _context = new SkillExecutionContext(
                _player,
                _firePoint,
                FindNearestEnemy,
                GetRandomEnemy,
                GetDensestEnemyPosition,
                GetFacingDirection,
                SegmentIntersectsEnemy,
                GetProjectileRotation,
                LaunchProjectile,
                LaunchDirectProjectile,
                DealSkillDamage,
                DamageArea,
                CreateEffect,
                CreatePrefabEffect,
                CreateExplosionEffect,
                CreateStationaryProjectileEffect,
                PlayCombatSfx,
                PlayCombatLoop,
                StartSkillRoutine,
                StopAndDestroyProjectile,
                RegisterPlayerAttachedEffect,
                UnregisterPlayerAttachedEffect);
        }

        private void RegisterBehaviors()
        {
            // Adding a skill only requires registering its behavior here.
            _behaviors.Clear();
            Register(SkillBehavior.MagicMissile, new MagicMissileBehavior());
            Register(SkillBehavior.FireBall, new FireBallBehavior());
            Register(SkillBehavior.IceSpear, new IceSpearBehavior());
            Register(SkillBehavior.LightningBolt, new LightningBoltBehavior());
            Register(SkillBehavior.ArcaneOrb, new ArcaneOrbBehavior());
            Register(SkillBehavior.WindCutter, new WindCutterBehavior());
            Register(SkillBehavior.Meteor, new MeteorBehavior());
            Register(SkillBehavior.Blizzard, new BlizzardBehavior());
            Register(SkillBehavior.BlackHole, new BlackHoleBehavior());
            Register(SkillBehavior.StarNova, new StarNovaBehavior());
        }

        private void Register(SkillBehavior type, ISkillBehavior behavior)
        {
            _behaviors[type] = behavior;
        }

        public void Execute(SkillData skill)
        {
            if (skill == null ||
                _player == null)
                return;

            StartCoroutine(
                ExecuteRoutine(skill));
        }

        private IEnumerator ExecuteRoutine(SkillData skill)
        {
            if (skill == null || _context == null)
                yield break;

            if (!_behaviors.TryGetValue(skill.behavior, out ISkillBehavior behavior))
            {
                Debug.LogWarning($"등록되지 않은 SkillBehavior: {skill.behavior}", this);
                yield break;
            }

            yield return behavior.Execute(skill, _context);
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
                if (enemy == null || enemy.CombatGroup != _player.CombatGroup ||
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
            float damage = _player.AttackDamage * multiplier * _player.SkillDamageMultiplier * levelMultiplier;
            enemy.TakeDamage(ApplyBossDamage(damage, enemy), applyKnockback);
        }

        private int GetEquippedSkillLevel(SkillData skill)
        {
            return _skillController?.GetLevel(skill) ?? 1;
        }

        private float ApplyBossDamage(float damage, EnemyActor enemy)
        {
            return enemy != null && enemy.IsBoss ? damage * _player.BossDamageMultiplier : damage;
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
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            float lifetime,
            int sortingOrder)
        {
            if (prefab == null) return null;
            GameObject effect = Instantiate(prefab, position, rotation);
            RegisterSkillEffect(effect);
            ApplySkillEffectSorting(effect, sortingOrder);
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

        public void Clear()
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

        private void StartSkillRoutine(IEnumerator routine)
        {
            if (routine != null)
                StartCoroutine(routine);
        }

        private void RegisterPlayerAttachedEffect(GameObject effect)
        {
            if (effect != null && !_playerAttachedSkillEffects.Contains(effect))
                _playerAttachedSkillEffects.Add(effect);
        }

        private void UnregisterPlayerAttachedEffect(GameObject effect)
        {
            _playerAttachedSkillEffects.Remove(effect);
        }

        private void EnsureSkillEffectRoot()
        {
            if (_skillEffectRoot != null)
            {
                _skillEffectRoot.gameObject.layer = gameObject.layer;
                return;
            }

            GameObject root = new GameObject("SkillEffectRoot");
            root.layer = gameObject.layer;
            _skillEffectRoot = root.transform;
        }

        private GameObject RegisterSkillEffect(GameObject effect)
        {
            if (effect == null)
                return null;
            EnsureSkillEffectRoot();
            SetLayerRecursively(effect, gameObject.layer);
            effect.transform.SetParent(_skillEffectRoot, true);
            return effect;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
                return;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
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
                if (enemy == null || enemy.CombatGroup != _player.CombatGroup)
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
                if (enemy != null && enemy.CombatGroup == _player.CombatGroup && enemy.Health > 0f)
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
                if (candidate == null || candidate.CombatGroup != _player.CombatGroup)
                    continue;
                int count = 0;
                foreach (EnemyActor other in enemies)
                {
                    if (other != null && other.CombatGroup == _player.CombatGroup &&
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

        private void PlayCombatSfx(
            string id,
            float volume = 1f)
        {
            if (_player == null)
                return;

            GameAudioManager audio =
                GameAudioManager.Instance;

            if (audio.IsBattleAudioAllowed(
                    _player.CombatGroup))
            {
                audio.PlaySfx(
                    id,
                    volume);
            }
        }

        private void PlayCombatLoop(
            string id,
            float duration,
            float volume = 1f)
        {
            if (_player == null)
                return;

            GameAudioManager audio =
                GameAudioManager.Instance;

            if (audio.IsBattleAudioAllowed(
                    _player.CombatGroup))
            {
                audio.PlayLoopForDuration(
                    id,
                    duration,
                    volume);
            }
        }

        private void OnDestroy()
        {
            Clear();

            if (_skillEffectRoot != null)
                Destroy(_skillEffectRoot.gameObject);
        }
    }
}
