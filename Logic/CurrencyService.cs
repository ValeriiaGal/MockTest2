using Azure.Identity;
using Microsoft.IdentityModel.Tokens;

using MockTest;
using Microsoft.Data.SqlClient;

public class CurrencyService : ICurrencyService
{
    private readonly string _connectionString;

    public CurrencyService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<bool> AddCurrency(CurrencyRequestDTO request)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var transaction = conn.BeginTransaction();

        try
        {
            // Validate countries and collect their IDs
            var countryIds = new List<int>();
            foreach (var country in request.Countries)
            {
                var cmd = new SqlCommand("SELECT Id FROM Country WHERE Name = @Name", conn, transaction);
                cmd.Parameters.AddWithValue("@Name", country.Name);
                var result = await cmd.ExecuteScalarAsync();
                if (result == null)
                    throw new Exception($"Country '{country.Name}' doesn't exist.");
                countryIds.Add(Convert.ToInt32(result));
            }

            // Check if currency exists
            var getCurrencyIdCmd = new SqlCommand("SELECT Id FROM Currency WHERE Name = @Name", conn, transaction);
            getCurrencyIdCmd.Parameters.AddWithValue("@Name", request.CurrencyName);
            var currencyIdObj = await getCurrencyIdCmd.ExecuteScalarAsync();

            int currencyId;

            if (currencyIdObj != null)
            {
                currencyId = Convert.ToInt32(currencyIdObj);
                var updateCmd = new SqlCommand("UPDATE Currency SET Rate = @Rate WHERE Id = @Id", conn, transaction);
                updateCmd.Parameters.AddWithValue("@Rate", request.Rate);
                updateCmd.Parameters.AddWithValue("@Id", currencyId);
                await updateCmd.ExecuteNonQueryAsync();
            }
            else
            {
                var insertCmd = new SqlCommand(
                    "INSERT INTO Currency (Name, Rate) VALUES (@Name, @Rate); SELECT SCOPE_IDENTITY();",
                    conn, transaction);
                insertCmd.Parameters.AddWithValue("@Name", request.CurrencyName);
                insertCmd.Parameters.AddWithValue("@Rate", request.Rate);
                var inserted = await insertCmd.ExecuteScalarAsync();
                if (inserted == null)
                    throw new Exception("Failed to insert new currency.");
                currencyId = Convert.ToInt32(inserted);
            }

            // Link currency to countries
            foreach (var countryId in countryIds)
            {
                var checkCmd = new SqlCommand(
                    "SELECT 1 FROM Currency_Country WHERE Country_Id = @CountryId AND Currency_Id = @CurrencyId",
                    conn, transaction);
                checkCmd.Parameters.AddWithValue("@CountryId", countryId);
                checkCmd.Parameters.AddWithValue("@CurrencyId", currencyId);
                var exists = await checkCmd.ExecuteScalarAsync();

                if (exists == null)
                {
                    var insertMap = new SqlCommand(
                        "INSERT INTO Currency_Country (Country_Id, Currency_Id) VALUES (@CountryId, @CurrencyId)",
                        conn, transaction);
                    insertMap.Parameters.AddWithValue("@CountryId", countryId);
                    insertMap.Parameters.AddWithValue("@CurrencyId", currencyId);
                    await insertMap.ExecuteNonQueryAsync();
                }
            }

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
        finally
        {
            await conn.CloseAsync();
        }
    }

    public async Task<object?> SearchCurrency(string type, string query)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        if (type == "Country")
        {
            const string sql = @"
                SELECT cur.Name, cur.Rate
                FROM Currency cur
                JOIN Currency_Country cc ON cur.Id = cc.Currency_Id
                JOIN Country c ON c.Id = cc.Country_Id
                WHERE c.Name = @query";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@query", query);
            var reader = await cmd.ExecuteReaderAsync();

            var currencies = new List<object>();
            while (await reader.ReadAsync())
            {
                currencies.Add(new
                {
                    Name = reader.GetString(0),
                    Rate = reader.GetFloat(1)
                });
            }

            await reader.CloseAsync();
            return new { Name = query, Currencies = currencies };
        }
        else if (type == "Currency")
        {
            const string sql = @"
                SELECT c.Id, c.Name
                FROM Country c
                JOIN Currency_Country cc ON c.Id = cc.Country_Id
                JOIN Currency cur ON cur.Id = cc.Currency_Id
                WHERE cur.Name = @query";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@query", query);
            var reader = await cmd.ExecuteReaderAsync();

            var countries = new List<Country>();
            while (await reader.ReadAsync())
            {
                countries.Add(new Country
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }

            await reader.CloseAsync();
            return countries.Count > 0 ? countries : null;
        }
        else
        {
            throw new ArgumentException("Invalid type. Expected: 'Country' or 'Currency'");
        }
    }
}