using System;
using UnityEngine;

namespace LostFamiliar.Core
{
    [DefaultExecutionOrder(-100)]
    public sealed class IdleGameController : MonoBehaviour
    {
        public static IdleGameController Instance { get; private set; }
        public GameSaveData Data { get; private set; }
        public double EnemyHealth { get; private set; }
        public double EnemyMaxHealth { get; private set; }
        public double PlayerHealth { get; private set; }
        public bool IsBoss { get; private set; }
        public bool LastHitWasCritical { get; private set; }
        public event Action StateChanged;

        public double Attack => (5d + Data.attackLevel * 2d) * (1d + (Data.playerLevel - 1) * 0.05d);
        public double MaxHealth => 50d * (1d + (Data.playerLevel - 1) * 0.03d);
        public double AttacksPerSecond => 1d;
        public double CriticalChance => Math.Min(0.75d, 0.05d + Data.criticalChanceLevel * 0.002d);
        public double CriticalMultiplier => 1.5d + Data.criticalDamageLevel * 0.02d;

        private float _attackTimer;
        private float _enemyAttackTimer;
        private float _saveTimer;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Data = SaveService.Load();
            PlayerHealth = MaxHealth;
            SpawnEnemy();
        }

        private void Update()
        {
            _attackTimer += Time.deltaTime;
            _enemyAttackTimer += Time.deltaTime;
            _saveTimer += Time.unscaledDeltaTime;

            float interval = (float)(1d / AttacksPerSecond);
            while (_attackTimer >= interval)
            {
                _attackTimer -= interval;
                AttackEnemy();
                if (EnemyHealth <= 0d) break;
            }

            if (_enemyAttackTimer >= 1.25f)
            {
                _enemyAttackTimer = 0f;
                PlayerHealth -= GameBalance.EnemyAttack(Data.stage, IsBoss);
                if (PlayerHealth <= 0d)
                {
                    PlayerHealth = MaxHealth;
                    Data.stageProgress = Mathf.Max(0f, Data.stageProgress - 10f);
                    SpawnEnemy();
                }
                StateChanged?.Invoke();
            }

            if (_saveTimer >= 10f) { _saveTimer = 0f; SaveService.Save(Data); }
        }

        private void AttackEnemy()
        {
            LastHitWasCritical = UnityEngine.Random.value < CriticalChance;
            EnemyHealth -= Attack * (LastHitWasCritical ? CriticalMultiplier : 1d);
            if (EnemyHealth <= 0d) DefeatEnemy();
            StateChanged?.Invoke();
        }

        private void DefeatEnemy()
        {
            Data.gold += GameBalance.GoldReward(Data.stage, IsBoss);
            Data.playerExperience += GameBalance.PlayerExperienceReward(Data.stage, IsBoss);
            while (Data.playerExperience >= GameBalance.ExperienceToLevel(Data.playerLevel))
            {
                Data.playerExperience -= GameBalance.ExperienceToLevel(Data.playerLevel);
                Data.playerLevel++;
                PlayerHealth = MaxHealth;
            }

            if (IsBoss)
            {
                Data.stage++;
                Data.stageProgress = 0f;
                Data.gems += 10;
            }
            else Data.stageProgress = Mathf.Min(100f, Data.stageProgress + GameBalance.StageProgressReward(false));
            SpawnEnemy();
        }

        private void SpawnEnemy()
        {
            IsBoss = Data.stageProgress >= 100f;
            EnemyMaxHealth = GameBalance.EnemyHealth(Data.stage, IsBoss);
            EnemyHealth = EnemyMaxHealth;
            _enemyAttackTimer = 0f;
        }

        public bool TryUpgrade(StatType type)
        {
            double cost = GameBalance.UpgradeCost(type, Data.GetStatLevel(type));
            if (Data.gold < cost) return false;
            Data.gold -= cost;
            Data.IncreaseStatLevel(type);
            PlayerHealth = Math.Min(MaxHealth, PlayerHealth);
            SaveService.Save(Data);
            StateChanged?.Invoke();
            return true;
        }

        private void OnApplicationPause(bool paused) { if (paused) SaveService.Save(Data); }
        private void OnApplicationQuit() => SaveService.Save(Data);
    }
}
