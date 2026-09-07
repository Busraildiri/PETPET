using PetWork.Models;

namespace PetWork.Tests;

public sealed class PasswordPolicyTests
{
    [Theory]
    [InlineData("Valid1!x", true)]
    [InlineData("short", false)]
    [InlineData("nouppercase1!", false)]
    [InlineData("NOLOWERCASE1!", false)]
    [InlineData("NoNumber!", false)]
    [InlineData("NoSpecial1", false)]
    public void Validates_required_character_classes(string password, bool expected) =>
        Assert.Equal(expected, new PasswordPolicyAttribute().IsValid(password));
}
