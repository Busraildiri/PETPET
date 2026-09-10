using PetWork.Security;

namespace PetWork.Tests;

public sealed class SensitiveDataMaskerTests
{
    [Theory]
    [InlineData("user@example.com", "user…")]
    [InlineData("abc", "abc…")]
    [InlineData("", "[empty]")]
    [InlineData(null, "[empty]")]
    public void Mask_ExposesAtMostTheFirstFourCharacters(string? value, string expected) =>
        Assert.Equal(expected, SensitiveDataMasker.Mask(value));
}
