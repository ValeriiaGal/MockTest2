using MockTest;

public interface ICurrencyService
{
    public Task<bool> AddCurrency(CurrencyRequestDTO request);
    public Task<object?> SearchCurrency(string type, string query);
}