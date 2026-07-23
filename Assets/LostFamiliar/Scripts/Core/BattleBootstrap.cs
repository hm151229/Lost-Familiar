using UnityEngine;

namespace LostFamiliar.Battle
{
    public static class BattleBootstrap
    {
        private const int StageContentVersion = 3;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Application.targetFrameRate = 60;

            LostFamiliar.Core.IdleGameController old = Object.FindFirstObjectByType<LostFamiliar.Core.IdleGameController>();
            if (old != null)
                Object.Destroy(old.gameObject);

            EnsureCamera();
            PlayerAutoCombat player = Object.FindFirstObjectByType<PlayerAutoCombat>();
            if (player == null)
                player = CreatePlaceholderPlayer();
            ConfigureCameraFollow(player);

            MainBattleLoop battle = Object.FindFirstObjectByType<MainBattleLoop>();
            if (battle == null)
            {
                GameObject root = new GameObject("Main_AutoBattle");
                battle = root.AddComponent<MainBattleLoop>();
            }

            StageDatabase database = battle.Database != null
                ? battle.Database
                : Resources.Load<StageDatabase>("StageData/DefaultStageDatabase");
            if (!IsStageDatabaseUsable(database))
            {
                Debug.LogWarning(
                    "StageDatabase가 없거나 몬스터 목록이 비어 있어 임시 전투 데이터를 사용합니다. " +
                    "Tools/Lost Familiar/Rebuild Stage Data (Keep Enemy Visuals)를 실행하세요.");
                database = BuildDefaultDatabase();
            }
            // 스킬은 기본 지급하지 않고 저장된 장착 정보만 MainBattleLoop에서 복원한다.
            player.SetEquippedSkills(System.Array.Empty<SkillData>());
            battle.Initialize(database, player);
            if (battle.GetComponent<GameCheatController>() == null)
                battle.gameObject.AddComponent<GameCheatController>();

            if (Object.FindFirstObjectByType<Canvas>() == null && battle.GetComponent<BattlePrototypeHUD>() == null)
                battle.gameObject.AddComponent<BattlePrototypeHUD>();
        }

        private static PlayerAutoCombat CreatePlaceholderPlayer()
        {
            GameObject playerObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerObject.name = "Player_BlackCat_Placeholder";
            playerObject.transform.position = Vector3.zero;
            playerObject.GetComponent<Renderer>().material.color = new Color(.05f, .05f, .08f);
            Object.Destroy(playerObject.GetComponent<Collider>());
            return playerObject.AddComponent<PlayerAutoCombat>();
        }

        private static void ConfigureCameraFollow(PlayerAutoCombat player)
        {
            if (Camera.main == null || player == null)
                return;

            CameraFollow2D follow = Camera.main.GetComponent<CameraFollow2D>();
            if (follow == null)
                follow = Camera.main.gameObject.AddComponent<CameraFollow2D>();

            follow.Bind(player.transform);

            GameObject background = GameObject.Find("World/Background");
            if (background != null)
            {
                BackgroundTiler2D tiler = background.GetComponent<BackgroundTiler2D>();
                if (tiler == null)
                    tiler = background.AddComponent<BackgroundTiler2D>();
                tiler.Bind(Camera.main.transform);
            }
        }

        private static void EquipDefaultSkillIfNeeded(PlayerAutoCombat player)
        {
            if (player.EquippedSkills != null && player.EquippedSkills.Length > 0)
                return;

            SkillData burst = Resources.Load<SkillData>("StageData/Skills/01_Common_MagicMissile");
            if (burst == null)
                burst = CreateRuntimeAsset<SkillData>("Default_MagicMissile");
            burst.displayName = "마력 폭발";
            burst.id = "magic_missile";
            burst.displayName = "매직 미사일";
            burst.behavior = SkillBehavior.MagicMissile;
            burst.cooldown = 3f;
            burst.damageMultiplier = 1.2f;
            burst.projectileCount = 3;
            burst.targetType = SkillTargetType.NearestEnemy;
            burst.radius = 6f;
            player.SetEquippedSkills(new[] { burst });
        }

