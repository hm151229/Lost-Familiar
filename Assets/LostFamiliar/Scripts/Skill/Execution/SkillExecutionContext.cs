using System;
using System.Collections;
using UnityEngine;

namespace LostFamiliar.Battle
{
    public delegate void DealSkillDamageDelegate(
        SkillData skill, EnemyActor enemy, float multiplier,
        GameObject impactProjectile, bool createHitEffect, bool applyKnockback);

    public delegate void DamageSkillAreaDelegate(
        SkillData skill, Vector3 center, float radius, float multiplier,
        GameObject impactProjectile, bool createHitEffect, bool applyKnockback);

    public delegate GameObject CreatePrimitiveEffectDelegate(
        Vector3 position, Vector3 scale, Color color, float lifetime, Quaternion? rotation);

    public delegate GameObject CreatePrefabEffectDelegate(
        GameObject prefab, Vector3 position, Quaternion rotation, float lifetime, int sortingOrder);

    public delegate GameObject CreateStationaryEffectDelegate(
        GameObject prefab, Vector3 position, Vector3 rotationOffset, out float lifetime);

    public delegate IEnumerator LaunchDirectProjectileDelegate(
        SkillData skill, EnemyActor target, Vector3 origin, float multiplier, int sortingOrder);

    public sealed class SkillExecutionContext
    {
        public const int DefaultEffectSortingOrder = 50;
        public const int PlayerAreaEffectSortingOrder = 150;
        public const int StarNovaProjectileSortingOrder = 200;

        public PlayerAutoCombat Player { get; }
        public Transform PlayerTransform => Player != null ? Player.transform : null;
        public Transform FirePoint { get; }
        public int CombatGroup => Player?.CombatGroup ?? 0;

        public Func<float, EnemyActor> FindNearestEnemy { get; }
        public Func<EnemyActor> GetRandomEnemy { get; }
        public Func<float, Vector3> GetDensestEnemyPosition { get; }
        public Func<Vector3> GetFacingDirection { get; }
        public Func<Vector3, Vector3, EnemyActor, float, bool> SegmentIntersectsEnemy { get; }
        public Func<Vector3, Vector3, Vector3, Quaternion> GetProjectileRotation { get; }
        public Func<SkillData, EnemyActor, float, float, float, IEnumerator> LaunchProjectile { get; }
        public LaunchDirectProjectileDelegate LaunchDirectProjectile { get; }
        public DealSkillDamageDelegate DealDamage { get; }
        public DamageSkillAreaDelegate DamageArea { get; }
        public CreatePrimitiveEffectDelegate CreatePrimitiveEffect { get; }
        public CreatePrefabEffectDelegate CreatePrefabEffect { get; }
        public Func<SkillData, Vector3, GameObject> CreateExplosionEffect { get; }
        public CreateStationaryEffectDelegate CreateStationaryEffect { get; }
        public Action<string, float> PlaySfx { get; }
        public Action<string, float, float> PlayLoop { get; }
        public Action<IEnumerator> StartRoutine { get; }
        public Action<GameObject> DestroyProjectile { get; }
        public Action<GameObject> RegisterPlayerAttachedEffect { get; }
        public Action<GameObject> UnregisterPlayerAttachedEffect { get; }

        public SkillExecutionContext(
            PlayerAutoCombat player,
            Transform firePoint,
            Func<float, EnemyActor> findNearestEnemy,
            Func<EnemyActor> getRandomEnemy,
            Func<float, Vector3> getDensestEnemyPosition,
            Func<Vector3> getFacingDirection,
            Func<Vector3, Vector3, EnemyActor, float, bool> segmentIntersectsEnemy,
            Func<Vector3, Vector3, Vector3, Quaternion> getProjectileRotation,
            Func<SkillData, EnemyActor, float, float, float, IEnumerator> launchProjectile,
            LaunchDirectProjectileDelegate launchDirectProjectile,
            DealSkillDamageDelegate dealDamage,
            DamageSkillAreaDelegate damageArea,
            CreatePrimitiveEffectDelegate createPrimitiveEffect,
            CreatePrefabEffectDelegate createPrefabEffect,
            Func<SkillData, Vector3, GameObject> createExplosionEffect,
            CreateStationaryEffectDelegate createStationaryEffect,
            Action<string, float> playSfx,
            Action<string, float, float> playLoop,
            Action<IEnumerator> startRoutine,
            Action<GameObject> destroyProjectile,
            Action<GameObject> registerPlayerAttachedEffect,
            Action<GameObject> unregisterPlayerAttachedEffect)
        {
            Player = player;
            FirePoint = firePoint;
            FindNearestEnemy = findNearestEnemy;
            GetRandomEnemy = getRandomEnemy;
            GetDensestEnemyPosition = getDensestEnemyPosition;
            GetFacingDirection = getFacingDirection;
            SegmentIntersectsEnemy = segmentIntersectsEnemy;
            GetProjectileRotation = getProjectileRotation;
            LaunchProjectile = launchProjectile;
            LaunchDirectProjectile = launchDirectProjectile;
            DealDamage = dealDamage;
            DamageArea = damageArea;
            CreatePrimitiveEffect = createPrimitiveEffect;
            CreatePrefabEffect = createPrefabEffect;
            CreateExplosionEffect = createExplosionEffect;
            CreateStationaryEffect = createStationaryEffect;
            PlaySfx = playSfx;
            PlayLoop = playLoop;
            StartRoutine = startRoutine;
            DestroyProjectile = destroyProjectile;
            RegisterPlayerAttachedEffect = registerPlayerAttachedEffect;
            UnregisterPlayerAttachedEffect = unregisterPlayerAttachedEffect;
        }
    }
}
