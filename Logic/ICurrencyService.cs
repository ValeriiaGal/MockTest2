using MockTest;

// Add these methods to ICurrencyService and implement in CurrencyService
public interface ICurrencyService
{
    Task<bool> AddCurrency(CurrencyRequestDTO request);
    Task<object?> SearchCurrency(string type, string query);
    Task<IEnumerable<Currency>> GetAllCurrencies();
    Task<IEnumerable<Country>> GetAllCountries();
    Task<bool> DeleteCurrencyByName(string name);
    Task<bool> DeleteCountryById(int id);
    Task<bool> UpdateCurrencyRate(string name, float rate);
    Task<IEnumerable<Country>?> GetCountriesForCurrency(string currencyName);
    Task<object?> GetCurrenciesForCountry(string countryName);
}
