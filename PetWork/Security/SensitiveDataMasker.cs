namespace PetWork.Security;

public static class SensitiveDataMasker
{
    public static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "[empty]";
        var visibleLength = Math.Min(4, value.Length);
        return string.Concat(value.AsSpan(0, visibleLength), "…");
    }
}