        private static StageDatabase BuildDefaultDatabase()
        {
            StageBalanceConfig balance = CreateRuntimeAsset<StageBalanceConfig>("Default_StageBalance");
            StageDatabase database = CreateRuntimeAsset<StageDatabase>("Default_StageDatabase");
            database.contentVersion = StageContentVersion;
            database.balance = balance;
            database.regions = new[]
            {
                CreateRegion("magic_forest", "마법 숲", 1, 10, new Color(.08f, .18f, .12f), 25f, 3f,
                    new[] { "마력 버섯", "숲 슬라임", "가시 덩굴" },
                    new[] { "버섯 수호자", "고대 나무 정령", "거대 마력 버섯" }),
                CreateRegion("mushroom_valley", "버섯 계곡", 11, 20, new Color(.18f, .12f, .18f), 28f, 3.2f,
                    new[] { "독 포자 버섯", "포자 벌", "점액 달팽이" },
                    new[] { "독버섯 기사", "포자 여왕", "포자 군주" }),
                CreateRegion("crystal_cave", "수정 동굴", 21, 30, new Color(.08f, .14f, .24f), 30f, 3.5f,
                    new[] { "수정 박쥐", "광석 슬라임", "꼬마 골렘" },
                    new[] { "수정 수호자", "광맥 거인", "수정 골렘" }),
                CreateRegion("snowy_mountain", "눈 덮인 산맥", 31, 40, new Color(.28f, .38f, .48f), 32f, 3.8f,
                    new[] { "얼음 정령", "설원 늑대", "서리 슬라임" },
                    new[] { "얼음 트롤", "눈보라 정령", "설산의 설인" }),
                CreateRegion("lava_canyon", "용암 협곡", 41, 50, new Color(.28f, .08f, .04f), 35f, 4.2f,
                    new[] { "불꽃 정령", "용암 슬라임", "잿불 도마뱀" },
                    new[] { "마그마 와이번", "화염 거인", "용암 골렘" })
            };
            database.specialStages = System.Array.Empty<SpecialStageData>();
            return database;
        }

        private static bool IsStageDatabaseUsable(StageDatabase database)
        {
            if (database == null || database.contentVersion < StageContentVersion ||
                database.balance == null || database.regions == null || database.regions.Length == 0)
                return false;

            foreach (RegionData region in database.regions)
            {
                if (region == null || region.normalEnemies == null || region.normalEnemies.Length == 0)
                    return false;
            }

            return true;
        }

        private static RegionData CreateRegion(
            string id,
            string displayName,
            int startStage,
            int endStage,
            Color backgroundColor,
            float baseHealth,
            float baseAttack,
            string[] normalNames,
            string[] bossNames)
        {
            int[] unlockStages = { 1, 4, 7 };
            int[] weights = { 6, 4, 2 };
            float[] healthMultipliers = { 1f, 1.12f, 1.28f };
            float[] attackMultipliers = { 1f, 1.1f, 1.22f };
            EnemySpawnEntry[] normalEntries = new EnemySpawnEntry[3];
            for (int i = 0; i < normalEntries.Length; i++)
            {
                EnemyData enemy = CreateEnemy(
                    normalNames[i],
                    baseHealth * healthMultipliers[i],
                    baseAttack * attackMultipliers[i],
                    10 + i * 2,
                    3 + i,
                    5d + i * 2d);
                enemy.moveSpeed = 1.35f + i * .15f;
                normalEntries[i] = new EnemySpawnEntry
                {
                    enemy = enemy,
                    weight = weights[i],
                    unlockStageInRegion = unlockStages[i]
                };
            }

            float[] bossHealthMultipliers = { 1.15f, 1.4f, 1.75f };
            float[] bossAttackMultipliers = { 1.1f, 1.35f, 1.65f };
            EnemyData[] bosses = new EnemyData[3];
            for (int i = 0; i < bosses.Length; i++)
            {
                bosses[i] = CreateEnemy(
                    bossNames[i],
                    baseHealth * bossHealthMultipliers[i],
                    baseAttack * bossAttackMultipliers[i],
                    0,
                    15 + i * 5,
                    15d + i * 5d);
                bosses[i].moveSpeed = 1f;
            }

            RegionData region = CreateRuntimeAsset<RegionData>($"Region_{id}");
            region.id = id;
            region.displayName = displayName;
            region.startStage = startStage;
            region.endStage = endStage;
            region.backgroundColor = backgroundColor;
            region.spawnInterval = .65f;
            region.spawnBatchSize = 3;
            region.maxAliveEnemies = 15;
            region.normalEnemies = normalEntries;
            region.stageBosses = new[]
            {
                bosses[0], bosses[0], bosses[0], bosses[1], bosses[1],
                bosses[1], bosses[1], bosses[2], bosses[2], bosses[2]
            };
            region.boss = bosses[2];
            return region;
        }

        private static EnemyData CreateEnemy(
            string displayName,
            float health,
            float attack,
            int stageExperience,
            int playerExperience,
            double gold)
        {
            EnemyData enemy = CreateRuntimeAsset<EnemyData>($"Enemy_{displayName}");
            enemy.displayName = displayName;
            enemy.baseHealth = health;
            enemy.baseAttack = attack;
            enemy.stageExperience = stageExperience;
            enemy.playerExperience = playerExperience;
            enemy.goldReward = gold;
            return enemy;
        }

