namespace Content.Shared.Localizations;

public sealed partial class ContentLocalizationManager
{
    private static ILocValue FormatPossessive(LocArgs args)
    {
        var text = args.Args[0].Value?.ToString() ?? string.Empty;

        if (text.EndsWith('s') || text.EndsWith('S'))
            return new LocValueString(text + "'");

        return new LocValueString(text + "'s");
    }
}
