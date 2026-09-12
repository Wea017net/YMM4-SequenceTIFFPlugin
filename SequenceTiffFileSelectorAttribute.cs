using System.Runtime.CompilerServices;
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
    private readonly ConditionalWeakTable<FileSelector, BindingState> states = new();

    public SequenceTiffFileSelectorAttribute()
        : base(FileGroupType.ImageItem)
    {
        CustomFilterName = nameof(Resource.TiffFilterName);
        CustomFilterValue = "*.tif;*.tiff";
        ResourceType = typeof(Resource);
    }

    public override FrameworkElement Create()
    {
        var selector = (FileSelector)base.Create();
        states.Add(selector, new BindingState());
        selector.BeginEdit += SelectorBeginEdit;
        selector.EndEdit += SelectorEndEdit;
        return selector;
    }

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        var selector = (FileSelector)control;
        var state = states.GetOrCreateValue(selector);
        state.Properties = itemProperties
            .Where(x => x.PropertyOwner is SequenceTiffShapeParameter)
            .ToArray();

        foreach (var property in state.Properties)
            ((SequenceTiffShapeParameter)property.PropertyOwner).AttachOwner(property.Item);

        state.CaptureOldValues();
        base.SetBindings(control, itemProperties);
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is FileSelector selector && states.TryGetValue(selector, out var state))
        {
            state.Properties = [];
            state.OldValues = [];
        }

        base.ClearBindings(control);
    }

    private void SelectorBeginEdit(object? sender, EventArgs e)
    {
        if (sender is FileSelector selector && states.TryGetValue(selector, out var state))
            state.CaptureOldValues();
    }

    private void SelectorEndEdit(object? sender, EventArgs e)
    {
        if (sender is not FileSelector selector || !states.TryGetValue(selector, out var state))
            return;

        for (var i = 0; i < state.Properties.Length; i++)
        {
            var parameter = (SequenceTiffShapeParameter)state.Properties[i].PropertyOwner;
            var oldValue = i < state.OldValues.Length ? state.OldValues[i] : string.Empty;
            parameter.OnFileSelectionCommitted(string.IsNullOrWhiteSpace(oldValue));
        }

        state.CaptureOldValues();
    }

    private sealed class BindingState
    {
        public ItemProperty[] Properties { get; set; } = [];

        public string[] OldValues { get; set; } = [];

        public void CaptureOldValues()
            => OldValues = Properties
                .Select(x => ((SequenceTiffShapeParameter)x.PropertyOwner).FirstFile)
                .ToArray();
    }
}
