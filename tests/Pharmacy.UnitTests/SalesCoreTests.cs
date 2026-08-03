using System.Reflection;
using Pharmacy.Application.Sales.Contracts;
using Pharmacy.Domain.Common;
using Pharmacy.Domain.Sales;
using Xunit;

namespace Pharmacy.UnitTests;

public sealed class SalesCoreTests
{
    [Fact]
    public void Check01_SuccessHasNoActiveError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.True(result.Error.IsNone);
    }

    [Fact]
    public void Check02_FailureContainsItsError()
    {
        var error = new Error("test.failure", "Échec attendu.");

        var result = Result.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Same(error, result.Error);
    }

    [Fact]
    public void Check03_GenericSuccessExposesItsValue()
    {
        Assert.Equal(42, Result<int>.Success(42).Value);
    }

    [Fact]
    public void Check04_GenericFailureDoesNotSilentlyExposeAValue()
    {
        var result = Result<int>.Failure(new Error("test.failure", "Échec attendu."));

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Check05_InconsistentResultCannotBeCreated()
    {
        Assert.Empty(typeof(Result).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(Result<int>).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Throws<ArgumentException>(() => Result.Failure(Error.None));
        Assert.Throws<ArgumentException>(() => Result<int>.Failure(Error.None));
    }

    [Fact]
    public void Check06_PositiveMoneyCanBeCreated()
    {
        var money = Money.Create(12.3456m);

        Assert.True(money.IsSuccess);
        Assert.Equal(12.3456m, money.Value.Amount);
        Assert.Equal("MAD", Money.CurrencyCode);
    }

    [Fact]
    public void Check07_ZeroMoneyIsValid()
    {
        Assert.Equal(Money.Zero, Money.Create(0m).Value);
    }

    [Fact]
    public void Check08_NegativeMoneyIsRejected()
    {
        var result = Money.Create(-0.0001m);

        Assert.True(result.IsFailure);
        Assert.Equal("sales.money.invalid", result.Error.Code);
    }

    [Fact]
    public void Check09_MoneyAdditionIsExact()
    {
        var left = Money.Create(0.1m).Value;
        var right = Money.Create(0.2m).Value;

        Assert.Equal(0.3m, left.Add(right).Amount);
    }

    [Fact]
    public void Check10_MoneyMultiplicationByQuantityIsExact()
    {
        var money = Money.Create(12.3456m).Value;
        var quantity = SaleQuantity.Create(3).Value;

        Assert.Equal(37.0368m, money.Multiply(quantity).Amount);
    }

    [Fact]
    public void Check11_MoneyEqualityUsesItsValue()
    {
        Assert.Equal(Money.Create(5.25m).Value, Money.Create(5.25m).Value);
        Assert.NotEqual(Money.Create(5.25m).Value, Money.Create(5.26m).Value);
    }

    [Fact]
    public void Check12_MoneyPublicApiContainsNoFloatingPointType()
    {
        var publicTypes = typeof(Money)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .SelectMany(MemberTypes)
            .ToArray();

        Assert.DoesNotContain(typeof(double), publicTypes);
        Assert.DoesNotContain(typeof(float), publicTypes);
    }

    [Fact]
    public void Check13_PositiveProductIdIsAccepted()
    {
        Assert.Equal(25, ProductId.Create(25).Value.Value);
    }

    [Fact]
    public void Check14_ZeroProductIdIsRejected()
    {
        Assert.True(ProductId.Create(0).IsFailure);
    }

    [Fact]
    public void Check15_NegativeProductIdIsRejected()
    {
        Assert.True(ProductId.Create(-1).IsFailure);
    }

    [Fact]
    public void Check16_EmptySaleDraftIdIsRejected()
    {
        Assert.True(SaleDraftId.Create(Guid.Empty).IsFailure);
        Assert.NotEqual(Guid.Empty, SaleDraftId.New().Value);
    }

    [Fact]
    public void Check17_EmptyProductReferenceIsRejected()
    {
        Assert.True(ProductReference.Create("   ").IsFailure);
        Assert.True(ProductReference.Create(null).IsFailure);
    }

    [Fact]
    public void Check18_ProductReferenceIsTrimmed()
    {
        Assert.Equal("REF-001", ProductReference.Create("  REF-001  ").Value.Value);
    }

    [Fact]
    public void Check19_ProductReferenceEqualityIsConsistent()
    {
        var upper = ProductReference.Create("REF-001").Value;
        var lower = ProductReference.Create("ref-001").Value;

        Assert.Equal(upper, lower);
        Assert.Equal(upper.GetHashCode(), lower.GetHashCode());
    }

    [Fact]
    public void Check20_QuantityOneIsAccepted()
    {
        Assert.Equal(1, SaleQuantity.Create(1).Value.Value);
    }

    [Fact]
    public void Check21_ZeroQuantityIsRejected()
    {
        Assert.True(SaleQuantity.Create(0).IsFailure);
    }

    [Fact]
    public void Check22_NegativeQuantityIsRejected()
    {
        Assert.True(SaleQuantity.Create(-1).IsFailure);
    }

    [Fact]
    public void Check23_QuantityAdditionIsCorrect()
    {
        var result = SaleQuantity.Create(2).Value.Add(SaleQuantity.Create(3).Value);

        Assert.Equal(5, result.Value.Value);
    }

    [Fact]
    public void Check24_QuantityPublicApiCannotExpressAFraction()
    {
        var publicTypes = typeof(SaleQuantity)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .SelectMany(MemberTypes)
            .ToArray();

        Assert.DoesNotContain(typeof(decimal), publicTypes);
        Assert.DoesNotContain(typeof(double), publicTypes);
        Assert.DoesNotContain(typeof(float), publicTypes);
    }

    [Fact]
    public void Check25_ValidProductSnapshotCanBeCreated()
    {
        var snapshot = CreateSnapshot(1, "DEMO-001", "Produit synthétique", 10.50m);

        Assert.Equal(1, snapshot.ProductId.Value);
        Assert.Equal("DEMO-001", snapshot.Reference.Value);
        Assert.Equal("Produit synthétique", snapshot.DisplayName);
        Assert.Equal(10.50m, snapshot.UnitSalePrice.Amount);
    }

    [Fact]
    public void Check26_EmptyProductNameIsRejected()
    {
        var result = SaleProductSnapshot.Create(
            ProductId.Create(1).Value,
            ProductReference.Create("DEMO-001").Value,
            " ",
            null,
            Money.Zero);

        Assert.True(result.IsFailure);
        Assert.Equal("sales.product_name.invalid", result.Error.Code);
    }

    [Fact]
    public void Check27_NegativeUnitPriceCannotReachASnapshot()
    {
        Assert.True(Money.Create(-1m).IsFailure);
        Assert.All(
            typeof(SaleProductSnapshot).GetConstructors(BindingFlags.Public | BindingFlags.Instance),
            constructor => Assert.DoesNotContain(
                constructor.GetParameters(),
                parameter => parameter.ParameterType == typeof(decimal)));
    }

    [Fact]
    public void Check28_LineSubtotalIsExact()
    {
        var line = SaleLine.Create(
            CreateSnapshot(1, "DEMO-001", "Produit synthétique", 12.3456m),
            SaleQuantity.Create(3).Value).Value;

        Assert.Equal(37.0368m, line.Subtotal.Amount);
    }

    [Fact]
    public void Check29_LineQuantityChangeReturnsTheCorrectLine()
    {
        var line = SaleLine.Create(
            CreateSnapshot(1, "DEMO-001", "Produit synthétique", 2m),
            SaleQuantity.One).Value;

        var changed = line.ChangeQuantity(SaleQuantity.Create(4).Value).Value;

        Assert.Equal(4, changed.Quantity.Value);
        Assert.Equal(1, line.Quantity.Value);
    }

    [Fact]
    public void Check30_LineStateIsPreservedWhenQuantityCreationFails()
    {
        var line = SaleLine.Create(
            CreateSnapshot(1, "DEMO-001", "Produit synthétique", 2m),
            SaleQuantity.One).Value;

        var invalidQuantity = SaleQuantity.Create(0);

        Assert.True(invalidQuantity.IsFailure);
        Assert.Equal(1, line.Quantity.Value);
        Assert.Equal(2m, line.Subtotal.Amount);
    }

    [Fact]
    public void Check31_DraftIsCreatedEmpty()
    {
        var draft = CreateDraft();

        Assert.True(draft.IsEmpty);
        Assert.Empty(draft.Lines);
        Assert.Equal(0, draft.LineCount);
    }

    [Fact]
    public void Check32_DraftInitialTotalIsZero()
    {
        Assert.Equal(Money.Zero, CreateDraft().Total);
    }

    [Fact]
    public void Check33_AddingAFirstItemCreatesOneLine()
    {
        var draft = CreateDraft();

        var result = draft.AddItem(
            CreateSnapshot(1, "DEMO-001", "Produit synthétique", 2m),
            SaleQuantity.One);

        Assert.True(result.IsSuccess);
        Assert.Single(draft.Lines);
    }

    [Fact]
    public void Check34_AddingTheSameProductMergesTheLine()
    {
        var draft = CreateDraft();
        var snapshot = CreateSnapshot(1, "DEMO-001", "Produit synthétique", 2m);

        draft.AddItem(snapshot, SaleQuantity.One);
        draft.AddItem(snapshot, SaleQuantity.Create(2).Value);

        Assert.Equal(3, Assert.Single(draft.Lines).Quantity.Value);
    }

    [Fact]
    public void Check35_DifferentProductsCreateSeveralLines()
    {
        var draft = CreateDraftWithTwoProducts();

        Assert.Equal(2, draft.LineCount);
    }

    [Fact]
    public void Check36_DraftContainsNoDuplicateProductId()
    {
        var draft = CreateDraft();
        var first = CreateSnapshot(1, "DEMO-001", "Produit initial", 2m);
        var refreshed = CreateSnapshot(1, "DEMO-001", "Produit actualisé", 3m);

        draft.AddItem(first, SaleQuantity.One);
        draft.AddItem(refreshed, SaleQuantity.One);

        Assert.Equal(draft.LineCount, draft.Lines.Select(line => line.ProductId).Distinct().Count());
        Assert.Equal("Produit initial", Assert.Single(draft.Lines).ProductSnapshot.DisplayName);
    }

    [Fact]
    public void Check37_DraftQuantityChangeSucceeds()
    {
        var draft = CreateDraft();
        var snapshot = CreateSnapshot(1, "DEMO-001", "Produit synthétique", 2m);
        draft.AddItem(snapshot, SaleQuantity.One);

        var result = draft.ChangeQuantity(snapshot.ProductId, SaleQuantity.Create(5).Value);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, Assert.Single(draft.Lines).Quantity.Value);
    }

    [Fact]
    public void Check38_ChangingAnAbsentLineReturnsAnError()
    {
        var result = CreateDraft().ChangeQuantity(
            ProductId.Create(99).Value,
            SaleQuantity.One);

        Assert.True(result.IsFailure);
        Assert.Equal("sales.line.not_found", result.Error.Code);
    }

    [Fact]
    public void Check39_InvalidQuantityDoesNotModifyTheDraft()
    {
        var draft = CreateDraft();
        var snapshot = CreateSnapshot(1, "DEMO-001", "Produit synthétique", 2m);
        draft.AddItem(snapshot, SaleQuantity.Create(2).Value);

        var invalidQuantity = SaleQuantity.Create(0);

        Assert.True(invalidQuantity.IsFailure);
        Assert.Equal(2, Assert.Single(draft.Lines).Quantity.Value);
        Assert.Equal(4m, draft.Total.Amount);
    }

    [Fact]
    public void Check40_RemovingAPresentLineSucceeds()
    {
        var draft = CreateDraft();
        var snapshot = CreateSnapshot(1, "DEMO-001", "Produit synthétique", 2m);
        draft.AddItem(snapshot, SaleQuantity.One);

        var result = draft.RemoveItem(snapshot.ProductId);

        Assert.True(result.IsSuccess);
        Assert.Empty(draft.Lines);
    }

    [Fact]
    public void Check41_RemovingAnAbsentLineReturnsAnError()
    {
        var result = CreateDraft().RemoveItem(ProductId.Create(99).Value);

        Assert.True(result.IsFailure);
        Assert.Equal("sales.line.not_found", result.Error.Code);
    }

    [Fact]
    public void Check42_DraftTotalEqualsTheExactSubtotalSum()
    {
        var draft = CreateDraftWithTwoProducts();

        Assert.Equal(draft.Lines.Aggregate(Money.Zero, (sum, line) => sum.Add(line.Subtotal)), draft.Total);
        Assert.Equal(7m, draft.Total.Amount);
    }

    [Fact]
    public void Check43_DraftTotalQuantityIsCorrect()
    {
        Assert.Equal(3, CreateDraftWithTwoProducts().TotalQuantity);
    }

    [Fact]
    public void Check44_ClearRemovesEveryLine()
    {
        var draft = CreateDraftWithTwoProducts();

        var result = draft.Clear();

        Assert.True(result.IsSuccess);
        Assert.True(draft.IsEmpty);
        Assert.Equal(Money.Zero, draft.Total);
    }

    [Fact]
    public void Check45_ClearingAnEmptyDraftIsStable()
    {
        var draft = CreateDraft();

        Assert.True(draft.Clear().IsSuccess);
        Assert.True(draft.Clear().IsSuccess);
        Assert.Empty(draft.Lines);
    }

    [Fact]
    public void Check46_ExposedLineCollectionCannotBeModified()
    {
        var draft = CreateDraft();
        var exposed = Assert.IsAssignableFrom<IList<SaleLine>>(draft.Lines);
        var line = SaleLine.Create(
            CreateSnapshot(1, "DEMO-001", "Produit synthétique", 2m),
            SaleQuantity.One).Value;

        Assert.Throws<NotSupportedException>(() => exposed.Add(line));
        Assert.Empty(draft.Lines);
    }

    [Fact]
    public void Check47_SnapshotHasNoMutableExternalDependency()
    {
        var snapshot = CreateSnapshot(1, "DEMO-001", "Produit synthétique", 2m);
        var setters = typeof(SaleProductSnapshot)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod?.IsPublic == true);

        Assert.Empty(setters);
        Assert.All(typeof(SaleProductSnapshot).GetProperties(), property => Assert.NotEqual(typeof(object), property.PropertyType));
        Assert.Equal("Produit synthétique", snapshot.DisplayName);
    }

    [Fact]
    public void Check48_FailedMergeLeavesNoPartialState()
    {
        var draft = CreateDraft();
        var snapshot = CreateSnapshot(1, "DEMO-001", "Produit synthétique", 1m);
        draft.AddItem(snapshot, SaleQuantity.Create(int.MaxValue).Value);

        var result = draft.AddItem(snapshot, SaleQuantity.One);

        Assert.True(result.IsFailure);
        Assert.Equal("sales.quantity.overflow", result.Error.Code);
        Assert.Equal(int.MaxValue, Assert.Single(draft.Lines).Quantity.Value);
    }

    private static IEnumerable<Type> MemberTypes(MemberInfo member)
    {
        return member switch
        {
            PropertyInfo property => [property.PropertyType],
            FieldInfo field => [field.FieldType],
            MethodInfo method => [method.ReturnType, .. method.GetParameters().Select(parameter => parameter.ParameterType)],
            ConstructorInfo constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType),
            _ => []
        };
    }

    private static SaleProductSnapshot CreateSnapshot(
        long id,
        string reference,
        string name,
        decimal unitPrice)
    {
        return SaleProductSnapshot.Create(
            ProductId.Create(id).Value,
            ProductReference.Create(reference).Value,
            name,
            "Conditionnement synthétique",
            Money.Create(unitPrice).Value).Value;
    }

    private static SaleDraft CreateDraft()
    {
        return SaleDraft.Create(SaleDraftId.Create(Guid.Parse("f0a4a80a-8411-40bd-b0d9-85bed8977548")).Value);
    }

    private static SaleDraft CreateDraftWithTwoProducts()
    {
        var draft = CreateDraft();
        draft.AddItem(
            CreateSnapshot(1, "DEMO-001", "Produit synthétique A", 2m),
            SaleQuantity.One);
        draft.AddItem(
            CreateSnapshot(2, "DEMO-002", "Produit synthétique B", 2.5m),
            SaleQuantity.Create(2).Value);
        return draft;
    }
}

