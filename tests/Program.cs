using SequenceTIFFPlugin;
using System.IO;
using System.Xml.Linq;

var failures = new List<string>();

Run("数値順・選択番号から開始", () =>
{
    using var directory = new TemporaryDirectory();
    directory.Create("render_0001.tif");
    var first = directory.Create("render_0002.tif");
    directory.Create("render_0010.tiff");
    directory.Create("render_0003.png");
    directory.Create("other_0003.tif");

    var result = SequenceFileResolver.Resolve(first);

    Equal(2, result.Count);
    Equal("render_0002.tif", Path.GetFileName(result[0]));
    Equal("render_0010.tiff", Path.GetFileName(result[1]));
});

Run("末尾番号なしは単一画像", () =>
{
    using var directory = new TemporaryDirectory();
    var first = directory.Create("background.tif");
    directory.Create("background2.tif");

    var result = SequenceFileResolver.Resolve(first);

    Equal(1, result.Count);
    Equal(Path.GetFullPath(first), result[0]);
});

Run("存在しないファイル・非TIFFは空", () =>
{
    using var directory = new TemporaryDirectory();
    Equal(0, SequenceFileResolver.Resolve(Path.Combine(directory.Path, "missing.tif")).Count);
    Equal(0, SequenceFileResolver.Resolve(directory.Create("image_001.png")).Count);
});

Run("タイムラインfpsから連番fpsへ換算", () =>
{
    Equal(0, SequenceFrameMapper.GetSequenceIndex(0, 60, 24, 1, 100));
    Equal(12, SequenceFrameMapper.GetSequenceIndex(30, 60, 24, 1, 100));
    Equal(24, SequenceFrameMapper.GetSequenceIndex(60, 60, 24, 1, 100));
    Equal(99, SequenceFrameMapper.GetSequenceIndex(10_000, 60, 24, 1, 100));
    Equal(-1, SequenceFrameMapper.GetSequenceIndex(0, 60, 24, 1, 0));
});

Run("再生開始位置は1始まり", () =>
{
    Equal(9, SequenceFrameMapper.GetSequenceIndex(0, 60, 24, 10, 100));
    Equal(21, SequenceFrameMapper.GetSequenceIndex(30, 60, 24, 10, 100));
    Equal(99, SequenceFrameMapper.GetSequenceIndex(10_000, 60, 24, 10, 100));
});

Run("素材枚数とfpsからアイテム長を切り上げ計算", () =>
{
    Equal(100, SequenceTimelineMath.GetItemLength(100, 30, 30));
    Equal(125, SequenceTimelineMath.GetItemLength(100, 30, 24));
    Equal(2, SequenceTimelineMath.GetItemLength(1, 30, 24));
});

Run("ContentOffsetで分割後の再生位置を復元", () =>
{
    var offset = SequenceTimelineMath.GetContentOffset(1, 100, 30);
    Equal(1, SequenceTimelineMath.GetPlaybackStart(offset, 100, 30));

    offset += TimeSpan.FromSeconds(10d / 30d);
    Equal(11, SequenceTimelineMath.GetPlaybackStart(offset, 100, 30));
    Equal(11, SequenceTimelineMath.AdvancePlaybackStart(1, 10, 30, 30));
});

Run("YMM4対応8言語のリソースを切り替え", () =>
{
    var expectedNames = new Dictionary<string, string>
    {
        ["Resource.ar-SA.resx"] = "تسلسل صور TIFF",
        ["Resource.en-US.resx"] = "TIFF Image Sequence",
        ["Resource.es-ES.resx"] = "Secuencia de imágenes TIFF",
        ["Resource.ja-JP.resx"] = "連番TIFF画像",
        ["Resource.ko-KR.resx"] = "TIFF 이미지 시퀀스",
        ["Resource.zh-CN.resx"] = "TIFF 图像序列",
        ["Resource.zh-TW.resx"] = "TIFF 圖片序列",
        ["Resource.id-ID.resx"] = "Urutan Gambar TIFF",
    };

    var requiredKeys = new HashSet<string>
    {
        "PluginName", "FirstFileName", "FirstFileDescription", "PlaybackStartName",
        "PlaybackStartDescription", "FrameRateName", "FrameRateDescription",
        "FrameUnit", "FpsUnit", "TiffFilterName",
    };

    foreach (var (fileName, expectedName) in expectedNames)
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Localization", fileName));
        var values = document.Root!
            .Elements("data")
            .ToDictionary(x => (string)x.Attribute("name")!, x => (string)x.Element("value")!);
        Equal(10, values.Count);
        Equal(true, requiredKeys.SetEquals(values.Keys));
        Equal(expectedName, values["PluginName"]);
    }
});

if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    return 1;
}

Console.WriteLine("8 smoke tests passed.");
return 0;

void Run(string name, Action test)
{
    try
    {
        test();
    }
    catch (Exception ex)
    {
        failures.Add($"FAIL: {name}: {ex.Message}");
    }
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"expected={expected}, actual={actual}");
}

internal sealed class TemporaryDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        "SequenceTIFFPlugin.Tests",
        Guid.NewGuid().ToString("N"));

    public TemporaryDirectory() => Directory.CreateDirectory(Path);

    public string Create(string fileName)
    {
        var path = System.IO.Path.Combine(Path, fileName);
        File.WriteAllBytes(path, []);
        return path;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
