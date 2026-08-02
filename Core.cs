using HarmonyLib;
using MelonLoader;
using SteamShelf.Placeables;
using SteamShelf.PlayerTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

[assembly: MelonInfo(typeof(LocalWorkshop.Core), "LocalWorkshop", "1.0.0", "MidgetBrony", null)]
[assembly: MelonGame("NestedLoop", "BOXROOM")]

namespace LocalWorkshop;

public class Core : MelonMod
{
    public override void OnInitializeMelon()
    {
        LoggerInstance.Msg("Initialized Local Workshop.");
    }
}

[HarmonyPatch(typeof(ModsPlaceableLoader), nameof(ModsPlaceableLoader.LoadPlaceableData))]
internal static class LocalWorkshopPatch
{
    static void Postfix(
        ModsPlaceableLoader __instance,
        ref Task<List<PlaceableData>> __result)
    {
        __result = LoadLocalModsAsync(__instance, __result);
    }

    private static async Task<List<PlaceableData>> LoadLocalModsAsync(
        ModsPlaceableLoader loader,
        Task<List<PlaceableData>> originalTask)
    {
        List<PlaceableData> placeables = await originalTask;

        string modsFolder = Path.Combine(
            Application.persistentDataPath,
            "Mods");

        if (!Directory.Exists(modsFolder))
        {
            Directory.CreateDirectory(modsFolder);
            return placeables;
        }

        // Grab the private dictionaries
        var modelField = typeof(ModsPlaceableLoader)
            .GetField("modModelPaths", BindingFlags.Instance | BindingFlags.NonPublic);

        var spriteField = typeof(ModsPlaceableLoader)
            .GetField("modSpritePaths", BindingFlags.Instance | BindingFlags.NonPublic);

        var modelPaths =
            (Dictionary<PlaceableData, string>)modelField.GetValue(loader);

        var spritePaths =
            (Dictionary<PlaceableData, string>)spriteField.GetValue(loader);

        foreach (string folder in Directory.GetDirectories(modsFolder))
        {
            string manifestPath = Path.Combine(folder, "manifest.json");

            if (!File.Exists(manifestPath))
                continue;

            try
            {
                ModManifest manifest =
                    JsonUtility.FromJson<ModManifest>(
                        await File.ReadAllTextAsync(manifestPath));

                PlaceableData data =
                    ScriptableObject.CreateInstance<PlaceableData>();

                data.ID = manifest.ID;
                data.DisplayName = manifest.DisplayName;
                data.IsLoadedFromMod = true;
                data.ModScale = manifest.Scale > 0 ? manifest.Scale : 1f;

                if (Enum.TryParse(manifest.PlacementType, out PlacementType placement))
                    data.PlacementType = placement;

                if (Enum.TryParse(manifest.ToolType, out EToolType tool))
                    data.SetToolType(tool);

                modelPaths[data] =
                    Path.Combine(folder, manifest.ModelPath);

                spritePaths[data] =
                    Path.Combine(folder, manifest.SpritePath);

                placeables.Add(data);

                MelonLogger.Msg($"Loaded local mod: {data.DisplayName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error(ex);
            }
        }

        return placeables;
    }
}