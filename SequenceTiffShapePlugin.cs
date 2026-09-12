using System.Globalization;
using SequenceTIFFPlugin.Localization;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace SequenceTIFFPlugin;

/// <summary>
/// YMM4の「図形」に連番TIFF画像を追加します。
/// </summary>
public sealed class SequenceTiffShapePlugin : IShapePlugin, ILocalizePlugin
{
    public string Name => Resource.PluginName;

    // 連番画像はAviUtl標準図形へ変換できないため、exo出力は非対応です。
    public bool IsExoShapeSupported => false;

    public bool IsExoMaskSupported => false;

    public IShapeParameter CreateShapeParameter(SharedDataStore? sharedData)
        => new SequenceTiffShapeParameter(sharedData);

    public void SetCulture(CultureInfo cultureInfo)
        => Resource.Culture = cultureInfo;
}
