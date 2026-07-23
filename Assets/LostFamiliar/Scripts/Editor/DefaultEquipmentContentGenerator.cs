#if UNITY_EDITOR
using System.Collections.Generic;
using LostFamiliar.Core;
using UnityEditor;
using UnityEngine;

namespace LostFamiliar.Editor
{
    public static class DefaultEquipmentContentGenerator
    {
        private const int ContentVersion = 1;
        private const string Root = "Assets/LostFamiliar/Resources/Equipment";
        private const string ItemsRoot = Root + "/Items";
        private const string DatabasePath = Root + "/DefaultEquipmentDatabase.asset";

        [InitializeOnLoadMethod]
        private static void QueueGeneration()
        {
            EditorApplication.delayCall += GenerateIfMissing;
        }

        [MenuItem("Tools/Lost Familiar/Generate Equipment Data")]
        public static void GenerateIfMissing()
        {
            EquipmentDatabase database = AssetDatabase.LoadAssetAtPath<EquipmentDatabase>(DatabasePath);
            if (database != null && database.contentVersion >= ContentVersion &&
                database.items != null && database.items.Length == 50)
                return;

            Generate();
        }

        [MenuItem("Tools/Lost Familiar/Rebuild Equipment Data (Keep Icons)")]
        public static void RebuildKeepingIcons()
        {
            Generate();
        }

        private static void Generate()
        {
            EnsureFolders();
            List<Definition> definitions = BuildDefinitions();
            EquipmentData[] items = new EquipmentData[definitions.Count];

            for (int i = 0; i < definitions.Count; i++)
                items[i] = CreateOrUpdate(definitions[i]);

            EquipmentDatabase database = AssetDatabase.LoadAssetAtPath<EquipmentDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<EquipmentDatabase>();
                database.name = "DefaultEquipmentDatabase";
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            database.contentVersion = ContentVersion;
            database.items = items;
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Lost Familiar 장비 데이터 생성 완료: 5개 희귀도, 50개 장비 (기존 아이콘 유지)");
        }

        private static EquipmentData CreateOrUpdate(Definition definition)
        {
            string rarityFolder = ItemsRoot + "/" + definition.rarity;
            string path = rarityFolder + "/" + definition.fileName + ".asset";
            EquipmentData item = AssetDatabase.LoadAssetAtPath<EquipmentData>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<EquipmentData>();
                item.name = definition.fileName;
                AssetDatabase.CreateAsset(item, path);
            }

            item.id = definition.id;
            item.displayName = definition.displayName;
            item.type = definition.type;
            item.rarity = definition.rarity;
            item.maxLevel = 100;
            item.effects = new[]
            {
                new EquipmentEffectDefinition
                {
                    type = definition.effectType,
                    baseValue = definition.baseEffectValue
                }
            };
            item.ownedEffects = new[]
            {
                new EquipmentEffectDefinition
                {
                    type = definition.effectType,
                    baseValue = definition.baseEffectValue * .2f
                }
            };
            item.ownedEffectRatio = .2f;
            EditorUtility.SetDirty(item);
            return item;
        }

        private static List<Definition> BuildDefinitions()
        {
            List<Definition> definitions = new List<Definition>(50);
            AddTier(definitions, EquipmentRarity.Common, new[]
            {
                "나뭇가지 지팡이", "초보 마법봉", "낡은 마녀모자", "천 모자", "견습 로브",
                "면 로브", "낡은 가죽신", "천 신발", "나무 목걸이", "작은 반지"
            });
            AddTier(definitions, EquipmentRarity.Rare, new[]
            {
                "수정 마법봉", "참나무 지팡이", "깃털 마녀모자", "숲의 후드", "숲의 로브",
                "마법 자수 로브", "숲 여행자 부츠", "가죽 부츠", "에메랄드 반지", "숲의 목걸이"
            });
            AddTier(definitions, EquipmentRarity.Epic, new[]
            {
                "푸른 수정 지팡이", "달빛 완드", "달빛 마녀모자", "별장식 모자", "달빛 로브",
                "마도사 코트", "바람 부츠", "달빛 부츠", "사파이어 펜던트", "달의 반지"
            });
            AddTier(definitions, EquipmentRarity.Legend, new[]
            {
                "태양 마법봉", "고대 대마법봉", "황금 마녀모자", "대현자의 모자", "황금 로브",
                "대현자 로브", "천공 부츠", "황금 부츠", "황금 펜던트", "용의 반지"
            });
            AddTier(definitions, EquipmentRarity.Mythic, new[]
            {
                "별의 지팡이", "차원의 완드", "마녀 여왕의 모자", "별빛 왕관", "별의 로브",
                "차원 마도복", "공간도약 부츠", "별빛 부츠", "별의 목걸이", "차원의 반지"
            });
            return definitions;
        }

        private static void AddTier(
            List<Definition> target,
            EquipmentRarity rarity,
            IReadOnlyList<string> names)
        {
            EquipmentType[] types =
            {
                EquipmentType.Weapon, EquipmentType.Weapon,
                EquipmentType.Head, EquipmentType.Head,
                EquipmentType.Body, EquipmentType.Body,
                EquipmentType.Shoes, EquipmentType.Shoes,
                EquipmentType.Accessory, EquipmentType.Accessory
            };
            EquipmentEffectType[] effects =
            {
                EquipmentEffectType.AttackPercent, EquipmentEffectType.SkillDamagePercent,
                EquipmentEffectType.CriticalChancePercentPoint, EquipmentEffectType.CriticalDamagePercent,
                EquipmentEffectType.MaxHealthPercent, EquipmentEffectType.MaxHealthPercent,
                EquipmentEffectType.AttackSpeedPercent, EquipmentEffectType.AttackSpeedPercent,
                EquipmentEffectType.BossDamagePercent, EquipmentEffectType.SkillDamagePercent
            };
            float[] baseValues = { 5f, 5f, 1f, 8f, 8f, 10f, 4f, 5f, 6f, 6f };
            int rarityIndex = (int)rarity + 1;

            for (int i = 0; i < names.Count; i++)
            {
                string typeName = types[i].ToString().ToLowerInvariant();
                int typeNumber = i % 2 + 1;
                string id = $"{rarity.ToString().ToLowerInvariant()}_{typeName}_{typeNumber:00}";
                target.Add(new Definition(
                    $"{rarityIndex:00}_{rarity}_{types[i]}_{typeNumber:00}",
                    id,
                    names[i],
                    types[i],
                    rarity,
                    effects[i],
                    baseValues[i]));
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/LostFamiliar/Resources", "Equipment");
            EnsureFolder(Root, "Items");
            foreach (EquipmentRarity rarity in System.Enum.GetValues(typeof(EquipmentRarity)))
                EnsureFolder(ItemsRoot, rarity.ToString());
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        private readonly struct Definition
        {
            public readonly string fileName;
            public readonly string id;
            public readonly string displayName;
            public readonly EquipmentType type;
            public readonly EquipmentRarity rarity;
            public readonly EquipmentEffectType effectType;
            public readonly float baseEffectValue;

            public Definition(
                string fileName,
                string id,
                string displayName,
                EquipmentType type,
                EquipmentRarity rarity,
                EquipmentEffectType effectType,
                float baseEffectValue)
            {
                this.fileName = fileName;
                this.id = id;
                this.displayName = displayName;
                this.type = type;
                this.rarity = rarity;
                this.effectType = effectType;
                this.baseEffectValue = baseEffectValue;
            }
        }
    }
}
#endif
