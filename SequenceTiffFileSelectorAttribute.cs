using System.Windows;
using SequenceTIFFPlugin.Localization;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Settings;

namespace SequenceTIFFPlugin;

/// <summary>
/// 通常のファイル選択UIに、親の図形アイテムとの同期処理を追加します。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class SequenceTiffFileSelectorAttribute : FileSelectorAttribute
{
    public SequenceTiffFileSelectorAttribute()
        : base(FileGroupType.ImageItem)
    {
        CustomFilterName = nameof(Resource.TiffFilterName);
        CustomFilterValue = "*.tif;*.tiff";
        ResourceType = typeof(Resource);
    }

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        foreach (var property in itemProperties.Where(x => x.PropertyOwner is SequenceTiffShapeParameter))
            ((SequenceTiffShapeParameter)property.PropertyOwner).AttachOwner(property.Item);

        base.SetBindings(control, itemProperties);
    }
}
