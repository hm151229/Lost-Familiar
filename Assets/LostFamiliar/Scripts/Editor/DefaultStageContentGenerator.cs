#if UNITY_EDITOR
using LostFamiliar.Battle;
using UnityEditor;
using UnityEngine;

namespace LostFamiliar.Editor
{
    public static class DefaultStageContentGenerator
    {
        private const int ContentVersion = 3;
        private const string Root = "Assets/LostFamiliar/Resources/StageData";
        private const string DatabasePath = Root + "/DefaultStageDatabase.asset";
        private const string BalancePath = Root + "/DefaultStageBalance.asset";
        private const string SkillPath = Root + "/Skills/01_Common_MagicMissile.asset";
        private const string GuideFingerPath = "Assets/LostFamiliar/Art/Textures/UI/UI_Finger.png";

        [InitializeOnLoadMethod]
        private static void QueueGeneration()
        {
            EditorApplication.delayCall += GenerateIfMissing;
        }

        [MenuItem("Tools/Lost Familiar/Generate Default Stage Data")]
        public static void GenerateIfMissing()
        {
            Generate(false);
        }

        [MenuItem("Tools/Lost Familiar/Rebuild Stage Data (Keep Enemy Visuals)")]
        public static void RebuildKeepingEnemyVisuals()
        {
            Generate(true);
        }

        private static void Generate(bool force)
        {
            EnsureFolders();
            if (force)
                DeleteGeneratedContainerAssets();

            StageDatabase existingDatabase = AssetDatabase.LoadAssetAtPath<StageDatabase>(DatabasePath);
            if (!force && existingDatabase != null && existingDatabase.contentVersion >= ContentVersion)
                return;

            StageBalanceConfig balance = CreateAsset<StageBalanceConfig>(
                BalancePath,
                "DefaultStageBalance");

            RegionDefinition[] definitions =
            {
                new RegionDefinition(
                    "01_MagicForest", "magic_forest", "마법 숲", 1, 10,
                    new Color(.08f, .18f, .12f), 25f, 3f,
                    new[] { "마력 버섯", "숲 슬라임", "가시 덩굴" },
                    new[] { "버섯 수호자", "고대 나무 정령", "거대 마력 버섯" }),
                new RegionDefinition(
                    "02_MushroomValley", "mushroom_valley", "버섯 계곡", 11, 20,
                    new Color(.18f, .12f, .18f), 28f, 3.2f,
                    new[] { "독 포자 버섯", "포자 벌", "점액 달팽이" },
                    new[] { "독버섯 기사", "포자 여왕", "포자 군주" }),
                new RegionDefinition(
                    "03_CrystalCave", "crystal_cave", "수정 동굴", 21, 30,
                    new Color(.08f, .14f, .24f), 30f, 3.5f,
                    new[] { "수정 박쥐", "광석 슬라임", "꼬마 골렘" },
                    new[] { "수정 수호자", "광맥 거인", "수정 골렘" }),
                new RegionDefinition(
                    "04_SnowyMountain", "snowy_mountain", "눈 덮인 산맥", 31, 40,
                    new Color(.28f, .38f, .48f), 32f, 3.8f,
                    new[] { "얼음 정령", "설원 늑대", "서리 슬라임" },
                    new[] { "얼음 트롤", "눈보라 정령", "설산의 설인" }),
                new RegionDefinition(
                    "05_LavaCanyon", "lava_canyon", "용암 협곡", 41, 50,
                    new Color(.28f, .08f, .04f), 35f, 4.2f,
                    new[] { "불꽃 정령", "용암 슬라임", "잿불 도마뱀" },
                    new[] { "마그마 와이번", "화염 거인", "용암 골렘" })
            };

            RegionData[] regions = new RegionData[definitions.Length];
            for (int i = 0; i < definitions.Length; i++)
                regions[i] = CreateRegion(definitions[i]);

            SkillData skill = CreateAsset<SkillData>(SkillPath, "01_Common_MagicMissile");
            skill.id = "magic_missile";
            skill.displayName = "매직 미사일";
            skill.behavior = SkillBehavior.MagicMissile;
            skill.displayName = "마력 폭발";
            skill.cooldown = 3f;
            skill.damageMultiplier = 1.2f;
            skill.projectileCount = 3;
            skill.targetType = SkillTargetType.NearestEnemy;
            skill.radius = 6f;
            EditorUtility.SetDirty(skill);

            StageDatabase database = existingDatabase != null
                ? existingDatabase
                : CreateAsset<StageDatabase>(DatabasePath, "DefaultStageDatabase");
            database.contentVersion = ContentVersion;
            database.balance = balance;
            database.guideFingerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GuideFingerPath);
            database.regions = regions;
            database.specialStages = System.Array.Empty<SpecialStageData>();
            EditorUtility.SetDirty(database);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Lost Familiar 스테이지 콘텐츠 업데이트 완료: 1~50, 일반 몬스터 15종, 보스 15종");
        }

        private static void DeleteGeneratedContainerAssets()
        {
            AssetDatabase.DeleteAsset(DatabasePath);
            AssetDatabase.DeleteAsset(BalancePath);

            string[] regionAssetPaths =
            {
                Root + "/Regions/01_MagicForest.asset",
                Root + "/Regions/02_MushroomValley.asset",
                Root + "/Regions/03_CrystalCave.asset",
                Root + "/Regions/04_SnowyMountain.asset",
                Root + "/Regions/05_LavaCanyon.asset"
            };

            foreach (string path in regionAssetPaths)
                AssetDatabase.DeleteAsset(path);
        }

