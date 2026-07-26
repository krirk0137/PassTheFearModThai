using GameFramework.Localization;
using HarmonyLib;
using UnityGameFramework.Runtime;
using Wogame;

namespace PassTheFear.Thai;

// ---------------------------------------------------------------------------------------
//  Making Thai a real entry in Settings -> Language.
//
//  GameFramework.Localization.Language.Thai (= 47) already exists in the game's own enum, so
//  Thai does not have to displace Spanish or Russian. Two things are needed:
//
//    1. put it in the picker  — SettingForm keeps _languageList / _languageDescList
//    2. give it something to load — the game builds dictionary paths as
//         Assets/GameMain/GMResources/Localization/{language}/Dictionaries/{name}.xml
//       and there is no Thai folder in the shipped bundle, so a Thai selection would fail to
//       load anything at all. Redirecting the request to the English folder gives the game a
//       complete table to work from, which LocalizationPatches then overwrites with Thai.
//
//  English is the base rather than Chinese because any key we have not translated yet falls
//  through to it, and English is the better fallback for a Thai player.
// ---------------------------------------------------------------------------------------
[HarmonyPatch]
internal static class LanguagePatches
{
    private const string ThaiSegment = "/Thai/";
    private static bool _announced;

    private static void Redirect(ref string dictionaryAssetName)
    {
        if (!Plugin.OptAddToPicker || dictionaryAssetName == null) return;
        if (dictionaryAssetName.IndexOf(ThaiSegment, System.StringComparison.Ordinal) < 0) return;

        var redirected = dictionaryAssetName.Replace(ThaiSegment, $"/{Plugin.OptBaseLanguage}/");
        if (!_announced)
        {
            _announced = true;
            Plugin.Logger.LogInfo(
                $"Thai is selected, so dictionary loads are being served from '{Plugin.OptBaseLanguage}' "
                + $"and overwritten with Thai. First: {dictionaryAssetName} -> {redirected}");
        }
        dictionaryAssetName = redirected;
    }

    [HarmonyPatch(typeof(LocalizationComponent), nameof(LocalizationComponent.ReadData), typeof(string))]
    [HarmonyPrefix]
    private static void ReadData1(ref string dictionaryAssetName) => Redirect(ref dictionaryAssetName);

    [HarmonyPatch(typeof(LocalizationComponent), nameof(LocalizationComponent.ReadData), typeof(string), typeof(int))]
    [HarmonyPrefix]
    private static void ReadData2(ref string dictionaryAssetName) => Redirect(ref dictionaryAssetName);

    [HarmonyPatch(typeof(LocalizationComponent), nameof(LocalizationComponent.ReadData), typeof(string), typeof(Il2CppSystem.Object))]
    [HarmonyPrefix]
    private static void ReadData3(ref string dictionaryAssetName) => Redirect(ref dictionaryAssetName);

    [HarmonyPatch(typeof(LocalizationComponent), nameof(LocalizationComponent.ReadData), typeof(string), typeof(int), typeof(Il2CppSystem.Object))]
    [HarmonyPrefix]
    private static void ReadData4(ref string dictionaryAssetName) => Redirect(ref dictionaryAssetName);

    /// <summary>
    /// CheckLanguageBtn refreshes the picker's display, so by the time it runs the lists the
    /// game builds asynchronously (SettingForm.LoadLanguage is a coroutine) are populated.
    /// Appending here rather than once at startup also survives the form rebuilding them.
    /// </summary>
    [HarmonyPatch(typeof(SettingForm), "CheckLanguageBtn")]
    [HarmonyPostfix]
    private static void AfterCheckLanguageBtn(SettingForm __instance)
    {
        if (!Plugin.OptAddToPicker) return;

        try
        {
            var languages = __instance._languageList;
            var labels = __instance._languageDescList;
            if (languages == null || labels == null) return;

            for (int i = 0; i < languages.Count; i++)
            {
                if (languages[i] == Language.Thai) return;   // already there
            }

            // The two lists are index-paired; refuse to touch them if the game has them in a
            // state we do not understand, rather than corrupt the picker.
            if (languages.Count != labels.Count)
            {
                Plugin.Logger.LogWarning(
                    $"Language picker lists are out of step ({languages.Count} vs {labels.Count}); "
                    + "not adding Thai this time.");
                return;
            }

            languages.Add(Language.Thai);
            labels.Add(Plugin.OptLanguageLabel);
            Plugin.Logger.LogInfo(
                $"Added '{Plugin.OptLanguageLabel}' to the language picker as entry {languages.Count}.");
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogError($"Adding Thai to the picker failed: {e.Message}");
        }
    }
}
