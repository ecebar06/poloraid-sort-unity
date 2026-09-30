/// <summary>
/// Keeps English category labels independent from the machine's Turkish
/// casing rules. This is a text normalization step, not a font change.
/// </summary>
public static class CategoryText
{
    public static string NormalizeEnglish(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        return value.Trim()
            .Replace('ı', 'i')
            .Replace('İ', 'I')
            .Replace('ş', 's')
            .Replace('Ş', 'S')
            .Replace('ğ', 'g')
            .Replace('Ğ', 'G')
            .Replace('ç', 'c')
            .Replace('Ç', 'C')
            .Replace('ö', 'o')
            .Replace('Ö', 'O')
            .Replace('ü', 'u')
            .Replace('Ü', 'U');
    }

    public static string ToLowerEnglish(string value)
    {
        return NormalizeEnglish(value).ToLowerInvariant();
    }

    public static string ToUpperEnglish(string value)
    {
        return NormalizeEnglish(value).ToUpperInvariant();
    }
}
