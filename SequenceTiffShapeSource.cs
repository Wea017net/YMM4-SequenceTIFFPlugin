using System.IO;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.WIC;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace SequenceTIFFPlugin;

internal sealed class SequenceTiffShapeSource : IShapeSource
{
    private readonly IGraphicsDevicesAndContext devices;
    private readonly SequenceTiffShapeParameter parameter;

    private string resolvedFirstFile = string.Empty;
    private IReadOnlyList<string> sequenceFiles = [];
    private int displayedIndex = -1;
    private ID2D1Bitmap? bitmap;
    private ID2D1CommandList? commandList;
    private bool disposed;

    public ID2D1Image Output
        => commandList
            ?? throw new InvalidOperationException("Updateを呼び出す前に描画結果を取得できません。");

    public SequenceTiffShapeSource(
        IGraphicsDevicesAndContext devices,
        SequenceTiffShapeParameter parameter)
    {
        this.devices = devices;
        this.parameter = parameter;
    }

    public void Update(TimelineItemSourceDescription timelineItemSourceDescription)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var firstFile = parameter.FirstFile ?? string.Empty;
        if (!string.Equals(firstFile, resolvedFirstFile, StringComparison.Ordinal))
        {
            sequenceFiles = SequenceFileResolver.Resolve(firstFile);
            resolvedFirstFile = firstFile;
            displayedIndex = -1;
        }

        var nextIndex = SequenceFrameMapper.GetSequenceIndex(
            timelineItemSourceDescription.ItemPosition.Frame,
            timelineItemSourceDescription.FPS,
            parameter.FrameRate,
            parameter.GetEffectivePlaybackStart(timelineItemSourceDescription, sequenceFiles.Count),
            sequenceFiles.Count);

        if (commandList is not null && nextIndex == displayedIndex)
            return;

        Render(nextIndex);
        // 読み込みに失敗したフレームは次回のUpdateで再試行します。
        displayedIndex = nextIndex < 0 || bitmap is not null ? nextIndex : int.MinValue;
    }

    private void Render(int index)
    {
        var dc = devices.DeviceContext;

        bitmap?.Dispose();
        bitmap = index >= 0 ? TryLoadBitmap(sequenceFiles[index]) : null;

        commandList?.Dispose();
        commandList = dc.CreateCommandList();

        dc.Target = commandList;
        dc.BeginDraw();

        try
        {
            dc.Clear(null);

            if (bitmap is not null)
            {
                var size = bitmap.Size;
                dc.DrawImage(
                    image: bitmap,
                    targetOffset: new Vector2(-size.Width / 2f, -size.Height / 2f),
                    interpolationMode: InterpolationMode.Linear,
                    compositeMode: CompositeMode.SourceOver);
            }
        }
        finally
        {
            try
            {
                dc.EndDraw();
            }
            finally
            {
                dc.Target = null;
            }

            commandList.Close();
        }
    }

    private ID2D1Bitmap? TryLoadBitmap(string filePath)
    {
        try
        {
            using var factory = new IWICImagingFactory();
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var wicStream = factory.CreateStream(stream);
            using var decoder = factory.CreateDecoderFromStream(wicStream);
            using var decodedFrame = decoder.GetFrame(0);
            using var converter = factory.CreateFormatConverter();

            converter.Initialize(
                decodedFrame,
                PixelFormat.Format32bppPBGRA,
                BitmapDitherType.None,
                null,
                0,
                BitmapPaletteType.Custom);

            return devices.DeviceContext.CreateBitmapFromWicBitmap(converter, null);
        }
        catch
        {
            // 壊れた画像や一時的に書き込み中の画像は透明として扱います。
            return null;
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;

        commandList?.Dispose();
        bitmap?.Dispose();
        commandList = null;
        bitmap = null;
        disposed = true;
    }
}
