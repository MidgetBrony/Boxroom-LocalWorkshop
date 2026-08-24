using Catalogue;
using HarmonyLib;
using MelonLoader;
using SteamShelf.Items;
using SteamShelf.Placeables;
using SteamShelf.PlayerTools;
using SteamShelf.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

[assembly: MelonInfo(
    typeof(LocalWorkshop.Core),
    "LocalWorkshop",
    "1.1.1",
    "MidgetBrony",
    null)]

[assembly: MelonGame("NestedLoop", "BOXROOM")]

namespace LocalWorkshop
{
    // ============================================================
    // CORE
    // ============================================================

    public class Core : MelonMod
    {
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg(
                "Initialized Local Workshop + Custom Catalogues.");
        }
    }


    // ============================================================
    // MANIFESTS
    // ============================================================

    [Serializable]
    public class ModManifest
    {
        public string ID;
        public string DisplayName;

        public string PlacementType = "Furniture";
        public string ModelPath;
        public string SpritePath;

        public string ToolType = "Placeable";

        public float Scale = 1f;
    }


    [Serializable]
    public class CatalogManifest
    {
        public string ID;
        public string DisplayName;

        public string Author;
        public string Version;

        // Relative to catalogue folder.
        public string Cover = "cover.png";

        // 0 = use BOXROOM Mods catalogue page size.
        public int ItemsPerPage = 0;
    }


    // ============================================================
    // RUNTIME CATALOGUE STRUCTURES
    // ============================================================

    internal sealed class LoadedCatalog
    {
        public string FolderPath;
        public CatalogManifest Manifest;

        public Texture2D CoverTexture;

        public readonly List<LoadedCategory> Categories =
            new List<LoadedCategory>();

        public CatalogueData RuntimeData;

        public readonly List<CataloguePageDefinition> RuntimePages =
            new List<CataloguePageDefinition>();
    }


    internal sealed class LoadedCategory
    {
        public string FolderPath;
        public string DisplayName;

        public readonly List<PlaceableData> Items =
            new List<PlaceableData>();
    }


    // ============================================================
    // CATALOGUE REGISTRY
    // ============================================================

    internal static class CatalogRegistry
    {
        public static readonly List<LoadedCatalog> Catalogs =
            new List<LoadedCatalog>();

        // IDs belonging to our custom catalogues.
        //
        // They still exist as normal mod PlaceableData underneath.
        // This only lets us distinguish their presentation.
        public static readonly HashSet<string> CatalogItemIDs =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);


        public static bool HasCatalogues =>
            Catalogs.Count > 0;


        public static bool IsCatalogItem(
            PlaceableData data)
        {
            if (data == null ||
                string.IsNullOrWhiteSpace(data.ID))
            {
                return false;
            }

            return CatalogItemIDs.Contains(
                data.ID);
        }


        public static void Reset()
        {
            CatalogItemIDs.Clear();

            foreach (LoadedCatalog catalog in Catalogs)
            {
                foreach (CataloguePageDefinition page
                         in catalog.RuntimePages)
                {
                    if (page != null)
                    {
                        UnityEngine.Object.Destroy(
                            page);
                    }
                }

                catalog.RuntimePages.Clear();

                if (catalog.RuntimeData != null)
                {
                    UnityEngine.Object.Destroy(
                        catalog.RuntimeData);

                    catalog.RuntimeData = null;
                }

                if (catalog.CoverTexture != null)
                {
                    UnityEngine.Object.Destroy(
                        catalog.CoverTexture);

                    catalog.CoverTexture = null;
                }
            }

            Catalogs.Clear();
        }


        // ========================================================
        // BUILD RUNTIME CATALOGUES
        // ========================================================

        public static void BuildRuntimeCatalogues(
            List<CatalogueData> existingCatalogues)
        {
            if (!HasCatalogues)
                return;

            if (Catalogs.All(
                x => x.RuntimeData != null))
            {
                return;
            }


            CatalogueData normalTemplate =
                existingCatalogues.FirstOrDefault(
                    x =>
                        x != null &&
                        !x.IsModsCatalogue &&
                        x.ContentsPagePrefab != null &&
                        x.PageNumberPrefab != null);


            CatalogueData modsTemplate =
                existingCatalogues.FirstOrDefault(
                    x =>
                        x != null &&
                        x.IsModsCatalogue);


            if (normalTemplate == null)
            {
                MelonLogger.Error(
                    "[Catalog] Could not locate a vanilla " +
                    "catalogue template.");

                return;
            }


            UI_PageTemplate pageTemplate = null;


            // Prefer the Mods catalogue page template.
            // We know it supports mod PlaceableData.
            if (modsTemplate != null &&
                modsTemplate.Pages != null &&
                modsTemplate.Pages.Length > 0)
            {
                pageTemplate =
                    modsTemplate.Pages[0]?.PageTemplate;
            }


            // Fallback to any vanilla page template.
            if (pageTemplate == null &&
                normalTemplate.Pages != null)
            {
                foreach (CataloguePageDefinition page
                         in normalTemplate.Pages)
                {
                    if (page?.PageTemplate != null)
                    {
                        pageTemplate =
                            page.PageTemplate;

                        break;
                    }
                }
            }


            if (pageTemplate == null)
            {
                MelonLogger.Error(
                    "[Catalog] Could not locate a usable " +
                    "catalogue page template.");

                return;
            }


            int defaultItemsPerPage = 8;


            if (modsTemplate != null &&
                modsTemplate.ModsItemsPerPage > 0)
            {
                defaultItemsPerPage =
                    modsTemplate.ModsItemsPerPage;
            }


            foreach (LoadedCatalog catalog
                     in Catalogs)
            {
                try
                {
                    BuildSingleCatalogue(
                        catalog,
                        normalTemplate,
                        pageTemplate,
                        defaultItemsPerPage);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error(
                        $"[Catalog] Failed building catalogue " +
                        $"'{catalog.Manifest?.ID}': {ex}");
                }
            }
        }


        private static void BuildSingleCatalogue(
            LoadedCatalog catalog,
            CatalogueData normalTemplate,
            UI_PageTemplate pageTemplate,
            int defaultItemsPerPage)
        {
            if (catalog.RuntimeData != null)
                return;


            CatalogueData data =
                ScriptableObject
                    .CreateInstance<CatalogueData>();


            data.name =
                $"LocalCatalog_{catalog.Manifest.ID}";


            data.CoverTexture =
                catalog.CoverTexture != null
                    ? catalog.CoverTexture
                    : normalTemplate.CoverTexture;


            data.ContentsPagePrefab =
                normalTemplate.ContentsPagePrefab;


            data.PageNumberPrefab =
                normalTemplate.PageNumberPrefab;


            data.IsModsCatalogue = false;
            data.ModsItemsPerPage = 0;


            int itemsPerPage =
                catalog.Manifest.ItemsPerPage > 0
                    ? catalog.Manifest.ItemsPerPage
                    : defaultItemsPerPage;


            List<CataloguePageDefinition> pages =
                new List<CataloguePageDefinition>();


            foreach (LoadedCategory category
                     in catalog.Categories)
            {
                if (category.Items.Count == 0)
                    continue;


                int pageCount =
                    Mathf.CeilToInt(
                        (float)category.Items.Count /
                        itemsPerPage);


                for (int pageIndex = 0;
                     pageIndex < pageCount;
                     pageIndex++)
                {
                    int start =
                        pageIndex * itemsPerPage;


                    int count =
                        Mathf.Min(
                            itemsPerPage,
                            category.Items.Count - start);


                    Item[] items =
                        new Item[count];


                    for (int i = 0;
                         i < count;
                         i++)
                    {
                        items[i] =
                            category.Items[start + i];
                    }


                    CataloguePageDefinition page =
                        ScriptableObject
                            .CreateInstance<
                                CataloguePageDefinition>();


                    page.name =
                        $"{catalog.Manifest.ID}_" +
                        $"{category.DisplayName}_" +
                        $"{pageIndex + 1}";


                    page.PageName =
                        category.DisplayName;


                    page.PageTemplate =
                        pageTemplate;


                    page.FillItems(
                        items);


                    // No DLC / EA / demo requirements.
                    page.MustHaveAppId =
                        Array.Empty<int>();

                    page.LinkedLootLockerAssetId = 0;
                    page.MustHavePlayedDemo = false;
                    page.MustHavePlayedEA = false;


                    pages.Add(
                        page);

                    catalog.RuntimePages.Add(
                        page);
                }
            }


            data.Pages =
                pages.ToArray();


            catalog.RuntimeData =
                data;


            MelonLogger.Msg(
                $"[Catalog] Built '{catalog.Manifest.DisplayName}' " +
                $"with {pages.Count} page(s).");
        }


        public static IEnumerable<CatalogueData>
            GetRuntimeCatalogues()
        {
            foreach (LoadedCatalog catalog
                     in Catalogs)
            {
                if (catalog.RuntimeData != null)
                {
                    yield return
                        catalog.RuntimeData;
                }
            }
        }


        // ========================================================
        // GENERIC MODS BOOK CHECK
        // ========================================================

        public static bool HasNonCatalogMods()
        {
            try
            {
                List<PlaceableData> mods =
                    Singleton<PlaceableManager>
                        .Instance
                        .GetLoadedModPlaceables();


                if (mods == null)
                    return false;


                foreach (PlaceableData data
                         in mods)
                {
                    if (data == null)
                        continue;


                    if (!IsCatalogItem(data))
                        return true;
                }
            }
            catch
            {
                // Better to leave the Mods catalogue visible
                // than accidentally hide it.
                return true;
            }


            return false;
        }
    }


    // ============================================================
    // PLACEABLE LOADER
    //
    // Layout:
    //
    // BOXROOM/
    //   Mods/
    //     LooseMod/
    //       manifest.json
    //
    //     Catalogs/
    //       HomeCollection/
    //         catalog.json
    //         cover.png
    //
    //         01_Bathroom/
    //           Bathtub/
    //             manifest.json
    //             bathtub.glb
    //             Screenshot.png
    //
    // All catalogue items still enter BOXROOM through the normal
    // ModsPlaceableLoader data structures.
    // ============================================================

    [HarmonyPatch(
        typeof(ModsPlaceableLoader),
        nameof(ModsPlaceableLoader.LoadPlaceableData))]
    internal static class LocalWorkshopPatch
    {
        static void Postfix(
            ModsPlaceableLoader __instance,
            ref Task<List<PlaceableData>> __result)
        {
            __result =
                LoadLocalContentAsync(
                    __instance,
                    __result);
        }


        private static async Task<List<PlaceableData>>
            LoadLocalContentAsync(
                ModsPlaceableLoader loader,
                Task<List<PlaceableData>> originalTask)
        {
            List<PlaceableData> placeables =
                await originalTask;


            CatalogRegistry.Reset();


            // ----------------------------------------------------
            // PRIVATE BOXROOM PATH DICTIONARIES
            // ----------------------------------------------------

            FieldInfo modelField =
                typeof(ModsPlaceableLoader)
                    .GetField(
                        "modModelPaths",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);


            FieldInfo spriteField =
                typeof(ModsPlaceableLoader)
                    .GetField(
                        "modSpritePaths",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);


            if (modelField == null ||
                spriteField == null)
            {
                MelonLogger.Error(
                    "[LocalWorkshop] Could not find " +
                    "ModsPlaceableLoader path dictionaries.");

                return placeables;
            }


            Dictionary<PlaceableData, string> modelPaths =
                (Dictionary<PlaceableData, string>)
                    modelField.GetValue(loader);


            Dictionary<PlaceableData, string> spritePaths =
                (Dictionary<PlaceableData, string>)
                    spriteField.GetValue(loader);


            if (modelPaths == null ||
                spritePaths == null)
            {
                MelonLogger.Error(
                    "[LocalWorkshop] BOXROOM mod path " +
                    "dictionaries were null.");

                return placeables;
            }


            // ----------------------------------------------------
            // MOD ROOT
            // ----------------------------------------------------

            string modsFolder =
                Path.Combine(
                    Application.persistentDataPath,
                    "Mods");


            if (!Directory.Exists(modsFolder))
            {
                Directory.CreateDirectory(
                    modsFolder);
            }


            MelonLogger.Msg(
                $"[LocalWorkshop] Mods root: " +
                $"{modsFolder}");


            // ----------------------------------------------------
            // LOOSE LOCAL MODS
            // ----------------------------------------------------

            await LoadLooseMods(
                modsFolder,
                placeables,
                modelPaths,
                spritePaths);


            // ----------------------------------------------------
            // CUSTOM CATALOGUES
            //
            // IMPORTANT:
            //
            // BOXROOM/Mods/Catalogs
            // ----------------------------------------------------

            string catalogsFolder =
                Path.Combine(
                    modsFolder,
                    "Catalogs");


            MelonLogger.Msg(
                $"[Catalog] Looking for catalogues at: " +
                $"{catalogsFolder}");


            if (!Directory.Exists(catalogsFolder))
            {
                MelonLogger.Msg(
                    "[Catalog] Catalogs folder does not " +
                    "exist. Creating it.");

                Directory.CreateDirectory(
                    catalogsFolder);
            }


            await LoadCatalogs(
                catalogsFolder,
                placeables,
                modelPaths,
                spritePaths);


            MelonLogger.Msg(
                $"[LocalWorkshop] Total custom catalogues: " +
                $"{CatalogRegistry.Catalogs.Count}");


            return placeables;
        }


        // ========================================================
        // LOOSE MODS
        // ========================================================

        private static async Task LoadLooseMods(
            string modsFolder,
            List<PlaceableData> placeables,
            Dictionary<PlaceableData, string> modelPaths,
            Dictionary<PlaceableData, string> spritePaths)
        {
            string[] folders =
                Directory.GetDirectories(
                    modsFolder);


            MelonLogger.Msg(
                $"[LocalWorkshop] Found {folders.Length} " +
                $"folder(s) under Mods.");


            foreach (string folder
                     in folders)
            {
                string folderName =
                    Path.GetFileName(
                        folder);


                // Catalogs is a container, not a loose mod.
                if (string.Equals(
                    folderName,
                    "Catalogs",
                    StringComparison.OrdinalIgnoreCase))
                {
                    MelonLogger.Msg(
                        "[LocalWorkshop] Skipping Catalogs " +
                        "during loose mod scan.");

                    continue;
                }


                string manifestPath =
                    Path.Combine(
                        folder,
                        "manifest.json");


                if (!File.Exists(manifestPath))
                    continue;


                PlaceableData data =
                    await LoadPlaceable(
                        folder,
                        manifestPath,
                        modelPaths,
                        spritePaths);


                if (data == null)
                    continue;


                placeables.Add(
                    data);


                MelonLogger.Msg(
                    $"[LocalWorkshop] Loaded loose mod: " +
                    $"{data.DisplayName}");
            }
        }


        // ========================================================
        // CUSTOM CATALOGUES
        // ========================================================

        private static async Task LoadCatalogs(
            string catalogsRoot,
            List<PlaceableData> placeables,
            Dictionary<PlaceableData, string> modelPaths,
            Dictionary<PlaceableData, string> spritePaths)
        {
            MelonLogger.Msg(
                $"[Catalog] Scanning root: " +
                $"{catalogsRoot}");


            if (!Directory.Exists(catalogsRoot))
            {
                MelonLogger.Warning(
                    $"[Catalog] Root does not exist: " +
                    $"{catalogsRoot}");

                return;
            }


            string[] catalogFolders =
                Directory
                    .GetDirectories(catalogsRoot)
                    .OrderBy(
                        x => Path.GetFileName(x))
                    .ToArray();


            MelonLogger.Msg(
                $"[Catalog] Found {catalogFolders.Length} " +
                $"candidate catalogue folder(s).");


            foreach (string catalogFolder
                     in catalogFolders)
            {
                string catalogFolderName =
                    Path.GetFileName(
                        catalogFolder);


                MelonLogger.Msg(
                    $"[Catalog] Checking catalogue folder: " +
                    $"{catalogFolderName}");


                string catalogManifestPath =
                    Path.Combine(
                        catalogFolder,
                        "catalog.json");


                if (!File.Exists(catalogManifestPath))
                {
                    MelonLogger.Warning(
                        $"[Catalog] Skipping " +
                        $"'{catalogFolderName}': " +
                        $"catalog.json not found.");

                    continue;
                }


                MelonLogger.Msg(
                    $"[Catalog] Found metadata: " +
                    $"{catalogManifestPath}");


                try
                {
                    // --------------------------------------------
                    // CATALOG.JSON
                    // --------------------------------------------

                    string json =
                        await File.ReadAllTextAsync(
                            catalogManifestPath);


                    CatalogManifest manifest =
                        JsonUtility
                            .FromJson<CatalogManifest>(
                                json);


                    if (manifest == null)
                    {
                        MelonLogger.Warning(
                            $"[Catalog] Invalid catalog.json: " +
                            $"{catalogManifestPath}");

                        continue;
                    }


                    if (string.IsNullOrWhiteSpace(
                            manifest.ID))
                    {
                        manifest.ID =
                            catalogFolderName;


                        MelonLogger.Warning(
                            $"[Catalog] Catalogue had no ID. " +
                            $"Using folder name: " +
                            $"{manifest.ID}");
                    }


                    if (string.IsNullOrWhiteSpace(
                            manifest.DisplayName))
                    {
                        manifest.DisplayName =
                            PrettyName(
                                catalogFolderName);
                    }


                    if (string.IsNullOrWhiteSpace(
                            manifest.Cover))
                    {
                        manifest.Cover =
                            "cover.png";
                    }


                    MelonLogger.Msg(
                        $"[Catalog] Loading catalogue: " +
                        $"{manifest.DisplayName} " +
                        $"[{manifest.ID}]");


                    LoadedCatalog catalog =
                        new LoadedCatalog
                        {
                            FolderPath =
                                catalogFolder,

                            Manifest =
                                manifest
                        };


                    // --------------------------------------------
                    // COVER
                    // --------------------------------------------

                    string coverPath =
                        Path.Combine(
                            catalogFolder,
                            manifest.Cover);


                    MelonLogger.Msg(
                        $"[Catalog] Cover path: " +
                        $"{coverPath}");


                    if (File.Exists(coverPath))
                    {
                        try
                        {
                            byte[] bytes =
                                await File.ReadAllBytesAsync(
                                    coverPath);


                            Texture2D texture =
                                new Texture2D(
                                    2,
                                    2,
                                    TextureFormat.RGBA32,
                                    false);


                            texture.name =
                                $"CatalogCover_{manifest.ID}";


                            if (texture.LoadImage(bytes))
                            {
                                catalog.CoverTexture =
                                    texture;


                                MelonLogger.Msg(
                                    $"[Catalog] Loaded cover " +
                                    $"({texture.width}x" +
                                    $"{texture.height}).");
                            }
                            else
                            {
                                MelonLogger.Warning(
                                    "[Catalog] Unity failed " +
                                    "to decode cover image: " +
                                    coverPath);


                                UnityEngine.Object.Destroy(
                                    texture);
                            }
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Warning(
                                $"[Catalog] Failed loading cover " +
                                $"for {manifest.ID}: " +
                                $"{ex.Message}");
                        }
                    }
                    else
                    {
                        MelonLogger.Warning(
                            $"[Catalog] Cover not found: " +
                            $"{coverPath}");
                    }


                    // --------------------------------------------
                    // CATEGORY FOLDERS
                    // --------------------------------------------

                    string[] categoryFolders =
                        Directory
                            .GetDirectories(
                                catalogFolder)
                            .OrderBy(
                                x => Path.GetFileName(x))
                            .ToArray();


                    MelonLogger.Msg(
                        $"[Catalog] " +
                        $"'{manifest.DisplayName}' has " +
                        $"{categoryFolders.Length} " +
                        $"category folder(s).");


                    foreach (string categoryFolder
                             in categoryFolders)
                    {
                        string rawCategoryName =
                            Path.GetFileName(
                                categoryFolder);


                        string displayCategoryName =
                            PrettyCategoryName(
                                rawCategoryName);


                        MelonLogger.Msg(
                            $"[Catalog] Category: " +
                            $"{rawCategoryName} -> " +
                            $"{displayCategoryName}");


                        LoadedCategory category =
                            new LoadedCategory
                            {
                                FolderPath =
                                    categoryFolder,

                                DisplayName =
                                    displayCategoryName
                            };


                        // ----------------------------------------
                        // ITEM FOLDERS
                        // ----------------------------------------

                        string[] itemFolders =
                            Directory
                                .GetDirectories(
                                    categoryFolder)
                                .OrderBy(
                                    x => Path.GetFileName(x))
                                .ToArray();


                        MelonLogger.Msg(
                            $"[Catalog] Category " +
                            $"'{displayCategoryName}' contains " +
                            $"{itemFolders.Length} " +
                            $"item folder(s).");


                        foreach (string itemFolder
                                 in itemFolders)
                        {
                            string itemFolderName =
                                Path.GetFileName(
                                    itemFolder);


                            string manifestPath =
                                Path.Combine(
                                    itemFolder,
                                    "manifest.json");


                            if (!File.Exists(manifestPath))
                            {
                                MelonLogger.Warning(
                                    $"[Catalog] Skipping item " +
                                    $"folder '{itemFolderName}': " +
                                    $"manifest.json missing.");

                                continue;
                            }


                            PlaceableData data =
                                await LoadPlaceable(
                                    itemFolder,
                                    manifestPath,
                                    modelPaths,
                                    spritePaths);


                            if (data == null)
                            {
                                MelonLogger.Warning(
                                    $"[Catalog] Failed loading " +
                                    $"item: {itemFolderName}");

                                continue;
                            }


                            // ------------------------------------
                            // DUPLICATE CATALOG ID
                            // ------------------------------------

                            if (CatalogRegistry
                                .CatalogItemIDs
                                .Contains(data.ID))
                            {
                                MelonLogger.Error(
                                    $"[Catalog] Duplicate " +
                                    $"catalogue item ID " +
                                    $"'{data.ID}'. Skipping " +
                                    $"{manifestPath}");


                                UnityEngine.Object.Destroy(
                                    data);


                                continue;
                            }


                            CatalogRegistry
                                .CatalogItemIDs
                                .Add(data.ID);


                            category.Items.Add(
                                data);


                            // Still part of BOXROOM's normal
                            // loaded mod collection.
                            //
                            // That is deliberate: model loading,
                            // placement and save/load continue to
                            // use the Workshop/mod pathway.
                            placeables.Add(
                                data);


                            MelonLogger.Msg(
                                $"[Catalog] Loaded item: " +
                                $"{manifest.DisplayName} / " +
                                $"{displayCategoryName} / " +
                                $"{data.DisplayName} " +
                                $"[{data.ID}]");
                        }


                        if (category.Items.Count > 0)
                        {
                            catalog.Categories.Add(
                                category);


                            MelonLogger.Msg(
                                $"[Catalog] Added category " +
                                $"'{displayCategoryName}' with " +
                                $"{category.Items.Count} item(s).");
                        }
                        else
                        {
                            MelonLogger.Warning(
                                $"[Catalog] Category " +
                                $"'{displayCategoryName}' " +
                                $"contained no valid items.");
                        }
                    }


                    // --------------------------------------------
                    // REGISTER CATALOGUE
                    // --------------------------------------------

                    if (catalog.Categories.Count > 0)
                    {
                        CatalogRegistry
                            .Catalogs
                            .Add(catalog);


                        int itemCount =
                            catalog.Categories
                                .Sum(
                                    x => x.Items.Count);


                        MelonLogger.Msg(
                            $"[Catalog] SUCCESS: Loaded " +
                            $"catalogue " +
                            $"'{manifest.DisplayName}' with " +
                            $"{catalog.Categories.Count} " +
                            $"categories / " +
                            $"{itemCount} items.");
                    }
                    else
                    {
                        MelonLogger.Warning(
                            $"[Catalog] Catalogue " +
                            $"'{manifest.DisplayName}' " +
                            $"contained no valid categories/" +
                            $"items and will not be registered.");


                        if (catalog.CoverTexture != null)
                        {
                            UnityEngine.Object.Destroy(
                                catalog.CoverTexture);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error(
                        $"[Catalog] Failed loading " +
                        $"{catalogManifestPath}: {ex}");
                }
            }
        }


        // ========================================================
        // SHARED PLACEABLE LOADER
        // ========================================================

        private static async Task<PlaceableData>
            LoadPlaceable(
                string folder,
                string manifestPath,
                Dictionary<PlaceableData, string> modelPaths,
                Dictionary<PlaceableData, string> spritePaths)
        {
            try
            {
                string json =
                    await File.ReadAllTextAsync(
                        manifestPath);


                ModManifest manifest =
                    JsonUtility
                        .FromJson<ModManifest>(
                            json);


                if (manifest == null)
                {
                    MelonLogger.Warning(
                        $"[LocalWorkshop] Invalid manifest: " +
                        $"{manifestPath}");

                    return null;
                }


                if (string.IsNullOrWhiteSpace(
                        manifest.ID))
                {
                    MelonLogger.Warning(
                        $"[LocalWorkshop] Manifest missing ID: " +
                        $"{manifestPath}");

                    return null;
                }


                if (string.IsNullOrWhiteSpace(
                        manifest.DisplayName))
                {
                    manifest.DisplayName =
                        manifest.ID;
                }


                if (string.IsNullOrWhiteSpace(
                        manifest.ModelPath))
                {
                    MelonLogger.Warning(
                        $"[LocalWorkshop] {manifest.ID} " +
                        $"has no ModelPath.");

                    return null;
                }


                string modelPath =
                    Path.Combine(
                        folder,
                        manifest.ModelPath);


                if (!File.Exists(modelPath))
                {
                    MelonLogger.Warning(
                        $"[LocalWorkshop] Model missing for " +
                        $"{manifest.ID}: {modelPath}");

                    return null;
                }


                string spritePath = null;


                if (!string.IsNullOrWhiteSpace(
                        manifest.SpritePath))
                {
                    spritePath =
                        Path.Combine(
                            folder,
                            manifest.SpritePath);
                }


                PlaceableData data =
                    ScriptableObject
                        .CreateInstance<PlaceableData>();


                data.ID =
                    manifest.ID;


                data.DisplayName =
                    manifest.DisplayName;


                // Deliberate:
                // catalogue items ARE mod placeables internally.
                data.IsLoadedFromMod =
                    true;


                data.ModScale =
                    manifest.Scale > 0
                        ? manifest.Scale
                        : 1f;


                if (Enum.TryParse(
                    manifest.PlacementType,
                    true,
                    out PlacementType placement))
                {
                    data.PlacementType =
                        placement;
                }
                else
                {
                    MelonLogger.Warning(
                        $"[LocalWorkshop] Unknown PlacementType " +
                        $"'{manifest.PlacementType}' for " +
                        $"{manifest.ID}.");
                }


                if (Enum.TryParse(
                    manifest.ToolType,
                    true,
                    out EToolType tool))
                {
                    data.SetToolType(
                        tool);
                }
                else
                {
                    MelonLogger.Warning(
                        $"[LocalWorkshop] Unknown ToolType " +
                        $"'{manifest.ToolType}' for " +
                        $"{manifest.ID}.");
                }


                modelPaths[data] =
                    modelPath;


                if (!string.IsNullOrWhiteSpace(
                        spritePath))
                {
                    if (File.Exists(spritePath))
                    {
                        spritePaths[data] =
                            spritePath;
                    }
                    else
                    {
                        MelonLogger.Warning(
                            $"[LocalWorkshop] Sprite missing " +
                            $"for {manifest.ID}: " +
                            $"{spritePath}");
                    }
                }


                return data;
            }
            catch (Exception ex)
            {
                MelonLogger.Error(
                    $"[LocalWorkshop] Failed loading " +
                    $"{manifestPath}: {ex}");

                return null;
            }
        }


        // ========================================================
        // NAME HELPERS
        // ========================================================

        private static string PrettyCategoryName(
            string folderName)
        {
            // 01_Bathroom -> Bathroom
            // 02_Office   -> Office

            string value =
                Regex.Replace(
                    folderName,
                    @"^\d+[\s_\-]*",
                    "");


            return PrettyName(
                value);
        }


        private static string PrettyName(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;


            value =
                value
                    .Replace('_', ' ')
                    .Replace('-', ' ');


            // camelCase -> camel Case
            //
            // bedDouble -> bed Double
            // sideTableDrawers -> side Table Drawers
            value =
                Regex.Replace(
                    value,
                    @"([a-z0-9])([A-Z])",
                    "$1 $2");


            value =
                Regex.Replace(
                    value,
                    @"\s+",
                    " ")
                    .Trim();


            return value;
        }
    }


    // ============================================================
    // CATALOGUE SELECTOR INJECTION
    //
    // Vanilla:
    //
    // [Vanilla Books] [Mods]
    //
    // Becomes:
    //
    // [Vanilla Books] [Home Collection] [Mods]
    //
    // ============================================================

    [HarmonyPatch(
        typeof(InWorldCatalogueSelector),
        nameof(InWorldCatalogueSelector.InitializeCatalogues))]
    internal static class CatalogueSelectorPatch
    {
        static void Prefix(
            InWorldCatalogueSelector __instance,
            List<CatalogueData> catalogues)
        {
            if (!CatalogRegistry.HasCatalogues)
                return;


            MelonLogger.Msg(
                $"[Catalog] Selector received " +
                $"{catalogues.Count} existing catalogue(s).");


            // Build runtime ScriptableObjects using references
            // from BOXROOM's real catalogue data.
            CatalogRegistry
                .BuildRuntimeCatalogues(
                    catalogues);


            List<CatalogueData> customCatalogues =
                CatalogRegistry
                    .GetRuntimeCatalogues()
                    .ToList();


            if (customCatalogues.Count == 0)
            {
                MelonLogger.Warning(
                    "[Catalog] No runtime custom catalogues " +
                    "were successfully built.");

                return;
            }


            // ----------------------------------------------------
            // REMOVE EMPTY GENERIC MODS BOOK
            // ----------------------------------------------------

            if (!CatalogRegistry.HasNonCatalogMods())
            {
                int removed =
                    catalogues.RemoveAll(
                        x =>
                            x != null &&
                            x.IsModsCatalogue);


                if (removed > 0)
                {
                    MelonLogger.Msg(
                        "[Catalog] Removed generic Mods book " +
                        "because no non-catalog mods exist.");
                }
            }


            // ----------------------------------------------------
            // INSERT CUSTOM BOOKS BEFORE MODS
            // ----------------------------------------------------

            int modsIndex =
                catalogues.FindIndex(
                    x =>
                        x != null &&
                        x.IsModsCatalogue);


            if (modsIndex < 0)
            {
                modsIndex =
                    catalogues.Count;
            }


            foreach (CatalogueData custom
                     in customCatalogues)
            {
                if (catalogues.Contains(custom))
                    continue;


                catalogues.Insert(
                    modsIndex,
                    custom);


                MelonLogger.Msg(
                    $"[Catalog] Inserted runtime catalogue " +
                    $"'{custom.name}' at selector index " +
                    $"{modsIndex}.");


                modsIndex++;
            }


            // ----------------------------------------------------
            // PHYSICAL BOOK CAPACITY
            // ----------------------------------------------------

            EnsureBookCapacity(
                __instance,
                catalogues.Count);


            MelonLogger.Msg(
                $"[Catalog] Selector now contains " +
                $"{catalogues.Count} catalogue(s).");
        }


        private static void EnsureBookCapacity(
            InWorldCatalogueSelector selector,
            int requiredCount)
        {
            FieldInfo field =
                AccessTools.Field(
                    typeof(InWorldCatalogueSelector),
                    "catalogueObjects");


            if (field == null)
            {
                MelonLogger.Error(
                    "[Catalog] Could not find " +
                    "InWorldCatalogueSelector.catalogueObjects.");

                return;
            }


            SelectionCatalogue[] books =
                (SelectionCatalogue[])
                    field.GetValue(selector);


            if (books == null ||
                books.Length == 0)
            {
                MelonLogger.Error(
                    "[Catalog] Catalogue selector contains " +
                    "no physical book templates.");

                return;
            }


            MelonLogger.Msg(
                $"[Catalog] Physical book capacity: " +
                $"{books.Length}; required: {requiredCount}.");


            if (books.Length >= requiredCount)
                return;


            List<SelectionCatalogue> expanded =
                books.ToList();


            // Clone the final existing catalogue.
            SelectionCatalogue template =
                books[books.Length - 1];


            Transform parent =
                template.transform.parent;


            while (expanded.Count < requiredCount)
            {
                SelectionCatalogue clone =
                    UnityEngine.Object.Instantiate(
                        template,
                        parent);


                clone.name =
                    $"CustomCatalogueBook_{expanded.Count}";


                clone.SetActive(
                    false);


                expanded.Add(
                    clone);


                MelonLogger.Msg(
                    $"[Catalog] Created physical catalogue " +
                    $"book slot #{expanded.Count}.");
            }


            field.SetValue(
                selector,
                expanded.ToArray());
        }
    }


    // ============================================================
    // FILTER CATALOG ITEMS OUT OF GENERIC MODS CATALOGUE
    //
    // IMPORTANT:
    //
    // This does NOT remove them from PlaceableManager.
    //
    // They remain ordinary mod PlaceableData so BOXROOM's existing
    // placement/save/load pipeline keeps working.
    //
    // We only prevent duplicate presentation in the Mods book.
    // ============================================================

    [HarmonyPatch(
        typeof(CatalogueData),
        nameof(CatalogueData.GenerateModsPages))]
    internal static class ModsCatalogueFilterPatch
    {
        static bool Prefix(
            CatalogueData __instance,
            ref List<CataloguePageDefinition> __result)
        {
            if (!CatalogRegistry.HasCatalogues)
                return true;


            if (!__instance.IsModsCatalogue)
                return true;


            List<PlaceableData> allMods =
                Singleton<PlaceableManager>
                    .Instance
                    .GetLoadedModPlaceables();


            List<PlaceableData> filtered =
                allMods?
                    .Where(
                        x =>
                            x != null &&
                            !CatalogRegistry.IsCatalogItem(x))
                    .ToList()
                ??
                new List<PlaceableData>();


            List<CataloguePageDefinition> pages =
                new List<CataloguePageDefinition>();


            if (filtered.Count == 0 ||
                __instance.Pages == null ||
                __instance.Pages.Length == 0)
            {
                __result =
                    pages;

                return false;
            }


            CataloguePageDefinition template =
                __instance.Pages[0];


            int itemsPerPage =
                __instance.ModsItemsPerPage;


            if (itemsPerPage <= 0)
            {
                __result =
                    pages;

                return false;
            }


            int pageCount =
                Mathf.CeilToInt(
                    (float)filtered.Count /
                    itemsPerPage);


            for (int pageIndex = 0;
                 pageIndex < pageCount;
                 pageIndex++)
            {
                int start =
                    pageIndex * itemsPerPage;


                int count =
                    Mathf.Min(
                        itemsPerPage,
                        filtered.Count - start);


                Item[] items =
                    new Item[count];


                for (int i = 0;
                     i < count;
                     i++)
                {
                    items[i] =
                        filtered[start + i];
                }


                CataloguePageDefinition page =
                    ScriptableObject
                        .CreateInstance<
                            CataloguePageDefinition>();


                page.PageTemplate =
                    template.PageTemplate;


                page.PageName =
                    template.PageName;


                page.FillItems(
                    items);


                pages.Add(
                    page);
            }


            __result =
                pages;


            return false;
        }
    }
}