using System.Reflection;

namespace SequenceTIFFPlugin;

/// <summary>
/// YMM4本体アセンブリへの直接参照を増やさず、公開されているShapeItemの値だけを操作します。
/// </summary>
internal static class SequenceTiffItemCoordinator
{
    public static bool IsSupportedOwner(object owner)
        => FindProperty(owner, "Frame", typeof(int), writable: false) is not null
            && FindProperty(owner, "Length", typeof(int), writable: true) is not null
            && FindProperty(owner, "ContentOffset", typeof(TimeSpan), writable: true) is not null;

    public static int GetFrame(object owner)
        => (int?)FindProperty(owner, "Frame", typeof(int), writable: false)?.GetValue(owner) ?? 0;

    public static TimeSpan GetContentOffset(object owner)
        => (TimeSpan?)FindProperty(owner, "ContentOffset", typeof(TimeSpan), writable: false)?.GetValue(owner)
            ?? TimeSpan.Zero;

    public static void SetLength(object owner, int value)
        => FindProperty(owner, "Length", typeof(int), writable: true)?.SetValue(owner, Math.Max(1, value));

    public static void SetContentOffset(object owner, TimeSpan value)
        => FindProperty(owner, "ContentOffset", typeof(TimeSpan), writable: true)?.SetValue(owner, value);

    private static PropertyInfo? FindProperty(object owner, string name, Type propertyType, bool writable)
    {
        var property = owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        if (property?.PropertyType != propertyType || !property.CanRead || (writable && !property.CanWrite))
            return null;
        return property;
    }
}
