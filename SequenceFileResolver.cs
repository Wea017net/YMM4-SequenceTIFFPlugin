using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace SequenceTIFFPlugin;

internal static partial class SequenceFileResolver
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// 選択されたファイルを先頭として、同じ接頭辞を持つTIFFを連番順に返します。
    /// </summary>
    public static IReadOnlyList<string> Resolve(string? firstFile)
    {
        if (string.IsNullOrWhiteSpace(firstFile))
            return [];

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(firstFile);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return [];
        }

        if (!File.Exists(fullPath) || !IsTiff(fullPath))
            return [];

        var directory = Path.GetDirectoryName(fullPath);
        var stem = Path.GetFileNameWithoutExtension(fullPath);
        var firstMatch = TrailingNumberRegex().Match(stem);
        if (directory is null || !firstMatch.Success)
            return [fullPath];

        var prefix = firstMatch.Groups["prefix"].Value;
        if (!long.TryParse(
                firstMatch.Groups["number"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var firstNumber))
        {
            return [fullPath];
        }

        var numberedFiles = new List<NumberedFile>();

        try
        {
            foreach (var path in Directory.EnumerateFiles(directory))
            {
                if (!IsTiff(path))
                    continue;

                var candidateStem = Path.GetFileNameWithoutExtension(path);
                var candidateMatch = TrailingNumberRegex().Match(candidateStem);
                if (!candidateMatch.Success
                    || !string.Equals(
                        candidateMatch.Groups["prefix"].Value,
                        prefix,
                        StringComparison.OrdinalIgnoreCase)
                    || !long.TryParse(
                        candidateMatch.Groups["number"].Value,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var number)
                    || number < firstNumber)
                {
                    continue;
                }

                numberedFiles.Add(new NumberedFile(number, Path.GetFullPath(path)));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [fullPath];
        }

        if (numberedFiles.Count == 0)
            return [fullPath];

        return numberedFiles
            .OrderBy(file => file.Number)
            .ThenBy(file => file.Path, PathComparer)
            .Select(file => file.Path)
            .ToArray();
    }

    private static bool IsTiff(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".tif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"^(?<prefix>.*?)(?<number>\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingNumberRegex();

    private sealed record NumberedFile(long Number, string Path);
}
