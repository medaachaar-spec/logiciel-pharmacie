using Pharmacy.Desktop.Components.Pos;
using Pharmacy.Domain.Common;
using Xunit;

namespace Pharmacy.UnitTests;

public sealed class SalesPosErrorMapperTests
{
    [Fact]
    public void ProductNotFoundHasSafeExplicitMessage()
    {
        var presentation = SalesPosErrorMapper.Map(
            new Error("sales.product.not_found", "Technical message."));

        Assert.Contains("inconnu", presentation.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Aucun produit n’a été créé", presentation.Message, StringComparison.Ordinal);
        Assert.Null(presentation.AvailabilityIncident);
    }

    [Fact]
    public void NonSellableProductHasDedicatedMessage()
    {
        var presentation = SalesPosErrorMapper.Map(
            new Error("sales.product.not_sellable", "Technical message."));

        Assert.Contains("n’est pas vendable", presentation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidQuantityExplainsThatRemovalIsSeparate()
    {
        var presentation = SalesPosErrorMapper.Map(
            new Error("sales.quantity.invalid", "Technical message."));

        Assert.Contains("strictement positif", presentation.Message, StringComparison.Ordinal);
        Assert.Contains("Supprimer", presentation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AvailabilityMessageKeepsRequestedAndAvailableQuantities()
    {
        var presentation = SalesPosErrorMapper.Map(
            new Error("sales.stock.insufficient_availability", "Technical message."),
            new SalesPosErrorContext("Produit Démo E", 5, 4));

        Assert.Equal("Produit Démo E", presentation.AvailabilityIncident!.ProductName);
        Assert.Equal(5, presentation.AvailabilityIncident.RequestedQuantity);
        Assert.Equal(4, presentation.AvailabilityIncident.AvailableQuantity);
        Assert.Contains("panier existant est conservé", presentation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnexpectedExpectedErrorUsesGenericSafeMessageWithoutTechnicalDetails()
    {
        var presentation = SalesPosErrorMapper.Map(
            new Error("sales.future.error", "Sensitive technical details."));

        Assert.DoesNotContain("Sensitive", presentation.Message, StringComparison.Ordinal);
        Assert.Contains("panier existant est conservé", presentation.Message, StringComparison.Ordinal);
    }
}
