using QuestPDF.Drawing;

namespace ContactTogetherApi.Reports.Export;

/// <summary>Registers the Thai font embedded in the assembly (Assets/Fonts, SIL OFL) with QuestPDF.</summary>
public static class ReportFonts
{
    /// <summary>Family name stored in the font files.</summary>
    public const string Family = "Sarabun";

    /// <summary>Call once at startup, before any PDF is generated.</summary>
    public static void Register()
    {
        var assembly = typeof(ReportFonts).Assembly;
        var fonts = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (fonts.Count == 0)
        {
            throw new InvalidOperationException("No report fonts are embedded. Check the EmbeddedResource item for Assets/Fonts.");
        }

        foreach (var name in fonts)
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            FontManager.RegisterFontFromStream(stream);
        }
    }
}
