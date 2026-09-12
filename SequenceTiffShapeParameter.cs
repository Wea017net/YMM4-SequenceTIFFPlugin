using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using SequenceTIFFPlugin.Localization;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Settings;

namespace SequenceTIFFPlugin;

internal sealed class SequenceTiffShapeParameter : ShapeParameterBase
{
    private string firstFile = string.Empty;
    private int playbackStart = 1;
    private int frameRate = 30;
    private bool timelineMetadataInitialized;
    private int timelineAnchorFrame;
    private bool synchronizingFromOwner;
    private WeakReference<object>? ownerReference;
    private INotifyPropertyChanged? notifyingOwner;

    public SequenceTiffShapeParameter(SharedDataStore? sharedData)
        : base(sharedData)
    {
    }

    // プロジェクトファイルからの復元に必要です。
    public SequenceTiffShapeParameter()
        : this(null)
    {
    }

    [Display(
        Name = nameof(Resource.FirstFileName),
        Order = 0,
        Description = nameof(Resource.FirstFileDescription),
        ResourceType = typeof(Resource))]
    [SequenceTiffFileSelector]
    public string FirstFile
    {
        get => firstFile;
        set => Set(ref firstFile, value ?? string.Empty);
    }

    [Display(
        Name = nameof(Resource.PlaybackStartName),
        Order = 1,
        Description = nameof(Resource.PlaybackStartDescription),
        ResourceType = typeof(Resource))]
    [TextBoxSlider("F0", nameof(Resource.FrameUnit), 1, 1000, ResourceType = typeof(Resource))]
    [DefaultValue(1)]
    [Range(1, int.MaxValue)]
    public int PlaybackStart
    {
        get => playbackStart;
        set
        {
            if (Set(ref playbackStart, Math.Max(1, value)) && !synchronizingFromOwner)
                UpdateOwnerContentOffset();
        }
    }

    [Display(
        Name = nameof(Resource.FrameRateName),
        Order = 2,
        Description = nameof(Resource.FrameRateDescription),
        ResourceType = typeof(Resource))]
    [TextBoxSlider("F0", nameof(Resource.FpsUnit), 1, 120, ResourceType = typeof(Resource))]
    [DefaultValue(30)]
    [Range(1, 240)]
    public int FrameRate
    {
        get => frameRate;
        set
        {
            if (Set(ref frameRate, Math.Clamp(value, 1, 240)))
                UpdateOwnerContentOffset();
        }
    }

    // ShapeItemはContentLengthを公開しないため、素材終端と分割位置を同期するための内部状態。
    // publicプロパティにして、YMM4のプロジェクトJSONへ保存・複製されるようにします。
    [Browsable(false)]
    public bool TimelineMetadataInitialized
    {
        get => timelineMetadataInitialized;
        set => timelineMetadataInitialized = value;
    }

    [Browsable(false)]
    public int TimelineAnchorFrame
    {
        get => timelineAnchorFrame;
        set => timelineAnchorFrame = value;
    }

    public override IShapeSource CreateShapeSource(IGraphicsDevicesAndContext devices)
        => new SequenceTiffShapeSource(devices, this);

    public override IEnumerable<string> CreateMaskExoFilter(
        int keyFrameIndex,
        ExoOutputDescription desc,
        ShapeMaskExoOutputDescription shapeMaskDesc)
        => [];

    public override IEnumerable<string> CreateShapeItemExoFilter(
        int keyFrameIndex,
        ExoOutputDescription desc)
        => [];

    protected override IEnumerable<IAnimatable> GetAnimatables() => [];

    protected override void LoadSharedData(SharedDataStore store)
    {
        var data = store.Load<SharedData>();
        if (data is null)
            return;

        FirstFile = data.FirstFile;
        PlaybackStart = data.PlaybackStart;
        FrameRate = data.FrameRate;
    }

    protected override void SaveSharedData(SharedDataStore store)
        => store.Save(new SharedData(FirstFile, PlaybackStart, FrameRate));

    internal int CurrentTimelineFrameRate => videoFPS > 0 ? videoFPS : 30;

    internal void AttachOwner(object owner)
    {
        if (!SequenceTiffItemCoordinator.IsSupportedOwner(owner))
            return;

        if (ownerReference?.TryGetTarget(out var oldOwner) == true && ReferenceEquals(oldOwner, owner))
        {
            SynchronizeFromOwner();
            return;
        }

        if (notifyingOwner is not null)
            notifyingOwner.PropertyChanged -= OwnerPropertyChanged;

        ownerReference = new WeakReference<object>(owner);
        notifyingOwner = owner as INotifyPropertyChanged;
        if (notifyingOwner is not null)
            notifyingOwner.PropertyChanged += OwnerPropertyChanged;

        if (string.IsNullOrWhiteSpace(FirstFile))
        {
            TimelineAnchorFrame = SequenceTiffItemCoordinator.GetFrame(owner);
            return;
        }

        if (TimelineMetadataInitialized)
        {
            SynchronizeFromOwner();
        }
        else
        {
            // 旧バージョンで保存したアイテムには内部情報がないため、長さは変えずに初期化します。
            TimelineAnchorFrame = SequenceTiffItemCoordinator.GetFrame(owner);
            TimelineMetadataInitialized = true;
            UpdateOwnerContentOffset();
        }
    }

