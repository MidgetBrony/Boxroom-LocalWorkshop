using Catalogue;
using HarmonyLib;
using MelonLoader;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LocalWorkshop
{
    internal static class CatalogueScrollState
    {
        internal sealed class State
        {
            public int Count;
            public int ScrollIndex;
            public int LastSelected = -2;
        }

        internal const int VisibleBooks = 7;
        internal static readonly Dictionary<int, State> States = new Dictionary<int, State>();
    }

    [HarmonyPatch(typeof(InWorldCatalogueSelector), nameof(InWorldCatalogueSelector.InitializeCatalogues))]
    internal static class CatalogueScrollInitializePatch
    {
        private static void Postfix(InWorldCatalogueSelector __instance, List<SteamShelf.Items.CatalogueData> catalogues)
        {
            CatalogueScrollState.States[__instance.GetInstanceID()] = new CatalogueScrollState.State
            {
                Count = catalogues?.Count ?? 0
            };
            Apply(__instance, 0);
        }

        internal static void Apply(InWorldCatalogueSelector selector, int selectedIndex)
        {
            if (!CatalogueScrollState.States.TryGetValue(selector.GetInstanceID(), out var state)) return;
            int maxScroll = Mathf.Max(0, state.Count - CatalogueScrollState.VisibleBooks);
            state.ScrollIndex = Mathf.Clamp(state.ScrollIndex, 0, maxScroll);

            var baseField = AccessTools.Field(typeof(InWorldCatalogueSelector), "baseLocalPositions");
            var targetField = AccessTools.Field(typeof(InWorldCatalogueSelector), "targetLocalPositions");
            var peekField = AccessTools.Field(typeof(InWorldCatalogueSelector), "peekSpacing");
            var depthField = AccessTools.Field(typeof(InWorldCatalogueSelector), "stackDepthStep");
            var basePositions = baseField?.GetValue(selector) as Vector3[];
            var targetPositions = targetField?.GetValue(selector) as Vector3[];
            if (basePositions == null || targetPositions == null) return;
            float peek = peekField != null ? (float)peekField.GetValue(selector) : 0.4f;
            float depth = depthField != null ? (float)depthField.GetValue(selector) : 0.01f;
            for (int i = 0; i < basePositions.Length; i++)
            {
                Vector3 position = new Vector3((i - state.ScrollIndex) * peek, -i * depth, 0f);
                basePositions[i] = position;
                targetPositions[i] = position;
            }
            AccessTools.Method(typeof(InWorldCatalogueSelector), "ApplyStackTargets")?.Invoke(selector, new object[] { selectedIndex });
        }
    }

    [HarmonyPatch(typeof(InWorldCatalogueSelector), "Update")]
    internal static class CatalogueScrollUpdatePatch
    {
        private static void Prefix(InWorldCatalogueSelector __instance)
        {
            if (!CatalogueScrollState.States.TryGetValue(__instance.GetInstanceID(), out var state)) return;
            var selectedField = AccessTools.Field(typeof(InWorldCatalogueSelector), "selectedIndex");
            int selected = selectedField != null ? (int)selectedField.GetValue(__instance) : -1;
            int oldScroll = state.ScrollIndex;

            if (Mouse.current != null)
            {
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (wheel < -0.01f) state.ScrollIndex++;
                else if (wheel > 0.01f) state.ScrollIndex--;
            }

            if (selected >= 0 && selected != state.LastSelected)
            {
                if (selected < state.ScrollIndex) state.ScrollIndex = selected;
                else if (selected >= state.ScrollIndex + CatalogueScrollState.VisibleBooks)
                    state.ScrollIndex = selected - CatalogueScrollState.VisibleBooks + 1;
            }

            int maxScroll = Mathf.Max(0, state.Count - CatalogueScrollState.VisibleBooks);
            state.ScrollIndex = Mathf.Clamp(state.ScrollIndex, 0, maxScroll);
            if (state.ScrollIndex != oldScroll || selected != state.LastSelected)
                CatalogueScrollInitializePatch.Apply(__instance, selected);
            state.LastSelected = selected;
        }
    }

    [HarmonyPatch(typeof(InWorldCatalogueSelector), nameof(InWorldCatalogueSelector.HideCatalogues))]
    internal static class CatalogueScrollClosePatch
    {
        private static void Postfix(InWorldCatalogueSelector __instance) =>
            CatalogueScrollState.States.Remove(__instance.GetInstanceID());
    }
}