        private static T CreateRuntimeAsset<T>(string name) where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            asset.hideFlags = HideFlags.DontSave;
            return asset;
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null)
                return;

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
        }
    }

    [DisallowMultipleComponent]
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float smoothTime = 0.16f;

        private Transform _target;
        private Vector3 _offset;
        private Vector3 _velocity;

        public void Bind(Transform target)
        {
            _target = target;
            _offset = target != null
                ? transform.position - target.position
                : new Vector3(0f, 0f, -10f);
        }

        public void SnapToTarget()
        {
            if (_target == null)
                return;

            _velocity = Vector3.zero;
            transform.position = _target.position + _offset;
        }

        private void LateUpdate()
        {
            if (_target == null)
                return;

            Vector3 desiredPosition = _target.position + _offset;
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref _velocity,
                smoothTime);
        }
    }

    [DisallowMultipleComponent]
    public sealed class BackgroundTiler2D : MonoBehaviour
    {
        private readonly Transform[] _tiles = new Transform[9];
        private Transform _viewer;
        private SpriteRenderer _source;
        private Vector2 _tileSize;
        private Vector3 _origin;
        private bool _initialized;

        public void Bind(Transform viewer)
        {
            _viewer = viewer;
            if (_initialized)
                return;

            _source = GetComponent<SpriteRenderer>();
            if (_source == null || _source.sprite == null)
                return;

            _origin = transform.position;
            _tileSize = _source.bounds.size;
            if (_tileSize.x <= Mathf.Epsilon || _tileSize.y <= Mathf.Epsilon)
                return;

            _tiles[0] = transform;
            for (int i = 1; i < _tiles.Length; i++)
                _tiles[i] = CreateTile(i).transform;

            _initialized = true;
            RepositionTiles();
        }

        private GameObject CreateTile(int index)
        {
            GameObject tile = new GameObject($"BackgroundTile_{index}");
            tile.transform.SetParent(transform.parent, true);
            tile.transform.rotation = transform.rotation;
            tile.transform.localScale = transform.localScale;

            SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
            renderer.sprite = _source.sprite;
            renderer.sharedMaterial = _source.sharedMaterial;
            renderer.color = _source.color;
            renderer.flipX = _source.flipX;
            renderer.flipY = _source.flipY;
            renderer.sortingLayerID = _source.sortingLayerID;
            renderer.sortingOrder = _source.sortingOrder;
            renderer.maskInteraction = _source.maskInteraction;
            return tile;
        }

        private void LateUpdate()
        {
            if (_initialized && _viewer != null)
                RepositionTiles();
        }

        private void RepositionTiles()
        {
            int centerX = Mathf.RoundToInt((_viewer.position.x - _origin.x) / _tileSize.x);
            int centerY = Mathf.RoundToInt((_viewer.position.y - _origin.y) / _tileSize.y);
            int index = 0;

            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    Vector3 position = _origin + new Vector3(
                        (centerX + x) * _tileSize.x,
                        (centerY + y) * _tileSize.y,
                        0f);
                    position.z = _origin.z;
                    _tiles[index++].position = position;
                }
            }
        }
    }

    public sealed class BattlePrototypeHUD : MonoBehaviour
    {
        private void OnGUI()
        {
            MainBattleLoop battle = GetComponent<MainBattleLoop>();
            if (battle == null || battle.CurrentStage == null || battle.Player == null)
                return;

            float progress = Mathf.Clamp01((float)battle.StageExperience / battle.CurrentStage.experienceToBoss);
            GUI.Box(new Rect(15, 15, Screen.width - 30, 125), GUIContent.none);
            GUI.Label(new Rect(30, 25, Screen.width - 60, 25),
                $"STAGE {battle.StageNumber} · {battle.CurrentStage.DisplayName} · {battle.Phase}");
            GUI.Label(new Rect(30, 50, Screen.width - 60, 22),
                $"Lv.{battle.PlayerLevel}  HP {battle.Player.Health:0}/{battle.Player.MaxHealth:0}  GOLD {Format(battle.Gold)}  GEM {battle.Gems}");
            GUI.Box(new Rect(30, 78, Screen.width - 60, 22), GUIContent.none);
            GUI.DrawTexture(new Rect(32, 80, (Screen.width - 64) * progress, 18), Texture2D.whiteTexture);
            GUI.Label(new Rect(30, 78, Screen.width - 60, 22),
                battle.Phase == BattlePhase.Boss ? "BOSS BATTLE" : $"BOSS GAUGE {progress * 100:0}%");
            GUI.Label(new Rect(30, 104, Screen.width - 60, 22),
                $"PLAYER EXP {battle.PlayerExperience:0}/{battle.PlayerExperienceToLevel:0}");
        }

        private static string Format(double value)
        {
            if (value >= 1_000_000_000d) return $"{value / 1_000_000_000d:0.##}B";
            if (value >= 1_000_000d) return $"{value / 1_000_000d:0.##}M";
            if (value >= 1_000d) return $"{value / 1_000d:0.##}K";
            return $"{value:0}";
        }
    }
}
