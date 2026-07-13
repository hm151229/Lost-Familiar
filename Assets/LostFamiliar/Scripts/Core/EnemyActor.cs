using System;
using System.Collections.Generic;
using UnityEngine;

namespace LostFamiliar.Battle
{
    public sealed class EnemyActor : MonoBehaviour
    {
        public static readonly List<EnemyActor> Active = new();

        public EnemyData Data { get; private set; }
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public bool IsBoss { get; private set; }

        public event Action<EnemyActor> Died;

        private PlayerAutoCombat _target;
        private float _attackTimer;

        public void Initialize(EnemyData data, PlayerAutoCombat target, int stage, bool boss)
        {
            Data = data;
            _target = target;
            IsBoss = boss;

            float stageScale = Mathf.Pow(1.22f, Mathf.Max(0, stage - 1));
            MaxHealth = data.baseHealth * stageScale * (boss ? 8f : 1f);
            Health = MaxHealth;

            transform.localScale = Vector3.one * (boss ? 1.8f : 1f);
            gameObject.name = boss ? $"Boss_{data.displayName}" : data.displayName;
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        private void Update()
        {
            if (Data == null || _target == null || !_target.IsAlive)
                return;

            float distance = Vector3.Distance(transform.position, _target.transform.position);
            if (distance > Data.attackRange)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    _target.transform.position,
                    Data.moveSpeed * Time.deltaTime);
                return;
            }

            _attackTimer += Time.deltaTime;
            if (_attackTimer < Data.attackInterval)
                return;

            _attackTimer = 0f;
            _target.TakeDamage(Data.baseAttack * (IsBoss ? 2f : 1f));
        }

        public void TakeDamage(float amount)
        {
            if (Health <= 0f)
                return;

            Health -= Mathf.Max(0f, amount);
            if (Health > 0f)
                return;

            Health = 0f;
            Died?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
