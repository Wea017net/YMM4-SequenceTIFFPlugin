using System.Globalization;
using System.Resources;

namespace SequenceTIFFPlugin.Localization;

/// <summary>
/// YMM4から指定されたUIカルチャーで文字列を取得します。
/// </summary>
public static class Resource
{
    private static readonly ResourceManager Manager = new(
        "SequenceTIFFPlugin.Localization.Resource",
        typeof(Resource).Assembly);

    public static CultureInfo? Culture { get; set; }

    public static ResourceManager ResourceManager => Manager;

    public static string PluginName => Get(nameof(PluginName));
    public static string FirstFileName => Get(nameof(FirstFileName));
    public static string FirstFileDescription => Get(nameof(FirstFileDescription));
    public static string PlaybackStartName => Get(nameof(PlaybackStartName));
    public static string PlaybackStartDescription => Get(nameof(PlaybackStartDescription));
    public static string FrameRateName => Get(nameof(FrameRateName));
    public static string FrameRateDescription => Get(nameof(FrameRateDescription));
    public static string FrameUnit => Get(nameof(FrameUnit));
    public static string FpsUnit => Get(nameof(FpsUnit));
    public static string TiffFilterName => Get(nameof(TiffFilterName));

    private static string Get(string key)
        => Manager.GetString(key, Culture) ?? key;
}
