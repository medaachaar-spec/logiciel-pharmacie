using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Catalog.Commands;
using Pharmacy.Application.Inventory.Commands;
using Pharmacy.Application.Purchasing.Commands;
using Pharmacy.Application.Purchasing.Contracts;
using Pharmacy.Application.Purchasing.Queries;
using Pharmacy.Domain.Purchasing;
using Pharmacy.Infrastructure.Catalog;
using Pharmacy.Infrastructure.Inventory;
using Pharmacy.Infrastructure.Persistence;
using Pharmacy.Infrastructure.Purchasing;
using Xunit;

namespace Pharmacy.IntegrationTests;

[Collection("Sprint 09 persistence serial")]
public sealed class SqlPurchasingIntegrationTests
{
    private const string Prefix = "Pharmacy_Sprint12_Test_";

    [Fact]
    public async Task RealSqlPurchasingValidatesSuppliersOrdersReceiptsStockAndIdempotence()
    {
        var token = TestContext.Current.CancellationToken; var server = ServerConnection(); var databaseName = Prefix + Guid.NewGuid().ToString("N")[..12]; ValidateName(databaseName);
        var database = new SqlConnectionStringBuilder(server.ConnectionString) { InitialCatalog=databaseName, ApplicationName="Pharmacy.Sprint12.Tests", Pooling=false };
        await CreateDatabaseAsync(server.ConnectionString,databaseName);
        try
        {
            var factory=new TestFactory(database.ConnectionString);
            await using(var db=await factory.CreateDbContextAsync(token)){await db.Database.MigrateAsync(token);Assert.Empty(await db.Database.GetPendingMigrationsAsync(token));Assert.Contains(await db.Database.GetAppliedMigrationsAsync(token),x=>x.EndsWith("_PurchasingPersistenceV01",StringComparison.Ordinal));}
            Assert.Equal(5,await ScalarAsync<int>(database.ConnectionString,"SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name=N'purchasing';"));
            Assert.Equal(0,await ScalarAsync<int>(database.ConnectionString,"SELECT COUNT(*) FROM sys.foreign_keys fk JOIN sys.tables t ON t.object_id=fk.parent_object_id JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name=N'purchasing' AND fk.delete_referential_action<>0;"));

            var catalog=new SqlCatalogStore(factory); var category=await catalog.CreateAsync(new CreateCatalogCategoryCommand("S12-TEST","Catégorie Purchasing",DefaultTracksLots:true,DefaultTracksExpiry:true),token); Assert.True(category.IsSuccess);
            var lotProduct=await new CreateCatalogProductHandler(catalog).HandleAsync(new("Produit réception lot","Boîte test",category.Value.CategoryId,"S12-PUR-LOT",20m,TracksLots:true,TracksExpiry:true),token); Assert.True(lotProduct.IsSuccess);
            var plainProduct=await new CreateCatalogProductHandler(catalog).HandleAsync(new("Produit réception direct","Unité test",category.Value.CategoryId,"S12-PUR-DIRECT",33m,TracksLots:false,TracksExpiry:false),token); Assert.True(plainProduct.IsSuccess);
            var clock=new FixedClock(new DateTime(2026,8,10,9,30,0,DateTimeKind.Utc)); var store=new SqlPurchasingStore(factory,clock); var commands=new PurchasingCommandHandlers(store); var queries=new PurchasingQueryHandlers(new SqlPurchasingReader(factory));
            var inventoryCommands=new InventoryCommandHandlers(new SqlInventoryStore(factory),new FixedInventoryDate(new DateOnly(2026,8,10))); var location=await inventoryCommands.CreateLocationAsync(new(Guid.NewGuid(),"S12-RECV","Réception Sprint 12",true),token); Assert.True(location.IsSuccess);

            var supplier=await commands.CreateSupplierAsync(new("s12-supplier","Fournisseur test","0500000000","supplier@example.invalid","Adresse synthétique"),token); Assert.True(supplier.IsSuccess); Assert.Equal("S12-SUPPLIER",supplier.Value.Code); Assert.Equal(8,Convert.FromBase64String(supplier.Value.RowVersion).Length);
            Assert.Equal("purchasing.supplier.code_duplicate",(await commands.CreateSupplierAsync(new("S12-SUPPLIER","Doublon"),token)).Error.Code);
            var staleSupplier=supplier.Value; var updated=await commands.UpdateSupplierAsync(new(supplier.Value.SupplierId,"S12-SUPPLIER","Fournisseur renommé",null,null,null,supplier.Value.RowVersion),token); Assert.True(updated.IsSuccess);
            Assert.Equal("purchasing.supplier.concurrency_conflict",(await commands.UpdateSupplierAsync(new(staleSupplier.SupplierId,staleSupplier.Code,"Écriture périmée",null,null,null,staleSupplier.RowVersion),token)).Error.Code);
            var inactive=await commands.SetSupplierActivationAsync(new(updated.Value.SupplierId,false,updated.Value.RowVersion),token); Assert.False(inactive.Value.IsActive);
            Assert.Equal("purchasing.supplier.inactive",(await commands.CreateOrderAsync(new(inactive.Value.SupplierId,"S12-PO-INACTIVE",clock.CurrentDate),token)).Error.Code);
            supplier=await commands.SetSupplierActivationAsync(new(inactive.Value.SupplierId,true,inactive.Value.RowVersion),token); Assert.True(supplier.IsSuccess);

            var order=await commands.CreateOrderAsync(new(supplier.Value.SupplierId,"S12-PO-001",clock.CurrentDate,clock.CurrentDate.AddDays(5),"Commande test"),token); Assert.True(order.IsSuccess); Assert.Equal(PurchaseOrderStatus.Draft,order.Value.Status);
            Assert.Equal("purchasing.order.empty",(await commands.ConfirmOrderAsync(new(order.Value.OrderId,order.Value.RowVersion),token)).Error.Code);
            Assert.Equal("purchasing.order.quantity_invalid",(await commands.AddOrderLineAsync(new(order.Value.OrderId,lotProduct.Value.ProductId,0,1m,order.Value.RowVersion),token)).Error.Code);
            Assert.Equal("purchasing.receipt.product_not_found",(await commands.AddOrderLineAsync(new(order.Value.OrderId,long.MaxValue,1,1m,order.Value.RowVersion),token)).Error.Code);
            var staleOrderView=order.Value;
            order=await commands.AddOrderLineAsync(new(order.Value.OrderId,lotProduct.Value.ProductId,6,9.1250m,order.Value.RowVersion),token); Assert.True(order.IsSuccess);
            Assert.Equal("purchasing.order.concurrency_conflict",(await commands.AddOrderLineAsync(new(staleOrderView.OrderId,lotProduct.Value.ProductId,1,9m,staleOrderView.RowVersion),token)).Error.Code);
            order=await commands.AddOrderLineAsync(new(order.Value.OrderId,lotProduct.Value.ProductId,4,null,order.Value.RowVersion),token); Assert.Single(order.Value.Lines); Assert.Equal(10,order.Value.Lines[0].OrderedQuantity); Assert.Equal(9.1250m,order.Value.Lines[0].UnitPurchasePrice);
            var editable=order.Value.Lines[0]; order=await commands.UpdateOrderLineAsync(new(order.Value.OrderId,editable.LineId,12,9.5000m,order.Value.RowVersion,editable.RowVersion),token); Assert.Equal(12,order.Value.Lines[0].OrderedQuantity);
            var stockBefore=await ScalarAsync<int>(database.ConnectionString,"SELECT COALESCE(SUM(AvailableQuantity),0) FROM inventory.StockBalances WHERE ProductId=@p;",new SqlParameter("@p",SqlDbType.BigInt){Value=lotProduct.Value.ProductId});
            order=await commands.ConfirmOrderAsync(new(order.Value.OrderId,order.Value.RowVersion),token); Assert.True(order.IsSuccess); Assert.Equal(PurchaseOrderStatus.Confirmed,order.Value.Status);
            Assert.Equal(stockBefore,await ScalarAsync<int>(database.ConnectionString,"SELECT COALESCE(SUM(AvailableQuantity),0) FROM inventory.StockBalances WHERE ProductId=@p;",new SqlParameter("@p",SqlDbType.BigInt){Value=lotProduct.Value.ProductId}));
            Assert.Equal("purchasing.order.invalid_status",(await commands.AddOrderLineAsync(new(order.Value.OrderId,plainProduct.Value.ProductId,1,1m,order.Value.RowVersion),token)).Error.Code);

            var removable=await commands.CreateOrderAsync(new(supplier.Value.SupplierId,"S12-PO-REMOVE",clock.CurrentDate),token); removable=await commands.AddOrderLineAsync(new(removable.Value.OrderId,plainProduct.Value.ProductId,2,2m,removable.Value.RowVersion),token); var removed=await commands.RemoveOrderLineAsync(new(removable.Value.OrderId,removable.Value.Lines[0].LineId,removable.Value.RowVersion,removable.Value.Lines[0].RowVersion),token); Assert.Empty(removed.Value.Lines); var cancelled=await commands.CancelOrderAsync(new(removed.Value.OrderId,"Annulation contrôlée test",removed.Value.RowVersion),token); Assert.Equal(PurchaseOrderStatus.Cancelled,cancelled.Value.Status);

            var firstOperation=Guid.NewGuid(); var firstCommand=new FinalizeGoodsReceiptCommand(firstOperation,supplier.Value.SupplierId,order.Value.OrderId,location.Value.LocationId,"BL-S12-001",clock.CurrentDate,order.Value.RowVersion,[new(lotProduct.Value.ProductId,order.Value.Lines[0].LineId,5,8.7500m,null,"LOT-S12-A",clock.CurrentDate.AddYears(2))]);
            var first=await commands.FinalizeReceiptAsync(firstCommand,token); Assert.True(first.IsSuccess,first.IsFailure?first.Error.Code:null); Assert.Equal(PurchaseOrderStatus.PartiallyReceived,(await queries.GetOrderAsync(order.Value.OrderId,token)).Value.Status); Assert.Equal(5,(await queries.GetOrderAsync(order.Value.OrderId,token)).Value.Lines[0].ReceivedQuantity); Assert.Equal(8.7500m,first.Value.Lines[0].UnitPurchasePrice);
            Assert.Equal(stockBefore+5,await ScalarAsync<int>(database.ConnectionString,"SELECT SUM(AvailableQuantity) FROM inventory.StockBalances WHERE ProductId=@p;",new SqlParameter("@p",SqlDbType.BigInt){Value=lotProduct.Value.ProductId}));
            Assert.Equal(1,await ScalarAsync<int>(database.ConnectionString,"SELECT COUNT(*) FROM inventory.StockMovements WHERE SourceType=N'GoodsReceipt' AND SourceReference=@r;",new SqlParameter("@r",SqlDbType.NVarChar,64){Value=first.Value.ReceiptNumber}));
            Assert.Equal(20m,await ScalarAsync<decimal>(database.ConnectionString,"SELECT CurrentSalePrice FROM catalog.ProductLocalSettings WHERE ProductId=@p;",new SqlParameter("@p",SqlDbType.BigInt){Value=lotProduct.Value.ProductId}));

            var replay=await commands.FinalizeReceiptAsync(firstCommand,token); Assert.True(replay.IsSuccess); Assert.Equal(first.Value.ReceiptId,replay.Value.ReceiptId); Assert.Equal(1,await ScalarAsync<int>(database.ConnectionString,"SELECT COUNT(*) FROM purchasing.GoodsReceipts WHERE OperationId=@o;",new SqlParameter("@o",SqlDbType.UniqueIdentifier){Value=firstOperation}));
            Assert.Equal("purchasing.receipt.operation_conflict",(await commands.FinalizeReceiptAsync(firstCommand with{DeliveryNoteNumber="BL-DIFFERENT"},token)).Error.Code);

            var currentOrder=(await queries.GetOrderAsync(order.Value.OrderId,token)).Value; var countsBefore=await Counts(database.ConnectionString);
            var tooMuch=await commands.FinalizeReceiptAsync(firstCommand with{OperationId=Guid.NewGuid(),DeliveryNoteNumber="BL-S12-OVER",ExpectedOrderRowVersion=currentOrder.RowVersion,Lines=[firstCommand.Lines[0] with{Quantity=8}]},token); Assert.Equal("purchasing.receipt.quantity_exceeds_order",tooMuch.Error.Code); Assert.Equal(countsBefore,await Counts(database.ConnectionString));
            var duplicate=await commands.FinalizeReceiptAsync(firstCommand with{OperationId=Guid.NewGuid(),ExpectedOrderRowVersion=currentOrder.RowVersion},token); Assert.Equal("purchasing.receipt.duplicate_delivery_note",duplicate.Error.Code); Assert.Equal(countsBefore,await Counts(database.ConnectionString));
            var invalidExpiry=await commands.FinalizeReceiptAsync(firstCommand with{OperationId=Guid.NewGuid(),DeliveryNoteNumber="BL-S12-EXPIRED",ExpectedOrderRowVersion=currentOrder.RowVersion,Lines=[firstCommand.Lines[0] with{Quantity=1,LotNumber="LOT-EXPIRED",ExpiryDate=clock.CurrentDate.AddDays(-1)}]},token); Assert.Equal("purchasing.receipt.invalid_expiry",invalidExpiry.Error.Code); Assert.Equal(countsBefore,await Counts(database.ConnectionString));
            Assert.Equal("purchasing.receipt.supplier_not_found",(await commands.FinalizeReceiptAsync(new(Guid.NewGuid(),long.MaxValue,null,location.Value.LocationId,"BL-S12-NO-SUPPLIER",clock.CurrentDate,null,[new(plainProduct.Value.ProductId,null,1,1m,null,null,null)]),token)).Error.Code);
            Assert.Equal("purchasing.receipt.product_not_found",(await commands.FinalizeReceiptAsync(new(Guid.NewGuid(),supplier.Value.SupplierId,null,location.Value.LocationId,"BL-S12-NO-PRODUCT",clock.CurrentDate,null,[new(long.MaxValue,null,1,1m,null,null,null)]),token)).Error.Code);
            Assert.Equal("purchasing.receipt.invalid_lot",(await commands.FinalizeReceiptAsync(new(Guid.NewGuid(),supplier.Value.SupplierId,null,location.Value.LocationId,"BL-S12-BAD-BATCH",clock.CurrentDate,null,[new(plainProduct.Value.ProductId,null,1,1m,first.Value.Lines[0].StockBatchId,null,null)]),token)).Error.Code);
            Assert.Equal(countsBefore,await Counts(database.ConnectionString));

            var secondCommand=firstCommand with{OperationId=Guid.NewGuid(),DeliveryNoteNumber="BL-S12-002",ExpectedOrderRowVersion=currentOrder.RowVersion,Lines=[firstCommand.Lines[0] with{Quantity=7}]}; var completed=await commands.FinalizeReceiptAsync(secondCommand,token); Assert.True(completed.IsSuccess,completed.IsFailure?completed.Error.Code:null); var finalOrder=(await queries.GetOrderAsync(order.Value.OrderId,token)).Value; Assert.Equal(PurchaseOrderStatus.FullyReceived,finalOrder.Status); Assert.Equal(12,finalOrder.Lines[0].ReceivedQuantity);

            var direct=await commands.FinalizeReceiptAsync(new(Guid.NewGuid(),supplier.Value.SupplierId,null,location.Value.LocationId,"BL-S12-DIRECT",clock.CurrentDate,null,[new(plainProduct.Value.ProductId,null,3,15.2500m,null,null,null)]),token); Assert.True(direct.IsSuccess,direct.IsFailure?direct.Error.Code:null); Assert.Equal(GoodsReceiptType.Direct,direct.Value.ReceiptType); Assert.Null(direct.Value.Lines[0].StockBatchId); Assert.Equal(3,await ScalarAsync<int>(database.ConnectionString,"SELECT SUM(AvailableQuantity) FROM inventory.StockBalances WHERE ProductId=@p;",new SqlParameter("@p",SqlDbType.BigInt){Value=plainProduct.Value.ProductId}));

            var referencedDelete=await Assert.ThrowsAsync<SqlException>(()=>ExecuteAsync(database.ConnectionString,"DELETE FROM purchasing.Suppliers WHERE Id=@id;",new SqlParameter("@id",SqlDbType.BigInt){Value=supplier.Value.SupplierId})); Assert.Equal(547,referencedDelete.Number);
            var diagnostics=await queries.GetDiagnosticsAsync(token); Assert.True(diagnostics.IsReachable); Assert.Equal(1,diagnostics.SupplierCount); Assert.Equal(3,diagnostics.ReceiptCount);
        }
        finally { SqlConnection.ClearAllPools(); await DropDatabaseAsync(server.ConnectionString,databaseName); }
    }

