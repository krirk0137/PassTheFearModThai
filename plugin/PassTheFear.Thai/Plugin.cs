using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using GameFramework.Localization;
using HarmonyLib;
using Wogame;

namespace PassTheFear.Thai;

// ---------------------------------------------------------------------------------------
//  Pass the Fear — Thai
//
//  Every game string is fetched by key through GameFramework's LocalizationManager, which
//  keeps a plain Dictionary<string,string> and exposes AddRawString(key, value) publicly.
//  Wogame.XmlLocalizationHelper.ParseData is what fills that dictionary, once per XML
//  dictionary asset the game loads.
//
//  So we postfix ParseData: the game writes all of its own strings first, then we overwrite
//  with Thai. That is deterministic, costs nothing per lookup (unlike hooking GetString,
//  which fires many times per frame), and touches no game file.
//
//  Language.Thai already exists in GameFramework's enum (= 47), so Thai can eventually be a
//  real entry in the in-game picker rather than a stolen language slot.
// ---------------------------------------------------------------------------------------
[BepInPlugin(Guid, Name, Version)]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "com.krirk0137.passthefear.thai";
    public const string Name = "Pass the Fear — Thai";
    public const string Version = "0.1.0";

    internal static ManualLogSource Logger;
    internal static ThaiTable Table;

    // Plain statics. Never read config through the plugin instance from a Harmony patch —
    // patches can run before or after the instance is in a usable state.
    internal static bool OptEnabled = true;

    public override void Load()
    {
        Logger = Log;

        OptEnabled = Config.Bind("General", "Enabled", true,
            "Master switch. False = the plugin loads but injects nothing.").Value;
        var file = Config.Bind("General", "File", "Thai.tsv",
            "Translation table, relative to this plugin's folder. Format: key<TAB>value, UTF-8, "
            + "'#' comments. Escapes in the value: \\n \\t \\\\ .").Value;

        var dir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
        Table = ThaiTable.Load(Path.Combine(dir!, file));
        Logger.LogInfo($"Loaded {Table.Count} Thai strings.");

        try
        {
            new Harmony(Guid).PatchAll(typeof(LocalizationPatches));
            Logger.LogInfo("Harmony patch on XmlLocalizationHelper.ParseData installed.");
        }
        catch (Exception e)
        {
            Logger.LogError($"Harmony patch FAILED: {e}");
        }
    }
}

[HarmonyPatch]
internal static class LocalizationPatches
{
    private static int _applies;

    /// <summary>
    /// Runs after the game has parsed one of its own XML dictionaries. Overwriting here means
    /// our value is what any later lookup sees, and it survives the game reloading dictionaries
    /// on a language change because this fires again each time.
    /// </summary>
    [HarmonyPatch(typeof(XmlLocalizationHelper), nameof(XmlLocalizationHelper.ParseData))]
    [HarmonyPostfix]
    private static void AfterParseData(ILocalizationManager localizationManager, bool __result)
    {
        if (!Plugin.OptEnabled || !__result || localizationManager == null) return;

        var table = Plugin.Table;
        if (table.Count == 0) return;

        // GameFramework's AddRawString refuses to overwrite an existing key, so a plain Add
        // would only ever win the passes where the game had not yet parsed that key's own
        // dictionary. Remove first to make the write unconditional.
        int written = 0;
        foreach (var pair in table.Entries)
        {
            localizationManager.RemoveRawString(pair.Key);
            if (localizationManager.AddRawString(pair.Key, pair.Value)) written++;
        }

        // Only report the first couple of passes; ParseData runs once per dictionary asset and
        // again on every language change, so logging every time would flood the file.
        if (++_applies <= 2)
        {
            Plugin.Logger.LogInfo(
                $"Applied {written}/{table.Count} Thai strings (pass {_applies}).");

            // Read straight back out of the game's own table. This is the only proof that
            // matters — if the game overwrites us later, this is where it shows.
            foreach (var key in new[] { "GameUI.Login.1", "GameUI.Setting.Rule.1" })
            {
                Plugin.Logger.LogInfo($"  readback {key} = \"{localizationManager.GetRawString(key)}\"");
            }
        }
    }
}
