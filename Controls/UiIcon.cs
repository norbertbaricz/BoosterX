namespace BoosterX.App.Controls;

public enum UiIcon
{
    None, Dashboard, Sparkles, Trash, Apps, Startup, Network, Wrench, History, Activity,
    Shield, Search, Checklist, Save, Settings, Check, Clear, Folder, Store, Refresh,
    Disable, Globe, Dns, Disk, Layers, Restore, Heart, Copy, Bell, Stop, Menu, Info, Warning, Error
}

internal enum IconPrimitiveKind { Lines, Ellipse, Rectangle, Bezier }
internal readonly record struct IconPrimitive(IconPrimitiveKind Kind, float[] Points);

/// <summary>Original 24-unit line icons. No installed symbol font or bitmap scaling is required.</summary>
internal static class UiIconVectors
{
    private static IconPrimitive L(params float[] points) => new(IconPrimitiveKind.Lines, points);
    private static IconPrimitive E(float x, float y, float width, float height) => new(IconPrimitiveKind.Ellipse, [x, y, width, height]);
    private static IconPrimitive R(float x, float y, float width, float height) => new(IconPrimitiveKind.Rectangle, [x, y, width, height]);
    private static IconPrimitive B(params float[] points) => new(IconPrimitiveKind.Bezier, points);

