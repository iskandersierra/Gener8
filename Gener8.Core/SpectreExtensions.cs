using System.Globalization;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Gener8.Core;

public static class SpectreExtensions
{
    public static string MarkupInterpolated(IFormatProvider provider, FormattableString value)
    {
        object?[] args = value
            .GetArguments()
            .Select(arg => arg is string s ? s.EscapeMarkup() : arg)
            .ToArray();

        return string.Format(provider, value.Format, args);
    }

    public static string MarkupInterpolated(FormattableString value) =>
        MarkupInterpolated(CultureInfo.CurrentCulture, value);

    public static IRenderable AsMarkup(this string value, Color color) =>
        new Markup(value.EscapeMarkup(), new(color));

    public static IRenderable AsHeaderMarkup(this string value) => value.AsMarkup(Color.White);

    public static IRenderable AsEmptyMarkup(this string value) => value.AsMarkup(Color.Grey);

    public static IRenderable AsMarkup(this string? value, string notSpecified = "∅") =>
        string.IsNullOrEmpty(value) ? notSpecified.AsEmptyMarkup() : value.AsMarkup(Color.Aqua);

    public static IRenderable AsMarkup(this bool value) =>
        value ? "✓".AsMarkup(Color.Green) : "×".AsMarkup(Color.Red);

    public static IRenderable AsMarkup(this bool? value) =>
        value is null ? "∅".AsEmptyMarkup()
        : value.Value ? "✓".AsMarkup(Color.Green)
        : "×".AsMarkup(Color.Red);
}
