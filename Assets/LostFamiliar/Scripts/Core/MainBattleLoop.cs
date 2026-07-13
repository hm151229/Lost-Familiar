using System.Collections;
using UnityEngine;

namespace LostFamiliar.Battle
{
    public enum BattlePhase { Normal, EnteringBoss, Boss, Returning, StageClear }

    public sealed class MainBattleLoop : MonoBehaviour
    {
        public StageData[] stages;
        public PlayerAutoCombat Player { get; private set; }
        public BattlePhase Phase { get; private set; }
        public int StageNumber { get; private set; } = 1;
        public int StageExperience { get; private set; }
        public int Gold { get; private set; }
        public StageData CurrentStage => stages != null && stages.Length > 0 ? stages[(StageNumber - 1) % stages.Length] : null;
        private float _spawnTimer;
        private bool _transitioning;

        public void Initialize(StageData[] stageList, PlayerAutoCombat player)
        {
            stages = stageList;
            Player = player;
            Player.Defeated += OnPlayerDefeated;
            Phase = BattlePhase.Normal;
            ApplyBackground();
        }

        private void Update()
        {
            if (CurrentStage == null || Player == null || _transitioning || Phase != BattlePhase.Normal) return;
            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= CurrentStage.spawnInterval && EnemyActor.Active.Count < CurrentStage.maxAliveEnemies)
            {
                _spawnTimer = 0f;
                Spawn(CurrentStage.PickEnemy(), false);
            }
        }

        private void Spawn(EnemyData data, bool boss)
        {
            if (data == null) return;
            GameObject enemyObject = data.prefab != null ? Instantiate(data.prefab) : GameObject.CreatePrimitive(boss ? PrimitiveType.Capsule : PrimitiveType.Sphere);
            float side = Random.value < .5f ? -1f : 1f;
            enemyObject.transform.position = new Vector3(side * Random.Range(6f, 9f), Random.Range(-3.5f, 3.5f), 0f);
            var enemy = enemyObject.GetComponent<EnemyActor>() ?? enemyObject.AddComponent<EnemyActor>();
            enemy.Initialize(data, Player, StageNumber, boss);
            enemy.Died += OnEnemyDied;
        }

        private void OnEnemyDied(EnemyActor enemy)
        {
            enemy.Died -= OnEnemyDied;
            Gold += Mathf.RoundToInt(enemy.Data.goldReward * Mathf.Pow(1.15f, StageNumber - 1) * (enemy.IsBoss ? 10f : 1f));
            if (enemy.IsBoss)
            {
                StartCoroutine(CompleteStage());
                return;
            }
            if (Phase != BattlePhase.Normal) return;
            StageExperience += enemy.Data.stageExperience;
            if (StageExperience >= CurrentStage.experienceToBoss) StartCoroutine(EnterBoss());
        }

        private IEnumerator EnterBoss()
        {
            _transitioning = true;
            Phase = BattlePhase.EnteringBoss;
            ClearEnemies();
            yield return new WaitForSeconds(1f);
            Player.Revive();
            Phase = BattlePhase.Boss;
            Spawn(CurrentStage.boss, true);
            _transitioning = false;
        }

        private IEnumerator CompleteStage()
        {
            _transitioning = true;
            Phase = BattlePhase.StageClear;
            yield return new WaitForSeconds(1.5f);
            StageNumber++;
            StageExperience = 0;
            Player.Revive();
            Phase = BattlePhase.Normal;
            ApplyBackground();
            _transitioning = false;
        }

        private void OnPlayerDefeated()
        {
            if (_transitioning) return;
            if (Phase == BattlePhase.Boss) StartCoroutine(ReturnToNormal());
            else StartCoroutine(RespawnInNormal());
        }

        private IEnumerator ReturnToNormal()
        {
            _transitioning = true;
            Phase = BattlePhase.Returning;
            ClearEnemies();
            yield return new WaitForSeconds(1.5f);
            StageExperience = 0;
            Player.Revive();
            Phase = BattlePhase.Normal;
            _transitioning = false;
        }

        private IEnumerator RespawnInNormal()
        {
            _transitioning = true;
            ClearEnemies();
            yield return new WaitForSeconds(1f);
            Player.Revive();
            _transitioning = false;
        }

        private void ClearEnemies()
        {
            foreach (EnemyActor enemy in EnemyActor.Active.ToArray()) if (enemy != null) Destroy(enemy.gameObject);
        }

        private void ApplyBackground()
        {
            if (Camera.main != null && CurrentStage != null) Camera.main.backgroundColor = CurrentStage.backgroundColor;
        }
    }
}