    private static async Task<(int Receipts,int Lines,int Movements,int Balances)> Counts(string cs)=>(await ScalarAsync<int>(cs,"SELECT COUNT(*) FROM purchasing.GoodsReceipts;"),await ScalarAsync<int>(cs,"SELECT COUNT(*) FROM purchasing.GoodsReceiptLines;"),await ScalarAsync<int>(cs,"SELECT COUNT(*) FROM inventory.StockMovements;"),await ScalarAsync<int>(cs,"SELECT COUNT(*) FROM inventory.StockBalances;"));
    private static SqlConnectionStringBuilder ServerConnection(){var raw=Environment.GetEnvironmentVariable("PHARMACY_TEST_SQLSERVER_CONNECTION");if(string.IsNullOrWhiteSpace(raw))throw new InvalidOperationException("PHARMACY_TEST_SQLSERVER_CONNECTION est requise.");return new(raw){InitialCatalog="master",ApplicationName="Pharmacy.Sprint12.Tests",Pooling=false};}
    private static async Task CreateDatabaseAsync(string cs,string name){ValidateName(name);await using var c=new SqlConnection(cs);await c.OpenAsync();await using var cmd=c.CreateCommand();cmd.CommandText=$"CREATE DATABASE {Quote(name)};";await cmd.ExecuteNonQueryAsync();}
    private static async Task DropDatabaseAsync(string cs,string name){ValidateName(name);await using var c=new SqlConnection(cs);await c.OpenAsync();await using var cmd=c.CreateCommand();cmd.CommandText=$"IF DB_ID(@n) IS NOT NULL BEGIN ALTER DATABASE {Quote(name)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {Quote(name)}; END;";cmd.Parameters.Add(new SqlParameter("@n",SqlDbType.NVarChar,128){Value=name});await cmd.ExecuteNonQueryAsync();}
    private static async Task<T> ScalarAsync<T>(string cs,string sql,params SqlParameter[] parameters){await using var c=new SqlConnection(cs);await c.OpenAsync();await using var cmd=c.CreateCommand();cmd.CommandText=sql;cmd.Parameters.AddRange(parameters);var value=await cmd.ExecuteScalarAsync()??throw new InvalidOperationException("Valeur SQL absente.");return (T)Convert.ChangeType(value,typeof(T),System.Globalization.CultureInfo.InvariantCulture);}
    private static async Task ExecuteAsync(string cs,string sql,params SqlParameter[] parameters){await using var c=new SqlConnection(cs);await c.OpenAsync();await using var cmd=c.CreateCommand();cmd.CommandText=sql;cmd.Parameters.AddRange(parameters);await cmd.ExecuteNonQueryAsync();}
    private static string Quote(string name)=>$"[{name.Replace("]","]]",StringComparison.Ordinal)}]";
    private static void ValidateName(string name){if(!Regex.IsMatch(name,"^Pharmacy_Sprint12_Test_[0-9a-f]{12}$",RegexOptions.CultureInvariant))throw new InvalidOperationException("Nom de base temporaire Sprint 12 refusé.");}
    private sealed class FixedClock(DateTime utcNow):IPurchasingClock{public DateTime UtcNow{get;}=utcNow;public DateOnly CurrentDate=>DateOnly.FromDateTime(UtcNow);}
    private sealed class FixedInventoryDate(DateOnly date):Pharmacy.Application.Inventory.Contracts.IInventoryDateProvider{public DateOnly CurrentDate{get;}=date;}
    private sealed class TestFactory:IDbContextFactory<PharmacyDbContext>{private readonly DbContextOptions<PharmacyDbContext> options;public TestFactory(string cs){var builder=new DbContextOptionsBuilder<PharmacyDbContext>();builder.UsePharmacySqlServer(cs);options=builder.Options;}public PharmacyDbContext CreateDbContext()=>new(options);public Task<PharmacyDbContext>CreateDbContextAsync(CancellationToken cancellationToken=default){cancellationToken.ThrowIfCancellationRequested();return Task.FromResult(CreateDbContext());}}
}