    internal void OnFileSelectionCommitted(bool wasEmpty)
    {
        if (!TryGetOwner(out var owner))
            return;

        if (string.IsNullOrWhiteSpace(FirstFile))
        {
            TimelineMetadataInitialized = false;
            TimelineAnchorFrame = SequenceTiffItemCoordinator.GetFrame(owner);
            SequenceTiffItemCoordinator.SetContentOffset(owner, TimeSpan.Zero);
            return;
        }

        var fileCount = SequenceFileResolver.Resolve(FirstFile).Count;
        if (fileCount <= 0)
            return;

        TimelineAnchorFrame = SequenceTiffItemCoordinator.GetFrame(owner);
        TimelineMetadataInitialized = true;
        UpdateOwnerContentOffset(fileCount);

        // 空欄から初めて素材を指定した時だけ、自動的に全素材分の長さへ合わせます。
        if (wasEmpty)
        {
            var length = SequenceTimelineMath.GetItemLength(
                fileCount,
                CurrentTimelineFrameRate,
                FrameRate);
            SequenceTiffItemCoordinator.SetLength(owner, length);
        }
    }

    internal int GetEffectivePlaybackStart(TimelineItemSourceDescription description, int fileCount)
    {
        if (TryGetOwner(out var owner) && TimelineMetadataInitialized)
        {
            return SequenceTimelineMath.GetPlaybackStart(
                SequenceTiffItemCoordinator.GetContentOffset(owner),
                fileCount,
                FrameRate);
        }

        if (!TimelineMetadataInitialized)
            return PlaybackStart;

        var itemTimelineFrame = description.TimelinePosition.Frame - description.ItemPosition.Frame;
        return SequenceTimelineMath.AdvancePlaybackStart(
            PlaybackStart,
            itemTimelineFrame - TimelineAnchorFrame,
            description.FPS,
            FrameRate);
    }

    private void OwnerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is null || synchronizingFromOwner)
            return;

        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "Frame")
            TimelineAnchorFrame = SequenceTiffItemCoordinator.GetFrame(sender);

        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is "ContentOffset" or "ContentSeparations")
            SynchronizeFromOwner();
    }

    private void SynchronizeFromOwner()
    {
        if (!TryGetOwner(out var owner) || !TimelineMetadataInitialized)
            return;

        var fileCount = SequenceFileResolver.Resolve(FirstFile).Count;
        if (fileCount <= 0)
            return;

        var start = SequenceTimelineMath.GetPlaybackStart(
            SequenceTiffItemCoordinator.GetContentOffset(owner),
            fileCount,
            FrameRate);

        synchronizingFromOwner = true;
        try
        {
            SetWithoutUndoRedo(ref playbackStart, start, nameof(PlaybackStart));
            TimelineAnchorFrame = SequenceTiffItemCoordinator.GetFrame(owner);
        }
        finally
        {
            synchronizingFromOwner = false;
        }
    }

    private void UpdateOwnerContentOffset(int? knownFileCount = null)
    {
        if (!TryGetOwner(out var owner) || string.IsNullOrWhiteSpace(FirstFile))
            return;

        var fileCount = knownFileCount ?? SequenceFileResolver.Resolve(FirstFile).Count;
        if (fileCount <= 0)
            return;

        TimelineMetadataInitialized = true;
        var offset = SequenceTimelineMath.GetContentOffset(PlaybackStart, fileCount, FrameRate);

        synchronizingFromOwner = true;
        try
        {
            SequenceTiffItemCoordinator.SetContentOffset(owner, offset);
        }
        finally
        {
            synchronizingFromOwner = false;
        }
    }

    private bool TryGetOwner(out object owner)
    {
        if (ownerReference?.TryGetTarget(out var target) == true)
        {
            owner = target;
            return true;
        }

        owner = null!;
        return false;
    }

    private sealed class SharedData
    {
        public string FirstFile { get; }
        public int PlaybackStart { get; }
        public int FrameRate { get; }

        public SharedData(string firstFile, int playbackStart, int frameRate)
        {
            FirstFile = firstFile;
            PlaybackStart = playbackStart;
            FrameRate = frameRate;
        }
    }
}
