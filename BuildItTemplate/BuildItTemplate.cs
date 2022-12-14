using System.Reflection;
using BepInEx;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using fastJSON;

namespace BuildItTemplate
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    //[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class BuildItTemplate : BaseUnityPlugin
    {
        public const string PluginGUID = "com.RockerKitten.BuildItTemplate";
        public const string PluginName = "BuildItTemplate";
        public const string PluginVersion = "1.0.0";

        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

        private AssetBundle BuildItAssetBundle { get; set; }
        //private AudioSource fireAudioSource;

        private Dictionary<BuildItMaterial, BuildItEffectLists> effects;

        private void Awake()
        {
            LoadEmbeddedAssembly("fastJSON.dll");
            this.BuildItAssetBundle = AssetUtils.LoadAssetBundleFromResources("BuildItTemplate", Assembly.GetExecutingAssembly());

            PrefabManager.OnVanillaPrefabsAvailable += SetupAssets;
            Jotunn.Logger.LogInfo("BuildItTemplate has landed");
        }

        private void SetupAssets()
        {
            this.effects = InitializeEffects();
            if (PrefabManager.Cache.GetPrefab<"rkc_scepter"> == null)
            {
                InitializeBuildItConstructionTools();
            }
            InitializeBuildItConstructionTools();
            InitializeBuildItAssets();
            PrefabManager.OnVanillaPrefabsAvailable -= SetupAssets;
        }

        private void InitializeBuildItConstructionTools()
        {
            var hammerTableFab = this.BuildItAssetBundle.LoadAsset<GameObject>("_RKC_CustomTable");
            var masonryTable = new CustomPieceTable(hammerTableFab,
                new PieceTableConfig
                {
                    CanRemovePieces = true,
                    UseCategories = false,
                    UseCustomCategories = true,
                    CustomCategories = new string[]
                    {
                        "Structure"
                    }
                });
            PieceManager.Instance.AddPieceTable(masonryTable);
            var toolFab = this.BuildItAssetBundle.LoadAsset<GameObject>("rkc_trowel");
            var tool = new CustomItem(toolFab, false,
                new ItemConfig
                {
                    Name = "$item_rkctrowel",
                    Description = "Build it castle style with a good old fashioned masonry trowel.",
                    Amount = 1,
                    Enabled = true,
                    CraftingStation = "forge",
                    PieceTable = masonryTable.PieceTablePrefab.name,
                    RepairStation = "forge",
                    Requirements = new[]
                    {
                        new RequirementConfig {Item = "Wood", Amount = 1},
                        new RequirementConfig {Item = "Iron", Amount = 1 }
                    }
                });
                
            ItemManager.Instance.AddItem(tool);
        }

        private void InitializeBuildItAssets()
        {
            var buildItAssets = LoadEmbeddedJsonFile<BuildItAssets>("builditassets.json");

            foreach (var buildItPieceTable in buildItAssets.PieceTables)
            {
                foreach (var buildItPieceCategory in buildItPieceTable.Categories)
                {
                    foreach (var buildItPiece in buildItPieceCategory.Pieces)
                    {
                        var customPiece = this.BuildCustomPiece(buildItPieceTable, buildItPieceCategory, buildItPiece);

                        // load supplemental assets (sfx and vfx)
                        this.AttachEffects(customPiece.PiecePrefab, buildItPiece);

                        PieceManager.Instance.AddPiece(customPiece);
                    }
                }
            }
        }

        private Dictionary<BuildItMaterial, BuildItEffectLists> InitializeEffects()
        {
            Dictionary<string, GameObject> effectCache = new Dictionary<string, GameObject>();
            GameObject loadfx(string prefabName)
            {
                if (!effectCache.ContainsKey(prefabName))
                {
                    effectCache.Add(prefabName, PrefabManager.Cache.GetPrefab<GameObject>(prefabName));
                }
                return effectCache[prefabName];
            }
            EffectList createfxlist(params string[] effectsList) => new EffectList { m_effectPrefabs = effectsList.Select(fx => new EffectList.EffectData { m_prefab = loadfx(fx) }).ToArray() };

            var effects = new Dictionary<BuildItMaterial, BuildItEffectLists>
            {
                {
                    BuildItMaterial.Wood,
                    new BuildItEffectLists
                    {
                        Place = createfxlist("sfx_build_hammer_wood", "vfx_Place_stone_wall_2x1"),
                        Break = createfxlist("sfx_wood_break", "vfx_SawDust"),
                        Hit   = createfxlist("vfx_SawDust"),
                        Open  = createfxlist("sfx_door_open"),
                        Close = createfxlist("sfx_door_close"),
                        Fuel  = createfxlist("vfx_HearthAddFuel"),
                    }
                },
                {
                    BuildItMaterial.Stone,
                    new BuildItEffectLists
                    {
                        Place = createfxlist("sfx_build_hammer_stone", "vfx_Place_stone_wall_2x1"),
                        Break = createfxlist("sfx_rock_destroyed", "vfx_Place_stone_wall_2x1"),
                        Hit   = createfxlist("sfx_Rock_Hit"),
                        Open  = createfxlist("sfx_door_open"),
                        Close = createfxlist("sfx_door_close"),
                        Fuel  = createfxlist("vfx_HearthAddFuel"),
                    }
                },
                {
                    BuildItMaterial.Metal,
                    new BuildItEffectLists
                    {
                        Place = createfxlist("sfx_build_hammer_metal", "vfx_Place_stone_wall_2x1"),
                        Break = createfxlist("sfx_rock_destroyed", "vfx_HitSparks"),
                        Hit   = createfxlist("vfx_HitSparks"),
                        Open  = createfxlist("sfx_door_open"),
                        Close = createfxlist("sfx_door_close"),
                        Fuel  = createfxlist("vfx_HearthAddFuel"),
                    }
                }
            };

            return effects;
        }

        //private void AddLocalizations()
        //{
        //    CustomLocalization customLocalization = new CustomLocalization();
        //    customLocalization.AddTranslation("English", new Dictionary<String, String>
        //    {
        //        { "piece_wallrkc", "Wall" }
        //    });
        //}

        private CustomPiece BuildCustomPiece(BuildItPieceTable buildItPieceTable, BuildItPieceCategories buildItPieceCategory, BuildItPiece buildItPiece)
        {
            var buildItPiecePrefab = this.BuildItAssetBundle.LoadAsset<GameObject>(buildItPiece.PrefabName);

            var pieceConfig = new PieceConfig();
            // TODO: verify token string
            pieceConfig.Name = buildItPiece.DisplayNameToken;
            pieceConfig.Description = buildItPiece.PrefabDescription;
            // NOTE: could move override to json config if needed.
            pieceConfig.AllowedInDungeons = false;
            pieceConfig.PieceTable = buildItPieceTable.TableName;
            pieceConfig.Category = buildItPieceCategory.CategoryTabName;
            pieceConfig.Enabled = buildItPiece.Enabled;
            if (!string.IsNullOrWhiteSpace(buildItPiece.RequiredStation))
            {
                pieceConfig.CraftingStation = buildItPiece.RequiredStation;
            }

            var requirements = buildItPiece.Requirements
                .Select(r => new RequirementConfig(r.Item, r.Amount, recover: r.Recover));

            pieceConfig.Requirements = requirements.ToArray();
            var customPiece = new CustomPiece(buildItPiecePrefab, fixReference: false, pieceConfig);
            var material = buildItPiecePrefab.GetComponentsInChildren<Material>();
            foreach (Material mat in material)
            {
                if (mat.name == "replace")
                {
                    mat.shader = Shader.Find("Custom/Piece");
                }
            }
            Jotunn.Logger.LogInfo(buildItPiecePrefab.name);
            return customPiece;
        }

        private void AttachEffects(GameObject piecePrefab, BuildItPiece buildItPiece)
        {
            var pieceComponent = piecePrefab.GetComponent<Piece>();
            pieceComponent.m_placeEffect = this.effects[buildItPiece.Material].Place;

            var wearComponent = piecePrefab.GetComponent<WearNTear>();
            wearComponent.m_destroyedEffect = this.effects[buildItPiece.Material].Break;
            wearComponent.m_hitEffect = this.effects[buildItPiece.Material].Hit;

            if (piecePrefab.TryGetComponent<Door>(out Door doorComponent))
            {
                doorComponent.m_openEffects = this.effects[buildItPiece.Material].Open;
                doorComponent.m_closeEffects = this.effects[buildItPiece.Material].Close;
            }

            if (piecePrefab.TryGetComponent<Fireplace>(out Fireplace fireplaceComponent))
            {
                fireplaceComponent.m_fuelAddedEffects = this.effects[buildItPiece.Material].Fuel;
                //fireplaceComponent.m_fuelItem = this.[buildItPiece.FuelItem];
                // how to add fuel type?
                //fireAudioSource = piecePrefab.GetComponentInChildren<AudioSource>();
                //fireAudioSource.outputAudioMixerGroup = AudioMan.instance.m_ambientMixer;
            }

        }

        // LOADING EMBEDDED RESOURCES
        private void LoadEmbeddedAssembly(string assemblyName)
        {
            var stream = GetManifestResourceStream(assemblyName);
            if (stream == null)
            {
                Logger.LogError($"Could not load embedded assembly ({assemblyName})!");
                return;
            }

            using (stream)
            {
                var data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);
                Assembly.Load(data);
            }
        }

        private Stream GetManifestResourceStream(string filename)
        {
            var assembly = Assembly.GetCallingAssembly();
            var fullname = assembly.GetManifestResourceNames().SingleOrDefault(x => x.EndsWith(filename));
            if (!string.IsNullOrEmpty(fullname))
            {
                return assembly.GetManifestResourceStream(fullname);
            }

            return null;
        }

        private T LoadEmbeddedJsonFile<T>(string filename) where T : class
        {
            string jsonFileText = String.Empty;

            using (StreamReader reader = new StreamReader(LoadEmbeddedJsonStream(filename)))
            {
                jsonFileText = reader.ReadToEnd();
            }

            T result;

            try
            {
                var jsonParameters = new JSONParameters
                {
                    AutoConvertStringToNumbers = true,
                };
                result = string.IsNullOrEmpty(jsonFileText) ? null : JSON.ToObject<T>(jsonFileText, jsonParameters);
            }
            catch (Exception)
            {
                Logger.LogError($"Could not parse file '{filename}'! Errors in JSON!");
                throw;
            }

            return result;
        }

        private Stream LoadEmbeddedJsonStream(string filename)
        {
            return this.GetManifestResourceStream(filename);
        }
    }
}

