using APBD9.Models.DTOs;
using Microsoft.Data.SqlClient;

namespace APBD9.Services;

public class WarehouseService : IWarehouseService
{
    private readonly string _connectionString;

    public WarehouseService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    public async Task<int> HandleOrder(ProductWarehouseDTO productWarehouseDto)
    {
        if (!await ProductExists(productWarehouseDto.IdProduct))
        {
            throw new ArgumentException("Product does not exist");
        }

        if (!await WarehouseExists(productWarehouseDto.IdWarehouse))
        {
            throw new ArgumentException("Warehouse does not exist");
        }

        var idOrder = await ValidOrderExists(productWarehouseDto.IdProduct,
            productWarehouseDto.Amount,
            productWarehouseDto.CreatedAt);
        if (idOrder == null)
        {
            throw new ArgumentException("Valid order does not exist");
        }

        if (await WasFulfilled(idOrder.Value))
        {
            throw new ArgumentException("Order has been already fulfilled");
        }

        return await AddNewOrder(productWarehouseDto, idOrder.Value);
    }

    private async Task<Boolean> ProductExists(int productId)
    {
        string query = "SELECT 1 FROM Product WHERE IdProduct = @PRODUCTID";
        using (SqlConnection connection = new SqlConnection(_connectionString))
        using (SqlCommand command = new SqlCommand(query, connection))
        {
            command.Parameters.AddWithValue("@PRODUCTID", productId);
            await connection.OpenAsync();
            using (var reader = await command.ExecuteReaderAsync())
            {
                return await reader.ReadAsync();
            }
        }
    }

    private async Task<Boolean> WarehouseExists(int warehouseId)
    {
        string query = "SELECT 1 FROM Warehouse WHERE IdWarehouse = @WAREHOUSEID";
        using (SqlConnection connection = new SqlConnection(_connectionString))
        using (SqlCommand command = new SqlCommand(query, connection))
        {
            command.Parameters.AddWithValue("@WAREHOUSEID", warehouseId);
            await connection.OpenAsync();
            using (var reader = await command.ExecuteReaderAsync())
            {
                return await reader.ReadAsync();
            }
        }
    }

    private async Task<int?> ValidOrderExists(int productId, int amount, DateTime date)
    {
        string query =
            "SELECT IdOrder FROM [Order] WHERE IdProduct = @PRODUCTID AND Amount = @AMOUNT AND CreatedAt < @CREATEDAT";
        using (SqlConnection connection = new SqlConnection(_connectionString))
        using (SqlCommand command = new SqlCommand(query, connection))
        {
            command.Parameters.AddWithValue("@PRODUCTID", productId);
            command.Parameters.AddWithValue("@AMOUNT", amount);
            command.Parameters.AddWithValue("@CREATEDAT", date);
            await connection.OpenAsync();
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    int? orderId = reader.GetInt32(reader.GetOrdinal("IdOrder"));
                    return orderId;
                }
            }

            return null;
        }
    }

    private async Task<Boolean> WasFulfilled(int orderId)
    {
        string query =
            "SELECT 1 FROM Product_Warehouse where IdOrder = @ORDERID";
        using (SqlConnection connection = new SqlConnection(_connectionString))
        using (SqlCommand command = new SqlCommand(query, connection))
        {
            command.Parameters.AddWithValue("@ORDERID", orderId);
            await connection.OpenAsync();
            using (var reader = await command.ExecuteReaderAsync())
            {
                return await reader.ReadAsync();
            }
        }
    }

    private async Task<decimal?> GetProductPrice(int productId)
    {
        string query = "SELECT Price FROM Product WHERE IdProduct = @PRODUCTID";
        using (SqlConnection connection = new SqlConnection(_connectionString))
        using (SqlCommand command = new SqlCommand(query, connection))
        {
            command.Parameters.AddWithValue("@PRODUCTID", productId);
            await connection.OpenAsync();
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var price = reader.GetDecimal(reader.GetOrdinal("Price"));
                    return price;
                }
            }
        }

        return null;
    }

    private async Task<int> AddNewOrder(ProductWarehouseDTO productWarehouseDto, int orderId)
    {
        string query1 = "UPDATE [Order] SET FulfilledAt = @DATENOW WHERE IdOrder = @ORDERID";

        string query2 =
            "INSERT INTO Product_Warehouse (IdWarehouse, IdProduct, IdOrder, Amount, Price, CreatedAt) " +
            "VALUES (@IDWAREHOUSE, @IDPRODUCT, @IDORDER, @AMOUNT, @PRICE, @CREATEDAT); " +
            "SELECT SCOPE_IDENTITY();";

        await using SqlConnection connection = new SqlConnection(_connectionString);
        await using SqlCommand command = new SqlCommand();

        command.Connection = connection;
        await connection.OpenAsync();

        var transaction = await connection.BeginTransactionAsync();
        command.Transaction = transaction as SqlTransaction;

        try
        {
            command.CommandText = query1;
            command.Parameters.AddWithValue("@DATENOW", DateTime.Now);
            command.Parameters.AddWithValue("@ORDERID", orderId);
            await command.ExecuteNonQueryAsync();

            command.Parameters.Clear();
            command.CommandText = query2;
            command.Parameters.AddWithValue("@IDWAREHOUSE", productWarehouseDto.IdWarehouse);
            command.Parameters.AddWithValue("@IDPRODUCT", productWarehouseDto.IdProduct);
            command.Parameters.AddWithValue("@IDORDER", orderId);
            command.Parameters.AddWithValue("@AMOUNT", productWarehouseDto.Amount);
            command.Parameters.AddWithValue("@PRICE",
                await GetProductPrice(productWarehouseDto.IdProduct) * productWarehouseDto.Amount);
            command.Parameters.AddWithValue("@CREATEDAT", DateTime.Now);

            var result = await command.ExecuteScalarAsync();
            int generatedId = Convert.ToInt32(result);

            await transaction.CommitAsync();

            return generatedId;
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<int> HandleOrderWithProcedure(ProductWarehouseDTO productWarehouseDto)
    {
        try
        {
            await using (SqlConnection connection = new SqlConnection(_connectionString))
            await using (SqlCommand command = new SqlCommand("AddProductToWarehouse", connection))
            {
                command.CommandType = System.Data.CommandType.StoredProcedure;

                command.Parameters.AddWithValue("@IdProduct", productWarehouseDto.IdProduct);
                command.Parameters.AddWithValue("@IdWarehouse", productWarehouseDto.IdWarehouse);
                command.Parameters.AddWithValue("@Amount", productWarehouseDto.Amount);
                command.Parameters.AddWithValue("@CreatedAt", productWarehouseDto.CreatedAt);

                await connection.OpenAsync();

                try
                {
                    var result = await command.ExecuteScalarAsync();

                    if (result != null && result != DBNull.Value)
                    {
                        return Convert.ToInt32(result);
                    }

                    throw new Exception("Failed to get new product warehouse ID");
                }
                catch (SqlException ex)
                {
                    if (ex.Message.Contains("IdProduct does not exist"))
                    {
                        throw new ArgumentException("Product does not exist");
                    }

                    if (ex.Message.Contains("no order to fullfill"))
                    {
                        throw new ArgumentException("Valid order does not exist");
                    }

                    if (ex.Message.Contains("IdWarehouse does not exist"))
                    {
                        throw new ArgumentException("Warehouse does not exist");
                    }

                    throw new Exception("Database error: " + ex.Message);
                }
            }
        }
        catch (SqlException ex)
        {
            throw new Exception("Database error: " + ex.Message);
        }
    }
}