    internal static readonly IReadOnlyDictionary<UiIcon, IconPrimitive[]> Shapes = new Dictionary<UiIcon, IconPrimitive[]>
    {
        [UiIcon.Dashboard] = [R(3, 3, 7, 7), R(14, 3, 7, 7), R(3, 14, 7, 7), R(14, 14, 7, 7)],
        [UiIcon.Sparkles] = [L(11, 3, 13, 9, 19, 11, 13, 13, 11, 19, 9, 13, 3, 11, 9, 9, 11, 3), L(19, 3, 19, 7), L(17, 5, 21, 5), L(19, 17, 19, 21), L(17, 19, 21, 19)],
        [UiIcon.Trash] = [L(3, 6, 21, 6), L(9, 6, 9, 3, 15, 3, 15, 6), L(6, 6, 7, 21, 17, 21, 18, 6), L(10, 10, 10, 17), L(14, 10, 14, 17)],
        [UiIcon.Apps] = [R(3, 3, 7, 7), R(14, 3, 7, 7), R(3, 14, 7, 7), L(14, 17, 21, 17), L(17.5f, 14, 17.5f, 21)],
        [UiIcon.Startup] = [L(13, 2, 4, 14, 11, 14, 10, 22, 20, 10, 13, 10, 13, 2)],
        [UiIcon.Network] = [R(8, 2, 8, 6), R(2, 16, 7, 6), R(15, 16, 7, 6), L(12, 8, 12, 12), L(5.5f, 16, 5.5f, 12, 18.5f, 12, 18.5f, 16)],
        [UiIcon.Wrench] = [B(20, 3, 22, 8, 18, 12, 14, 10), L(14, 10, 6, 20, 3, 17, 12, 9), B(12, 9, 10, 5, 14, 1, 18, 3), L(18, 3, 15, 6, 18, 9, 21, 6)],
        [UiIcon.History] = [B(4, 8, 6, 2, 15, 1, 19, 6), B(19, 6, 23, 14, 17, 23, 8, 20), L(3, 3, 3, 9, 9, 9), L(12, 7, 12, 12, 16, 15)],
        [UiIcon.Activity] = [L(2, 12, 6, 12, 9, 4, 14, 20, 17, 12, 22, 12)],
        [UiIcon.Shield] = [L(12, 2, 21, 6, 20, 14), B(20, 14, 19, 18, 15, 21, 12, 22), B(12, 22, 9, 21, 5, 18, 4, 14), L(4, 14, 3, 6, 12, 2), L(8, 12, 11, 15, 16, 9)],
        [UiIcon.Search] = [E(3, 3, 13, 13), L(14, 14, 21, 21)],
        [UiIcon.Checklist] = [L(3, 6, 5, 8, 8, 4), L(3, 13, 5, 15, 8, 11), L(3, 20, 5, 22, 8, 18), L(12, 6, 21, 6), L(12, 13, 21, 13), L(12, 20, 21, 20)],
        [UiIcon.Save] = [L(3, 3, 18, 3, 21, 6, 21, 21, 3, 21, 3, 3), R(7, 3, 9, 6), R(7, 14, 10, 7)],
        [UiIcon.Settings] = [E(8, 8, 8, 8), L(12, 2, 12, 5), L(12, 19, 12, 22), L(2, 12, 5, 12), L(19, 12, 22, 12), L(5, 5, 7, 7), L(17, 17, 19, 19), L(5, 19, 7, 17), L(17, 7, 19, 5), E(5, 5, 14, 14)],
        [UiIcon.Check] = [L(4, 12, 9, 17, 20, 6)],
        [UiIcon.Clear] = [L(8, 3, 21, 16, 15, 22, 2, 9, 8, 3), L(8, 15, 14, 9), L(14, 22, 22, 22)],
        [UiIcon.Folder] = [L(2, 6, 9, 6, 11, 9, 22, 9, 20, 21, 2, 21, 2, 6), L(2, 9, 2, 3, 9, 3, 12, 6, 20, 6, 20, 9)],
        [UiIcon.Store] = [L(4, 7, 20, 7, 22, 12, 2, 12, 4, 7), L(4, 12, 4, 21, 20, 21, 20, 12), L(4, 7, 4, 3, 20, 3, 20, 7), R(9, 15, 6, 6)],
        [UiIcon.Refresh] = [B(3, 10, 5, 2, 17, 1, 21, 8), L(21, 3, 21, 9, 15, 9), B(21, 14, 19, 22, 7, 23, 3, 16), L(3, 21, 3, 15, 9, 15)],
        [UiIcon.Disable] = [E(3, 3, 18, 18), L(5.5f, 5.5f, 18.5f, 18.5f)],
        [UiIcon.Globe] = [E(2, 2, 20, 20), E(7, 2, 10, 20), L(2, 12, 22, 12), L(4, 6, 20, 6), L(4, 18, 20, 18)],
        [UiIcon.Dns] = [R(3, 3, 18, 7), R(3, 14, 18, 7), L(7, 6.5f, 9, 6.5f), L(7, 17.5f, 9, 17.5f), L(15, 6.5f, 18, 6.5f), L(15, 17.5f, 18, 17.5f)],
        [UiIcon.Disk] = [L(6, 3, 18, 3, 22, 15, 22, 21, 2, 21, 2, 15, 6, 3), L(2, 15, 22, 15), L(16, 18, 19, 18)],
        [UiIcon.Layers] = [L(2, 7, 12, 2, 22, 7, 12, 12, 2, 7), L(2, 12, 12, 17, 22, 12), L(2, 17, 12, 22, 22, 17)],
        [UiIcon.Restore] = [L(9, 4, 3, 10, 9, 16), L(3, 10, 14, 10), B(14, 10, 22, 10, 22, 21, 14, 21), L(14, 21, 8, 21)],
        [UiIcon.Heart] = [B(12, 21, 9, 18, 2, 13, 2, 8), B(2, 8, 2, 2, 9, 1, 12, 6), B(12, 6, 15, 1, 22, 2, 22, 8), B(22, 8, 22, 13, 15, 18, 12, 21)],
        [UiIcon.Copy] = [R(8, 8, 13, 13), L(16, 8, 16, 3, 3, 3, 3, 16, 8, 16)],
        [UiIcon.Bell] = [B(5, 16, 7, 13, 5, 5, 12, 4), B(12, 4, 19, 5, 17, 13, 19, 16), L(19, 16, 21, 18, 3, 18, 5, 16), L(12, 2, 12, 4), B(9, 21, 10, 23, 14, 23, 15, 21)],
        [UiIcon.Stop] = [R(5, 5, 14, 14)],
        [UiIcon.Menu] = [L(3, 5, 21, 5), L(3, 12, 21, 12), L(3, 19, 21, 19)],
        [UiIcon.Info] = [E(2, 2, 20, 20), L(12, 11, 12, 17), L(12, 7, 12, 7.2f)],
        [UiIcon.Warning] = [L(12, 2, 22, 21, 2, 21, 12, 2), L(12, 8, 12, 13), L(12, 17, 12, 17.2f)],
        [UiIcon.Error] = [E(2, 2, 20, 20), L(8, 8, 16, 16), L(16, 8, 8, 16)]
    };
}
