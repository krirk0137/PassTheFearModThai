using TMPro;
using UnityEngine;

namespace PassTheFear.Thai;

/// <summary>
/// The game ships one TMP font asset per language and none of them contain Thai glyphs, so
/// injected Thai renders as tofu. We load a Thai TMP_FontAsset out of an AssetBundle and
/// attach it as a FALLBACK — never as a replacement, because replacing would strip CJK out
/// of any mixed string and this game is full of them.
///
/// Building the font asset at runtime from a Windows font is NOT an option here:
/// Font.CreateDynamicFontFromOSFont was stripped from this IL2CPP build (the game never
/// calls it) and Il2CppInterop cannot unstrip it — "Method unstripping failed". Shipping a
/// bundle sidesteps that entirely, since AssetBundle.LoadFromFile is code GameFramework
/// itself relies on and is therefore always present.
/// </summary>
internal static class Fonts
{
    private static TMP_FontAsset _thai;
    private static bool _tried;
    private static int _attached;

    internal static bool HasThai(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        foreach (var c in s)
        {
            if (c >= '฀' && c <= '๿') return true;
        }
        return false;
    }

    /// <summary>Load the Thai font asset once. Null means the bundle was missing or unusable.</summary>
    private static TMP_FontAsset ThaiFont()
    {
        if (_tried) return _thai;
        _tried = true;

        var dir = System.IO.Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
        var path = System.IO.Path.Combine(dir!, Plugin.OptFontBundle);

        if (!System.IO.File.Exists(path))
        {
            Plugin.Logger.LogError($"Font bundle not found at {path}. Thai will render as boxes.");
            return null;
        }

        try
        {
            var bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
            {
                Plugin.Logger.LogError(
                    $"'{Plugin.OptFontBundle}' could not be opened as an AssetBundle. It may have been "
                    + "built for a Unity version this game cannot read.");
                return null;
            }

            // The generic LoadAllAssets<T> is a likely stripping casualty; the plain overload
            // is what the game itself uses, so it is always there.
            var wanted = Plugin.OptFontAssetName;
            foreach (var obj in bundle.LoadAllAssets())
            {
                var font = obj.TryCast<TMP_FontAsset>();
                if (font == null) continue;

                Plugin.Logger.LogInfo($"  bundle contains TMP font asset '{font.name}'");
                if (_thai == null && font.name.IndexOf(wanted, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _thai = font;
                }
            }

            if (_thai == null)
            {
                Plugin.Logger.LogError($"No TMP font asset matching '{wanted}' inside {Plugin.OptFontBundle}.");
                return null;
            }

            UnityEngine.Object.DontDestroyOnLoad(_thai);
            Plugin.Logger.LogInfo($"Using '{_thai.name}' as the Thai fallback font.");
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogError($"Loading the Thai font bundle failed: {e}");
        }

        return _thai;
    }

    /// <summary>
    /// Attach the Thai font to every TMP face currently displaying Thai. Attaching per font
    /// asset rather than per component means one attach fixes every label sharing that face.
    /// </summary>
    internal static void Sweep()
    {
        var thai = ThaiFont();
        if (thai == null) return;

        foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (text == null || text.font == null) continue;
            if (text.font.Pointer == thai.Pointer) continue;
            if (!HasThai(text.text)) continue;

            var fallbacks = text.font.fallbackFontAssetTable;
            if (fallbacks == null)
            {
                fallbacks = new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();
                text.font.fallbackFontAssetTable = fallbacks;
            }

            bool present = false;
            for (int i = 0; i < fallbacks.Count; i++)
            {
                if (fallbacks[i] != null && fallbacks[i].Pointer == thai.Pointer) { present = true; break; }
            }

            if (!present)
            {
                fallbacks.Add(thai);
                _attached++;
                if (_attached <= 8)
                {
                    Plugin.Logger.LogInfo($"Attached Thai fallback to font asset '{text.font.name}'.");
                }
            }

            try { text.ForceMeshUpdate(true, true); } catch { }
        }
    }
}
