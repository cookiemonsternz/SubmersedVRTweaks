using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Nautilus.Handlers;

namespace SubmersedVRTweaks
{
    [BepInPlugin(GUID: PluginGuid, Name: PluginName, Version: PluginVersion)]
    [BepInDependency("com.snmodding.nautilus")]
    [BepInDependency("SubmersedVR")]
    public class SubmersedVRTweaksPlugin : BaseUnityPlugin
    {
        private const string PluginGuid = "cookiemonster_nz.submersedvrtweaks";
        private const string PluginName = "SubmersedVR Tweaks";
        private const string PluginVersion = "1.0.0";
        
        private static readonly Harmony HarmonyInstance = new Harmony(PluginGuid);
        
        public static ManualLogSource Log;
        
        public static ModOptions ModOptions;

        private void Awake()
        {
            ModOptions = OptionsPanelHandler.RegisterModOptions<ModOptions>();
            HarmonyInstance.PatchAll();
            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded!");
            Log = Logger;
        }
    }
}