public sealed class SalesApplicationContractTests
{
    private static readonly Type[] ContractTypes =
    [
        typeof(IProductCatalogReader),
        typeof(IStockAvailabilityReader),
        typeof(ISaleFinalizer)
    ];

    [Fact]
    public void Check49_ApplicationReferencesDomain()
    {
        var references = typeof(IProductCatalogReader).Assembly.GetReferencedAssemblies();

        Assert.Contains(references, reference => reference.Name == "Pharmacy.Domain");
    }

    [Fact]
    public void Check50_RequiredApplicationInterfacesArePresent()
    {
        Assert.All(ContractTypes, contract => Assert.True(contract.IsInterface));
        Assert.Equal(3, ContractTypes.Length);
    }

    [Fact]
    public void Check51_ApplicationContractsDoNotExposeIQueryable()
    {
        Assert.DoesNotContain(ContractSurfaceTypes(), type =>
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IQueryable<>));
    }

    [Fact]
    public void Check52_ApplicationContractsDoNotExposeDesktopTypes()
    {
        Assert.DoesNotContain(
            ContractSurfaceTypes(),
            type => type.Namespace?.StartsWith("Pharmacy.Desktop", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Check53_ApplicationContractsDoNotExposeDbContext()
    {
        Assert.DoesNotContain(
            ContractSurfaceTypes(),
            type => type.Name.Contains("DbContext", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Check54_ApplicationContractsContainNoConnectionString()
    {
        var memberNames = ContractTypes
            .SelectMany(type => type.GetMembers())
            .Select(member => member.Name);

        Assert.DoesNotContain(memberNames, name =>
            name.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<Type> ContractSurfaceTypes()
    {
        return ContractTypes
            .SelectMany(type => type.GetMethods())
            .SelectMany(method => new[] { method.ReturnType }
                .Concat(method.GetParameters().Select(parameter => parameter.ParameterType)))
            .SelectMany(FlattenType)
            .Distinct();
    }

    private static IEnumerable<Type> FlattenType(Type type)
    {
        yield return type;

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var nested in FlattenType(argument))
            {
                yield return nested;
            }
        }
    }
}
