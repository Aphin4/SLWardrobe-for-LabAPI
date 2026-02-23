using HarmonyLib;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Plugins;
using MEC;
using PlayerRoles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;

namespace SLWardrobe
{
    public class SLWardrobe : Plugin<Config>
    {
        public override string Name => "SLWardrobe";
        public override string Author => "ChochoZagorski forked by Aphin";
        public override string Description => "";
        public override Version RequiredApiVersion => new Version(1, 1, 5, 0);
        public override Version Version => new Version(1, 7, 0 );
        
        public static SLWardrobe Instance { get; private set; }
        private Dictionary<Player, string> playerSuitNames = new Dictionary<Player, string>();
        private static readonly HttpClient HttpClient = new HttpClient();
        private const string VERSION_URL = "https://raw.githubusercontent.com/ChochoZagorski/SLWardrobe/master/version.txt";
        private Harmony _harmony;

        public override void Enable()
        {
            Instance = this;
            PlayerEvents.ChangingRole += OnChangingRole;
            PlayerEvents.Left += OnPlayerLeft;
            ServerEvents.RoundEnded += OnRoundEnded;
            PlayerEvents.Death += OnPlayerDeath;
            Task.Run(async () => await CheckForUpdates());
            _harmony = new Harmony("SLWardobe");
            _harmony.PatchAll();

        }
        
        public override void Disable()
        {
            PlayerEvents.ChangingRole -= OnChangingRole;
            PlayerEvents.Left -= OnPlayerLeft;
            ServerEvents.RoundEnded -= OnRoundEnded;
            PlayerEvents.Death -= OnPlayerDeath;
            _harmony.UnpatchAll("SLWardobe");
            Instance = null;
        }
        
        private async Task CheckForUpdates()
        {
            try
            {
                HttpClient.DefaultRequestHeaders.Clear();
                HttpClient.DefaultRequestHeaders.Add("User-Agent", "SLWardrobe-VersionChecker");
        
                string latestVersion = await HttpClient.GetStringAsync(VERSION_URL);
                latestVersion = latestVersion.Trim();
        
                if (System.Version.TryParse(latestVersion, out var latest) && System.Version.TryParse(Version.ToString(), out var current))
                {
                    if (latest > current)
                    {
                        Logger.Warn($"[SLWardrobe] A new version is available! Current: {Version} | Latest: {latestVersion}");
                        Logger.Warn("[SLWardrobe] Download at: https://github.com/ChochoZagorski/SLWardrobe/releases/latest");
                    }
                    else if (latest < current)
                    {
                        Logger.Info($"[SLWardrobe] There is a... Wait a minute, how do you have a future version? Anyways your version: {Version} | Latest: {latestVersion}");
                        Logger.Info("[SLWardrobe] Seriously how?");
                    }
                    else
                    {
                        Logger.Info($"[SLWardrobe] You are running the latest version ({Version})");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"[SLWardrobe] Could not check for updates: {ex.Message}");
            }
        }
        
        private void OnChangingRole(PlayerChangingRoleEventArgs ev)
        {
            if (ev.NewRole == RoleTypeId.None || ev.NewRole == RoleTypeId.Spectator)
            {
                SuitBinder.RemoveSuit(ev.Player);
				SuitBinder.SetPlayerInvisibility(ev.Player, false);
            }
        }
        
        private void OnPlayerDeath(PlayerDeathEventArgs ev)
        {
            SuitBinder.RemoveSuit(ev.Player);
			SuitBinder.SetPlayerInvisibility(ev.Player, false);
        }
        
        private void OnPlayerLeft(PlayerLeftEventArgs ev)
        {
            SuitBinder.RemoveSuit(ev.Player);
			SuitBinder.SetPlayerInvisibility(ev.Player, false);
            playerSuitNames.Remove(ev.Player);
            GhostModeManager.RemovePlayer(ev.Player.ReferenceHub);
        }
        
        private void OnRoundEnded(RoundEndedEventArgs ev)
        {
            foreach (var player in Player.List)
            {
                SuitBinder.RemoveSuit(player);
            }
            playerSuitNames.Clear();
        }
        
        public void ApplySuit(Player player, string suitName)
        {
            if (!Config.IsEnabled) return;
            if (player.Role == RoleTypeId.None || player.Role == RoleTypeId.Spectator) return;
            Timing.RunCoroutine(ApplySuitCoroutine(player, suitName));
        }
        
        private IEnumerator<float> ApplySuitCoroutine(Player player, string suitName)
        {
            yield return Timing.WaitForSeconds(0.5f);
            
            if (player == null || !player.IsAlive) yield break;
            
            List<BoneBinding> bindings = null;
            SuitConfig suitConfig = null;

            if (Config.Suits.ContainsKey(suitName))
            {
                suitConfig = Config.Suits[suitName];
                bindings = ConvertConfigToBindings(suitConfig);
            }
            else
            {
                Logger.Warn($"Unknown suit: {suitName}. Please define it in the config.");
                yield break;
            }

            SuitBinder.ApplySuit(player, bindings);
            playerSuitNames[player] = suitName;

            if (suitConfig.MakeWearerInvisible)
            {
                SuitBinder.SetPlayerInvisibility(player, true);
            }

            yield return Timing.WaitForSeconds(1f);
            
            var suitData = SuitBinder.GetSuitData(player);
            if (suitData != null)
            {
                int activeCount = suitData.Parts.Count(p => p.GameObject != null);
            }
        }
        
        private List<BoneBinding> ConvertConfigToBindings(SuitConfig suitConfig)
        {
            var bindings = new List<BoneBinding>();
    
            foreach (var part in suitConfig.Parts)
            {
                string HitboxName = BoneMappings.GetBoneName(suitConfig.WearerType, part.BoneName);
                
                var binding = new BoneBinding(
                    part.SchematicName,
                    HitboxName,
                    new Vector3(part.PositionX, part.PositionY, part.PositionZ),
                    new Vector3(part.RotationX, part.RotationY, part.RotationZ),
                    new Vector3(part.ScaleX, part.ScaleY, part.ScaleZ)
                );
        
                binding.HideForWearer = part.HideForWearer;
        
                bindings.Add(binding);
            }
    
            return bindings;
        }

        public string GetPlayerSuitName(Player player)
        {
            return playerSuitNames.ContainsKey(player) ? playerSuitNames[player] : null;
        }
    }
}
