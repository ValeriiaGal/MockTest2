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
    using (var conn = new SqlConnection(_connectionString))
    {
        await conn.OpenAsync();
        var transaction = conn.BeginTransaction();

        try
        {
            // Validate all countries exist and retrieve their IDs
            var countryIds = new Dictionary<string, int>();
            foreach (var country in request.Countries)
            {
                var sqlCountryId = "SELECT Id FROM Country WHERE Name = @CountryName";
                using var cmd = new SqlCommand(sqlCountryId, conn, transaction);
                cmd.Parameters.AddWithValue("@CountryName", country.Name);
                var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    await reader.CloseAsync();
                    throw new Exception($"Country '{country.Name}' doesn't exist.");
                }

                int countryId = reader.GetInt32(0);
                countryIds[country.Name] = countryId;
                await reader.CloseAsync();
            }

            // Check if currency exists
            int currencyId;
            var checkCurrencySql = "SELECT Id FROM Currency WHERE Name = @CurrencyName";
            using (var cmd = new SqlCommand(checkCurrencySql, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@CurrencyName", request.CurrencyName);
                var result = await cmd.ExecuteScalarAsync();

                if (result != null)
                {
                    // Currency exists – update
                    currencyId = Convert.ToInt32(result);
                    var updateSql = "UPDATE Currency SET Rate = @Rate WHERE Id = @Id";
                    using var updateCmd = new SqlCommand(updateSql, conn, transaction);
                    updateCmd.Parameters.AddWithValue("@Rate", request.Rate);
                    updateCmd.Parameters.AddWithValue("@Id", currencyId);

                    if (await updateCmd.ExecuteNonQueryAsync() == 0)
                        throw new Exception("Failed to update currency rate.");
                }
                else
                {
                    // Currency does not exist – insert
                    var insertSql = "INSERT INTO Currency (Name, Rate) VALUES (@Name, @Rate); SELECT SCOPE_IDENTITY();";
                    using var insertCmd = new SqlCommand(insertSql, conn, transaction);
                    insertCmd.Parameters.AddWithValue("@Name", request.CurrencyName);
                    insertCmd.Parameters.AddWithValue("@Rate", request.Rate);

                    var inserted = await insertCmd.ExecuteScalarAsync();
                    if (inserted == null)
                        throw new Exception("Failed to insert new currency.");

                    currencyId = Convert.ToInt32(inserted);
                }
            }

            // Map to countries (if not already mapped)
            foreach (var (countryName, countryId) in countryIds)
            {
                var checkMapSql = @"SELECT 1 FROM Currency_Country 
                                    WHERE Country_Id = @CountryId AND Currency_Id = @CurrencyId";
                using var checkCmd = new SqlCommand(checkMapSql, conn, transaction);
                checkCmd.Parameters.AddWithValue("@CountryId", countryId);
                checkCmd.Parameters.AddWithValue("@CurrencyId", currencyId);

                var exists = await checkCmd.ExecuteScalarAsync();
                if (exists == null)
                {
                    var insertMapSql = "INSERT INTO Currency_Country (Country_Id, Currency_Id) VALUES (@CountryId, @CurrencyId)";
                    using var insertCmd = new SqlCommand(insertMapSql, conn, transaction);
                    insertCmd.Parameters.AddWithValue("@CountryId", countryId);
                    insertCmd.Parameters.AddWithValue("@CurrencyId", currencyId);

                    if (await insertCmd.ExecuteNonQueryAsync() == 0)
                        throw new Exception("Failed to map currency to country.");
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
}


    public async Task<object?> SearchCurrency(string type, string query)
    {
        using (var conn = new SqlConnection(_connectionString))
        {
            try
            {
                await conn.OpenAsync();
                if (type == "Country")
                {
                    var sqlQuery =
                        "SELECT cur.Name, cur.Rate FROM currency cur JOIN Currency_Country CC on cur.Id = CC.Currency_Id JOIN Country C on C.Id = CC.Country_Id where C.Name = @query";

                    using (var cmd = new SqlCommand(sqlQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@query", query);
                        var reader = await cmd.ExecuteReaderAsync();
                        
                        var results = new List<object?>();

                        while (await reader.ReadAsync())
                        {
                            results.Add(new { Name = reader.GetString(0), Rate = reader.GetFloat(1) });
                        }

                        await reader.CloseAsync();
                        
                        return new { Name = query, Currencies = results };
                    }
                }
                else if (type == "Currency")
                {
                    var sqlQuery =
                        "SELECT * from Country country join Currency_Country CC on country.Id = CC.Country_Id join Currency cur ON cur.Id = CC.Currency_Id where cur.Name = @query";

                    using (var cmd = new SqlCommand(sqlQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@query", query);
                        var reader = await cmd.ExecuteReaderAsync();
                        
                        var results = new List<Country>();

                        while (await reader.ReadAsync())
                        {
                            results.Add(new Country
                            {
                                Id = reader.GetInt32(0),
                                Name = reader.GetString(1)
                            });
                        }
                        
                        await reader.CloseAsync();

                        return results.IsNullOrEmpty() ? null : results;
                    }
                }
                else
                {
                    throw new Exception("Invalid type. Expected: [Country, Currency]");
                }
            }
            finally
            {
                await conn.CloseAsync();
            }
        }
    }
}