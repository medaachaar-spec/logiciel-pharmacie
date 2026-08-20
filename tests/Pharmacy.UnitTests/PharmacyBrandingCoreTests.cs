using Pharmacy.Application.Branding;
using Pharmacy.Application.Branding.Models;
using Xunit;

namespace Pharmacy.UnitTests;

public sealed class PharmacyBrandingCoreTests
{
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    [Fact]
    public void BRAND001_DefaultPharmacyNameIsPharmacieDiarDouha()
    {
        Assert.Equal("Pharmacie Diar Douha", PharmacyBrandingDefaults.CreateView().PharmacyName);
    }

    [Fact]
    public void BRAND002_DefaultPrimaryColorIsValidProfessionalGreen()
    {
        var request = ValidRequest(primaryColor: PharmacyBrandingDefaults.PrimaryColor);

        Assert.Equal("#0D9A48", PharmacyBrandingDefaults.PrimaryColor);
        Assert.True(PharmacyBrandingValidator.Validate(request).IsSuccess);
    }

    [Fact]
    public void BRAND011_InvalidNameIsRejected()
    {
        var blank = PharmacyBrandingValidator.Validate(ValidRequest(pharmacyName: "   "));
        var tooLong = PharmacyBrandingValidator.Validate(ValidRequest(
            pharmacyName: new string('P', PharmacyBrandingDefaults.MaxPharmacyNameLength + 1)));

        Assert.Equal("branding.name.invalid", blank.Error.Code);
        Assert.Equal("branding.name.invalid", tooLong.Error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("green")]
    [InlineData("#12")]
    [InlineData("#GGGGGG")]
    public void BRAND012_InvalidColorIsRejected(string color)
    {
        var result = PharmacyBrandingValidator.Validate(ValidRequest(primaryColor: color));

        Assert.Equal("branding.color.invalid", result.Error.Code);
    }

    [Fact]
    public void BRAND013_InvalidOrOversizedLogoIsRejected()
    {
        var invalid = PharmacyBrandingValidator.ValidateLogo(
            "not an image"u8.ToArray(), "image/png", "logo.png");
        var mismatched = PharmacyBrandingValidator.ValidateLogo(
            ValidPng(), "image/jpeg", "logo.jpg");
        var oversized = PharmacyBrandingValidator.ValidateLogo(
            new byte[PharmacyBrandingDefaults.MaxLogoBytes + 1], "image/png", "logo.png");

        Assert.Equal("branding.logo.invalid", invalid.Error.Code);
        Assert.Equal("branding.logo.invalid", mismatched.Error.Code);
        Assert.Equal("branding.logo.too_large", oversized.Error.Code);
    }

    private static UpdatePharmacyBrandingRequest ValidRequest(
        string pharmacyName = "Pharmacie Test",
        string primaryColor = "#0D9A48") => new(
            Guid.NewGuid(), pharmacyName, primaryColor, null, null, null, RowVersion);

    private static byte[] ValidPng() =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
}
