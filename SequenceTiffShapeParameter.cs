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
    private int page = 1;
    private int playbackStart = 1;
    private int frameRate = 30;
    private bool isLooped;
    private int loopEndFrame;
    private int loopOriginFrame = 1;
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
        set
        {
            var normalizedValue = value ?? string.Empty;
            var wasEmpty = string.IsNullOrWhiteSpace(firstFile);
            if (Set(ref firstFile, normalizedValue))
                OnFileSelectionCommitted(wasEmpty);
        }
    }

    [Display(
        Name = nameof(Resource.PageName),
        Order = 1,
        Description = nameof(Resource.PageDescription),
        ResourceType = typeof(Resource))]
    [TextBoxSlider("F0", nameof(Resource.PageUnit), 1, 100, ResourceType = typeof(Resource))]
    [DefaultValue(1)]
    [Range(1, int.MaxValue)]
    public int Page
    {
        get => page;
        set => Set(ref page, Math.Max(1, value));
    }

    [Display(
        Name = nameof(Resource.PlaybackStartName),
        Order = 2,
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
            {
                LoopOriginFrame = playbackStart;
                UpdateOwnerContentOffset();
            }
        }
    }

    [Display(
        Name = nameof(Resource.FrameRateName),
        Order = 3,
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

    [Display(
        Name = nameof(Resource.LoopName),
        Order = 4,
        Description = nameof(Resource.LoopDescription),
        ResourceType = typeof(Resource))]
    [ToggleSlider]
    [DefaultValue(false)]
    public bool IsLooped
    {
        get => isLooped;
        set
        {
            if (!Set(ref isLooped, value))
                return;

            if (value && !synchronizingFromOwner)
                LoopOriginFrame = PlaybackStart;
            UpdateOwnerContentOffset();
        }
    }

    [Display(
        Name = nameof(Resource.LoopEndFrameName),
        Order = 5,
        Description = nameof(Resource.LoopEndFrameDescription),
        ResourceType = typeof(Resource))]
    [TextBoxSlider("F0", nameof(Resource.FrameUnit), 0, 1000, ResourceType = typeof(Resource))]
    [DefaultValue(0)]
    [Range(0, int.MaxValue)]
    public int LoopEndFrame
    {
        get => loopEndFrame;
        set
        {
            if (Set(ref loopEndFrame, Math.Max(0, value)))
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

    [Browsable(false)]
    public int LoopOriginFrame
    {
        get => loopOriginFrame;
        set => loopOriginFrame = Math.Max(1, value);
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
        Page = data.Page;
        PlaybackStart = data.PlaybackStart;
        FrameRate = data.FrameRate;
        IsLooped = data.IsLooped;
        LoopEndFrame = data.LoopEndFrame;
        LoopOriginFrame = data.LoopOriginFrame;
    }

    protected override void SaveSharedData(SharedDataStore store)
        => store.Save(new SharedData(
            FirstFile,
            Page,
            PlaybackStart,
            FrameRate,
            IsLooped,
            LoopEndFrame,
            LoopOriginFrame));

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
                GetContentEndFrame(fileCount),
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
            GetContentEndFrame(fileCount),
            FrameRate);

        if (IsLooped)
        {
            start = SequenceFrameMapper.NormalizeLoopFrame(
                start,
                fileCount,
                LoopOriginFrame,
                LoopEndFrame);
        }

        synchronizingFromOwner = true;
        try
        {
            SetWithoutUndoRedo(ref playbackStart, start, nameof(PlaybackStart));
            TimelineAnchorFrame = SequenceTiffItemCoordinator.GetFrame(owner);
            if (IsLooped)
            {
                SequenceTiffItemCoordinator.SetContentOffset(
                    owner,
                    SequenceTimelineMath.GetContentOffset(
                        start,
                        GetContentEndFrame(fileCount),
                        FrameRate));
            }
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
        var offset = SequenceTimelineMath.GetContentOffset(
            PlaybackStart,
            GetContentEndFrame(fileCount),
            FrameRate);

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

    private int GetContentEndFrame(int fileCount)
        => IsLooped
            ? SequenceFrameMapper.GetLoopEndFrame(fileCount, LoopOriginFrame, LoopEndFrame)
            : fileCount;

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
        public int Page { get; }
        public int PlaybackStart { get; }
        public int FrameRate { get; }
        public bool IsLooped { get; }
        public int LoopEndFrame { get; }
        public int LoopOriginFrame { get; }

        public SharedData(
            string firstFile,
            int page,
            int playbackStart,
            int frameRate,
            bool isLooped,
            int loopEndFrame,
            int loopOriginFrame)
        {
            FirstFile = firstFile;
            Page = page;
            PlaybackStart = playbackStart;
            FrameRate = frameRate;
            IsLooped = isLooped;
            LoopEndFrame = loopEndFrame;
            LoopOriginFrame = loopOriginFrame;
        }
    }
}