        private static RegionData CreateRegion(RegionDefinition definition)
        {
            int[] unlockStages = { 1, 4, 7 };
            int[] weights = { 6, 4, 2 };
            float[] healthMultipliers = { 1f, 1.12f, 1.28f };
            float[] attackMultipliers = { 1f, 1.1f, 1.22f };
            EnemySpawnEntry[] normalEntries = new EnemySpawnEntry[3];

            for (int i = 0; i < normalEntries.Length; i++)
            {
                string suffix = i == 0 ? "_Normal" : $"_Normal0{i + 1}";
                EnemyData enemy = CreateEnemy(
                    definition.fileName + suffix,
                    definition.normalNames[i],
                    definition.baseHealth * healthMultipliers[i],
                    definition.baseAttack * attackMultipliers[i],
                    1.35f + i * .15f,
                    10 + i * 2,
                    3 + i,
                    5d + i * 2d);
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
                string suffix = i == 2 ? "_Boss" : $"_Boss0{i + 1}";
                bosses[i] = CreateEnemy(
                    definition.fileName + suffix,
                    definition.bossNames[i],
                    definition.baseHealth * bossHealthMultipliers[i],
                    definition.baseAttack * bossAttackMultipliers[i],
                    1f,
                    0,
                    15 + i * 5,
                    15d + i * 5d);
            }

            RegionData region = CreateAsset<RegionData>(
                Root + "/Regions/" + definition.fileName + ".asset",
                definition.fileName);
            region.id = definition.id;
            region.displayName = definition.displayName;
            region.startStage = definition.startStage;
            region.endStage = definition.endStage;
            region.backgroundColor = definition.backgroundColor;
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
            WriteEnemyReferences(region, normalEntries, region.stageBosses, region.boss);
            EditorUtility.SetDirty(region);
            return region;
        }

        private static void WriteEnemyReferences(
            RegionData region,
            EnemySpawnEntry[] normalEntries,
            EnemyData[] stageBosses,
            EnemyData finalBoss)
        {
            SerializedObject serializedRegion = new SerializedObject(region);

            SerializedProperty normalEnemies = serializedRegion.FindProperty("normalEnemies");
            normalEnemies.arraySize = normalEntries.Length;
            for (int i = 0; i < normalEntries.Length; i++)
            {
                SerializedProperty entry = normalEnemies.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("enemy").objectReferenceValue = normalEntries[i].enemy;
                entry.FindPropertyRelative("weight").intValue = normalEntries[i].weight;
                entry.FindPropertyRelative("unlockStageInRegion").intValue = normalEntries[i].unlockStageInRegion;
            }

            SerializedProperty bosses = serializedRegion.FindProperty("stageBosses");
            bosses.arraySize = stageBosses.Length;
            for (int i = 0; i < stageBosses.Length; i++)
                bosses.GetArrayElementAtIndex(i).objectReferenceValue = stageBosses[i];

            serializedRegion.FindProperty("boss").objectReferenceValue = finalBoss;
            serializedRegion.ApplyModifiedPropertiesWithoutUndo();
        }

        private static EnemyData CreateEnemy(
            string fileName,
            string displayName,
            float health,
            float attack,
            float moveSpeed,
            int stageExperience,
            int playerExperience,
            double gold)
        {
            EnemyData enemy = CreateAsset<EnemyData>(Root + "/Enemies/" + fileName + ".asset", fileName);
            enemy.displayName = displayName;
            enemy.baseHealth = health;
            enemy.baseAttack = attack;
            enemy.moveSpeed = moveSpeed;
            enemy.stageExperience = stageExperience;
            enemy.playerExperience = playerExperience;
            enemy.goldReward = gold;
            EditorUtility.SetDirty(enemy);
            return enemy;
        }

        private static T CreateAsset<T>(string path, string assetName) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            // A ScriptableObject whose class used to live in a differently named
            // source file can remain as a broken asset at this path. Remove only
            // that unusable asset before creating the correctly typed replacement.
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);

            T asset = ScriptableObject.CreateInstance<T>();
            asset.name = assetName;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/LostFamiliar", "Resources");
            EnsureFolder("Assets/LostFamiliar/Resources", "StageData");
            EnsureFolder(Root, "Enemies");
            EnsureFolder(Root, "Regions");
            EnsureFolder(Root, "Skills");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private sealed class RegionDefinition
        {
            public readonly string fileName;
            public readonly string id;
            public readonly string displayName;
            public readonly int startStage;
            public readonly int endStage;
            public readonly Color backgroundColor;
            public readonly float baseHealth;
            public readonly float baseAttack;
            public readonly string[] normalNames;
            public readonly string[] bossNames;

            public RegionDefinition(
                string fileName,
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
                this.fileName = fileName;
                this.id = id;
                this.displayName = displayName;
                this.startStage = startStage;
                this.endStage = endStage;
                this.backgroundColor = backgroundColor;
                this.baseHealth = baseHealth;
                this.baseAttack = baseAttack;
                this.normalNames = normalNames;
                this.bossNames = bossNames;
            }
        }
    }
}
#endif
