// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests;

public class SqlitePrecisionTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Specification_store_preserves_fifteen_digit_real_to_text_conversion(bool async)
    {
        var options = new DbContextOptionsBuilder()
            .UseSqlite("Data Source=:memory:;Pooling=False")
            .AddInterceptors(SqliteLegacyPrecisionInterceptor.Instance)
            .Options;
        await using var context = new DbContext(options);

        if (async)
        {
            await context.Database.OpenConnectionAsync();
        }
        else
        {
            context.Database.OpenConnection();
        }

        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT CAST(1.0 / 3 AS TEXT)";
        object? result = async ? await command.ExecuteScalarAsync() : command.ExecuteScalar();

        Assert.Equal("0.333333333333333", result);
    }

    [Fact]
    public void Unconfigured_connections_retain_the_native_precision()
    {
        var options = new DbContextOptionsBuilder()
            .UseSqlite("Data Source=:memory:;Pooling=False")
            .Options;
        using var context = new DbContext(options);
        context.Database.OpenConnection();

        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT CAST(1.0 / 3 AS TEXT)";

        // Verify the default's 17 significant digits independently of its final rounding digit.
        string result = Assert.IsType<string>(command.ExecuteScalar());
        Assert.Equal(19, result.Length);
        Assert.StartsWith("0.333333333333333", result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Direct_comparison_connections_preserve_precision_after_reopening(bool async)
    {
        await using var store = new SqliteInfoCarrierBackendTestStore(
            nameof(SqlitePrecisionTest), shared: false,
            new SharedTestStoreProperties { ContextType = typeof(DbContext) });
        await using var connection = store.CreateStoreConnection();

        for (int attempt = 0; attempt < 2; attempt++)
        {
            if (async)
            {
                await connection.OpenAsync();
            }
            else
            {
                connection.Open();
            }

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT CAST(1.0 / 3 AS TEXT)";
            object? result = async ? await command.ExecuteScalarAsync() : command.ExecuteScalar();
            Assert.Equal("0.333333333333333", result);

            if (async)
            {
                await connection.CloseAsync();
            }
            else
            {
                connection.Close();
            }
        }
    }
}
