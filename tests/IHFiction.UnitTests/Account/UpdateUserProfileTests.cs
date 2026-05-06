using System.ComponentModel.DataAnnotations;

using IHFiction.FictionApi.Account;
using IHFiction.FictionApi.Common;
using IHFiction.SharedKernel.Validation;

namespace IHFiction.UnitTests.Account;

public class UpdateUserProfileTests
{
    [Fact]
    public void UpdateOwnUserProfileBody_Name_HasRequiredAttribute()
    {
        var property = typeof(UpdateOwnUserProfile.UpdateOwnUserProfileBody).GetProperty("Name");
        var attributes = property?.GetCustomAttributes(true);

        Assert.NotNull(property);
        Assert.NotNull(attributes);
        Assert.Contains(attributes, a => a is RequiredAttribute);
    }

    [Fact]
    public void UpdateOwnUserProfileBody_Name_HasCorrectStringLength()
    {
        var property = typeof(UpdateOwnUserProfile.UpdateOwnUserProfileBody).GetProperty("Name");
        var attributes = property?.GetCustomAttributes(true);

        Assert.NotNull(attributes);
        var stringLengthAttr = attributes.OfType<StringLengthAttribute>().FirstOrDefault();
        Assert.NotNull(stringLengthAttr);
        Assert.Equal(100, stringLengthAttr.MaximumLength);
        Assert.Equal(1, stringLengthAttr.MinimumLength);
    }

    [Fact]
    public void UpdateOwnUserProfileBody_Name_HasNoExcessiveWhitespace()
    {
        var property = typeof(UpdateOwnUserProfile.UpdateOwnUserProfileBody).GetProperty("Name");
        var attributes = property?.GetCustomAttributes(true);

        Assert.NotNull(attributes);
        Assert.Contains(attributes, a => a is NoExcessiveWhitespaceAttribute);
    }

    [Fact]
    public void UpdateOwnUserProfileBody_Name_HasNoHarmfulContent()
    {
        var property = typeof(UpdateOwnUserProfile.UpdateOwnUserProfileBody).GetProperty("Name");
        var attributes = property?.GetCustomAttributes(true);

        Assert.NotNull(attributes);
        Assert.Contains(attributes, a => a is NoHarmfulContentAttribute);
    }

    [Fact]
    public void UpdateOwnUserProfileBody_GravatarEmail_HasEmailAddressAttribute()
    {
        var property = typeof(UpdateOwnUserProfile.UpdateOwnUserProfileBody).GetProperty("GravatarEmail");
        var attributes = property?.GetCustomAttributes(true);

        Assert.NotNull(property);
        Assert.NotNull(attributes);
        Assert.Contains(attributes, a => a is EmailAddressAttribute);
    }

    [Fact]
    public void UpdateOwnUserProfileBody_GravatarEmail_HasCorrectStringLength()
    {
        var property = typeof(UpdateOwnUserProfile.UpdateOwnUserProfileBody).GetProperty("GravatarEmail");
        var attributes = property?.GetCustomAttributes(true);

        Assert.NotNull(attributes);
        var stringLengthAttr = attributes.OfType<StringLengthAttribute>().FirstOrDefault();
        Assert.NotNull(stringLengthAttr);
        Assert.Equal(256, stringLengthAttr.MaximumLength);
    }

    [Fact]
    public void UpdateOwnUserProfileBody_GravatarEmail_HasNoHarmfulContent()
    {
        var property = typeof(UpdateOwnUserProfile.UpdateOwnUserProfileBody).GetProperty("GravatarEmail");
        var attributes = property?.GetCustomAttributes(true);

        Assert.NotNull(attributes);
        Assert.Contains(attributes, a => a is NoHarmfulContentAttribute);
    }
}

public class UpdateUserProfileSanitizationTests
{
    [Fact]
    public void SanitizeText_NormalizesExcessiveWhitespace()
    {
        var result = InputSanitizationService.SanitizeText("Hello  World");

        Assert.NotNull(result);
        Assert.DoesNotContain("  ", result);
    }

    [Fact]
    public void SanitizeOptionalText_ReturnsNullForEmptyInput()
    {
        var result = InputSanitizationService.SanitizeOptionalText(string.Empty);

        Assert.Null(result);
    }

    [Fact]
    public void SanitizeOptionalText_ReturnsNullForNullInput()
    {
        var result = InputSanitizationService.SanitizeOptionalText(null);

        Assert.Null(result);
    }

    [Fact]
    public void SanitizeOptionalText_ReturnsValueForValidEmail()
    {
        var result = InputSanitizationService.SanitizeOptionalText("user@example.com");

        Assert.Equal("user@example.com", result);
    }
}
