using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using LethalNetworkAPI;
using UnityEngine;

namespace SubspaceTripmineLC
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency(LethalLib.Plugin.ModGUID)]
    [BepInDependency(MyPluginInfo.PLUGIN_GUID)]
    public class STPlugin : BaseUnityPlugin
    {
        public const string GUID = "funfoxrr.SubspaceTripmine";
        public const string NAME = "Subspace Tripmine";
        public const string VERSION = "1.0.0";

        public static STPlugin Instance { get; private set; }

        private readonly Harmony harmony = new Harmony(GUID);

        internal static ManualLogSource Log;
        public static string assemblyLocation;
        public static AssetBundle assets;

        public static ConfigEntry<bool> enableExplosion;
        public static ConfigEntry<bool> instantlyKillPlayer;
        public static ConfigEntry<int> damageDone;
        public static ConfigEntry<float> plantedTransparency;
        public static ConfigEntry<bool> destroyAfterExplosion;

        void Awake()
        {
            if (Instance == null)
                Instance = this;
            
            Log = BepInEx.Logging.Logger.CreateLogSource(GUID);
            Log.LogInfo(GUID + " has loaded!");
            assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            assets = AssetBundle.LoadFromFile(Path.Combine(assemblyLocation, "funfoxrr__subspacetripmine"));
            if (assets == null)
            {
                Log.LogError("Failed to load custom assets.");
                return;
            }

            enableExplosion = base.Config.Bind(
                "Functionality",
                "Enable Explosion Audio",
                true,
                "If the subspace plays the explosion sfx."
            );
            instantlyKillPlayer = base.Config.Bind(
                "Functionality",
                "Instantly kill player",
                true,
                "Kills the player on contact. Adjust damage with 'Damage' Setting."
            );
            damageDone = base.Config.Bind(
                "Functionality",
                "Damage",
                90,
                "The amount of damage done."
            );
            plantedTransparency = base.Config.Bind(
                "Functionality",
                "Planted Transparency",
                .05f,
                "How transparent the mine is when placed. Best used below 0.15"
            );
            destroyAfterExplosion = base.Config.Bind(
                "Functionality",
                "Destroy after explosion",
                true,
                "If the mine disappears when someone steps on it."
            );


            var rarityConfig = base.Config.Bind(
                "Spawning",
                "Spawn Rate",
                50,
                "The spawn rate. The higher, the more will spawn. Requires restart if using configuration mods"
            );


            var canBuyConfig = base.Config.Bind(
                "Shop",
                "Can Buy",
                true,
                "If it shows up in the shop. Requires restart if using configuration mods"
            );
            var priceConfig = base.Config.Bind(
                "Shop",
                "Price",
                100,
                "The price of the item. Requires restart if using configuration mods"
            );

            int rarity = rarityConfig.Value;
            Item subspace = assets.LoadAsset<Item>("SubspaceTripmineItem.asset");
            if (subspace == null)
            {
                Log.LogError("Failed to load SubspaceTripmineItem asset.");
                return;
            }
            var prefab = subspace.spawnPrefab;
            var comp = prefab.AddComponent<SubspaceTripmine>();
            comp.grabbable = true;
            comp.grabbableToEnemies = true;
            comp.itemProperties = subspace;
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(subspace.spawnPrefab);
            LethalLib.Modules.Items.RegisterScrap(subspace, rarity, LethalLib.Modules.Levels.LevelTypes.All);

            if (canBuyConfig.Value)
            {
                int price = priceConfig.Value;
                TerminalNode iTerminalNode = assets.LoadAsset<TerminalNode>("iTerminalNode.asset");
                if (iTerminalNode == null)
                {
                    Log.LogError("Failed to load iTerminalNode asset.");
                    return;
                }
                LethalLib.Modules.Items.RegisterShopItem(subspace, null, null, iTerminalNode, price);
            }

            //TODO: test
            //harmony.PatchAll(typeof(RMPatch));
        }
    }
}