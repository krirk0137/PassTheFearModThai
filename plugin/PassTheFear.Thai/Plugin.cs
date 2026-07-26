using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using GameFramework.Localization;
using HarmonyLib;
using UnityEngine;
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
    internal static bool OptAddToPicker = true;
    internal static string OptLanguageLabel = "ไทย";
    internal static string OptBaseLanguage = "English";
    internal static string OptFontBundle = "Kanit_sdf";
    internal static string OptFontAssetName = "Kanit";
    internal static float OptSweepSeconds = 1f;

    public override void Load()
    {
        Logger = Log;

        OptEnabled = Config.Bind("General", "Enabled", true,
            "Master switch. False = the plugin loads but injects nothing.").Value;
        var file = Config.Bind("General", "File", "Thai.tsv",
            "Translation table, relative to this plugin's folder. Format: key<TAB>value, UTF-8, "
            + "'#' comments. Escapes in the value: \\n \\t \\\\ .").Value;

        OptAddToPicker = Config.Bind("Language", "AddToPicker", true,
            "Add Thai as a selectable language in Settings. False = Thai is simply overlaid on "
            + "whatever language is selected, which is simpler and cannot break the picker.").Value;
        OptLanguageLabel = Config.Bind("Language", "Label", OptLanguageLabel,
            "How Thai is labelled in the picker.").Value;
        OptBaseLanguage = Config.Bind("Language", "BaseLanguage", OptBaseLanguage,
            "Which shipped language's dictionaries are loaded underneath Thai. Untranslated keys "
            + "fall through to this, so it is also the fallback language.").Value;

        OptFontBundle = Config.Bind("Font", "Bundle", OptFontBundle,
            "AssetBundle in this plugin's folder holding the Thai TMP font asset. Building one from "
            + "a Windows font at runtime is impossible here: Font.CreateDynamicFontFromOSFont was "
            + "stripped from this IL2CPP build and cannot be unstripped.").Value;
        OptFontAssetName = Config.Bind("Font", "AssetName", OptFontAssetName,
            "Name fragment of the TMP font asset to use from that bundle. Match on a fragment, not "
            + "the filename: 'Kanit_sdf' contains an asset actually named 'Kanit-Regular SDF'.").Value;
        OptSweepSeconds = Config.Bind("Font", "SweepSeconds", 1f,
            "How often to re-scan for TMP labels showing Thai that still need the fallback.").Value;

        var dir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
        Table = ThaiTable.Load(Path.Combine(dir!, file));
        Logger.LogInfo($"Loaded {Table.Count} Thai strings.");

        try
        {
            var harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(LocalizationPatches));
            harmony.PatchAll(typeof(LanguagePatches));
            Logger.LogInfo("Harmony patches installed (ParseData, ReadData, CheckLanguageBtn).");
        }
        catch (Exception e)
        {
            Logger.LogError($"Harmony patch FAILED: {e}");
        }

        // The font work needs a frame loop: TMP components appear as scenes load, and each one
        // needs the Thai fallback attached to whatever face it uses.
        AddComponent<Ticker>();
    }
}

/// <summary>Our own always-on object, so nothing depends on the plugin instance's lifecycle.</summary>
internal sealed class Ticker : MonoBehaviour
{
    public Ticker(IntPtr ptr) : base(ptr) { }

    private float _nextSweep;

    private void Update()
    {
        if (Plugin.OptSweepSeconds <= 0f || Time.unscaledTime < _nextSweep) return;
        _nextSweep = Time.unscaledTime + Plugin.OptSweepSeconds;

        try { Fonts.Sweep(); }
        catch (Exception e) { Plugin.Logger.LogWarning($"Font sweep failed: {e.Message}"); }
    }
}

[HarmonyPatch]
internal static class LocalizationPatches
{
    private static int _applies;
    private static int _translated;
    private static readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private static long _nextReportMs;

    /// <summary>
    /// Runs after the game has parsed one of its own XML dictionaries. Overwriting here means
    /// our value is what any later lookup sees, and it survives the game reloading dictionaries
    /// on a language change because this fires again each time.
    /// </summary>
    [HarmonyPatch(typeof(XmlLocalizationHelper), nameof(XmlLocalizationHelper.ParseData))]
    [HarmonyPostfix]
    private static void AfterParseData(ILocalizationManager localizationManager, string dictionaryString,
                                       bool __result)
    {
        if (!Plugin.OptEnabled || !__result || localizationManager == null) return;

        var table = Plugin.Table;
        if (table.Count == 0) return;

        // Translate exactly the keys this dictionary just contributed, and nothing else.
        //
        // dictionaryString is the raw XML the game has just finished parsing, so its Key=""
        // attributes are precisely the strings that entered the table on this call. Walking it
        // is O(size of that one dictionary) no matter how large our table grows, and it never
        // touches a key whose own dictionary has not loaded.
        //
        // That second property is what matters. The first version injected the whole table on
        // every call, which introduced keys the game had not parsed yet and inflated
        // DictionaryCount. The game's language load is a coroutine (SettingForm.LoadLanguage /
        // LoadLanguageWait, with a languageLoadCount field) that waits on load progress, and it
        // never finished: black screen, CPU spinning, RAM frozen.
        //
        // AddRawString refuses to overwrite an existing key, hence the Remove before it.
        int written = 0, seen = 0;
        foreach (var key in KeysIn(dictionaryString))
        {
            seen++;
            if (!table.TryGet(key, out var thai)) continue;
            localizationManager.RemoveRawString(key);
            if (localizationManager.AddRawString(key, thai)) written++;
        }
        _translated += written;

        _applies++;

        // Report every pass that actually did something. Silent passes are normal — most
        // dictionaries hold no key we have translated yet — but a pass that translates must
        // be visible, or a working injection looks identical to a broken one.
        if (written > 0)
        {
            Plugin.Logger.LogInfo(
                $"Translated {written} of the {seen} keys in this dictionary "
                + $"({_translated}/{table.Count} of the table done, pass {_applies}).");
        }

        // ParseData should fire roughly once per dictionary asset. If this keeps climbing the
        // game is re-parsing in a loop, which is worth seeing before it becomes a hang.
        if (_clock.ElapsedMilliseconds >= _nextReportMs)
        {
            _nextReportMs = _clock.ElapsedMilliseconds + 5000;
            Plugin.Logger.LogInfo($"[rate] ParseData postfix has run {_applies} times "
                                  + $"in {_clock.ElapsedMilliseconds / 1000}s.");
        }
    }

    /// <summary>Yield every Key="..." attribute value in a localization dictionary XML.</summary>
    private static IEnumerable<string> KeysIn(string xml)
    {
        if (string.IsNullOrEmpty(xml)) yield break;

        const string marker = "Key=\"";
        int i = 0;
        while (true)
        {
            int start = xml.IndexOf(marker, i, StringComparison.Ordinal);
            if (start < 0) yield break;
            start += marker.Length;

            int end = xml.IndexOf('"', start);
            if (end < 0) yield break;

            yield return xml.Substring(start, end - start);
            i = end + 1;
        }
    }
}
