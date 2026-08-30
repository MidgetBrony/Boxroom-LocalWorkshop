using MelonLoader;
using SteamShelf.Placeables;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SteamShelf.Items;

namespace LocalWorkshop
{
    public static class LocalWorkshopApi
    {
        private sealed class Registration
        {
            public string Id, DisplayName, Author, Version;
            public Texture2D Cover;
            public int ItemsPerPage;
            public readonly List<CategoryRegistration> Categories = new List<CategoryRegistration>();
        }

        private sealed class CategoryRegistration
        {
            public string DisplayName;
            public readonly List<PlaceableData> Items = new List<PlaceableData>();
        }

        private static readonly Dictionary<string, Registration> Registrations =
            new Dictionary<string, Registration>(StringComparer.OrdinalIgnoreCase);

        public static void RegisterCatalogue(string id, string displayName, Texture2D cover,
            IReadOnlyDictionary<string, IEnumerable<PlaceableData>> categories,
            string author = null, string version = null, int itemsPerPage = 0)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Catalogue ID is required.", nameof(id));
            if (categories == null) throw new ArgumentNullException(nameof(categories));
            var registration = new Registration
            {
                Id = id, DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName,
                Author = author, Version = version, Cover = cover, ItemsPerPage = itemsPerPage
            };
            foreach (var pair in categories)
            {
                var category = new CategoryRegistration { DisplayName = string.IsNullOrWhiteSpace(pair.Key) ? "Items" : pair.Key };
                if (pair.Value != null) category.Items.AddRange(pair.Value.Where(x => x != null));
                if (category.Items.Count > 0) registration.Categories.Add(category);
            }
            if (registration.Categories.Count == 0) throw new ArgumentException("At least one non-empty category is required.", nameof(categories));
            Registrations[id] = registration;
            MelonLogger.Msg($"[LocalWorkshop API] Registered catalogue provider '{registration.DisplayName}'.");
        }

        public static bool IsCustomCatalogue(CatalogueData data) =>
            data != null && CatalogRegistry.Catalogs.Any(x => x.RuntimeData == data);

        public static string GetCatalogueDisplayName(CatalogueData data) =>
            CatalogRegistry.Catalogs.FirstOrDefault(x => x.RuntimeData == data)?.Manifest?.DisplayName
            ?? "COLLECTION";

        internal static void ApplyRegisteredCatalogues()
        {
            foreach (Registration registration in Registrations.Values)
            {
                if (CatalogRegistry.Catalogs.Any(x => string.Equals(x.Manifest?.ID, registration.Id, StringComparison.OrdinalIgnoreCase))) continue;
                var catalog = new LoadedCatalog
                {
                    Manifest = new CatalogManifest
                    {
                        ID = registration.Id, DisplayName = registration.DisplayName, Author = registration.Author,
                        Version = registration.Version, ItemsPerPage = registration.ItemsPerPage
                    },
                    CoverTexture = registration.Cover,
                    OwnsCoverTexture = false
                };
                foreach (CategoryRegistration source in registration.Categories)
                {
                    var category = new LoadedCategory { DisplayName = source.DisplayName };
                    foreach (PlaceableData item in source.Items)
                    {
                        if (item == null || string.IsNullOrWhiteSpace(item.ID) || !CatalogRegistry.CatalogItemIDs.Add(item.ID)) continue;
                        category.Items.Add(item);
                    }
                    if (category.Items.Count > 0) catalog.Categories.Add(category);
                }
                if (catalog.Categories.Count == 0) continue;
                CatalogRegistry.Catalogs.Add(catalog);
                MelonLogger.Msg($"[LocalWorkshop API] Applied '{registration.DisplayName}' with {catalog.Categories.Sum(x => x.Items.Count)} item(s).");
            }
        }
    }
}
