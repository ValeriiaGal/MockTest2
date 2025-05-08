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

    public async Task<IEnumerable<Currency>> GetAllCurrencies()
    {
        var result = new List<Currency>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = new SqlCommand("SELECT Id, Name, Rate FROM Currency", conn);
        var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new Currency
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Rate = reader.GetFloat(2)
            });
        }
        await reader.CloseAsync();
        return result;
    }

    public async Task<IEnumerable<Country>> GetAllCountries()
    {
        var result = new List<Country>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = new SqlCommand("SELECT Id, Name FROM Country", conn);
        var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new Country
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }
        await reader.CloseAsync();
        return result;
    }

    public async Task<bool> DeleteCurrencyByName(string name)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var transaction = conn.BeginTransaction();
        try
        {
            var getIdCmd = new SqlCommand("SELECT Id FROM Currency WHERE Name = @name", conn, transaction);
            getIdCmd.Parameters.AddWithValue("@name", name);
            var idObj = await getIdCmd.ExecuteScalarAsync();
            if (idObj == null) return false;
            int id = Convert.ToInt32(idObj);

            var deleteLinks = new SqlCommand("DELETE FROM Currency_Country WHERE Currency_Id = @id", conn, transaction);
            deleteLinks.Parameters.AddWithValue("@id", id);
            await deleteLinks.ExecuteNonQueryAsync();

            var deleteCurrency = new SqlCommand("DELETE FROM Currency WHERE Id = @id", conn, transaction);
            deleteCurrency.Parameters.AddWithValue("@id", id);
            await deleteCurrency.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> DeleteCountryById(int id)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var transaction = conn.BeginTransaction();
        try
        {
            var deleteLinks = new SqlCommand("DELETE FROM Currency_Country WHERE Country_Id = @id", conn, transaction);
            deleteLinks.Parameters.AddWithValue("@id", id);
            await deleteLinks.ExecuteNonQueryAsync();

            var deleteCountry = new SqlCommand("DELETE FROM Country WHERE Id = @id", conn, transaction);
            deleteCountry.Parameters.AddWithValue("@id", id);
            int rows = await deleteCountry.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
            return rows > 0;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> UpdateCurrencyRate(string name, float rate)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = new SqlCommand("UPDATE Currency SET Rate = @rate WHERE Name = @name", conn);
        cmd.Parameters.AddWithValue("@rate", rate);
        cmd.Parameters.AddWithValue("@name", name);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<IEnumerable<Country>?> GetCountriesForCurrency(string currencyName)
    {
        var result = new List<Country>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = new SqlCommand(@"SELECT c.Id, c.Name FROM Country c 
                                JOIN Currency_Country cc ON c.Id = cc.Country_Id 
                                JOIN Currency cur ON cur.Id = cc.Currency_Id 
                                WHERE cur.Name = @name", conn);
        cmd.Parameters.AddWithValue("@name", currencyName);
        var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new Country
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }
        await reader.CloseAsync();
        return result.Count > 0 ? result : null;
    }

    public async Task<object?> GetCurrenciesForCountry(string countryName)
    {
        var result = new List<object>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = new SqlCommand(@"SELECT cur.Name, cur.Rate FROM Currency cur
                                JOIN Currency_Country cc ON cur.Id = cc.Currency_Id
                                JOIN Country c ON c.Id = cc.Country_Id
                                WHERE c.Name = @name", conn);
        cmd.Parameters.AddWithValue("@name", countryName);
        var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new
            {
                Name = reader.GetString(0),
                Rate = reader.GetFloat(1)
            });
        }
        await reader.CloseAsync();
        return result.Count > 0 ? new { Name = countryName, Currencies = result } : null;
    }
}