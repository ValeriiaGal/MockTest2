using MockTest;



var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("UniversityDatabase");

builder.Services.AddSingleton<ICurrencyService, CurrencyService>(s => new CurrencyService(connectionString));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();


app.MapPost("/api/currency", async (CurrencyRequestDTO request, ICurrencyService service) =>
{
    try
    {
        var result = await service.AddCurrency(request);
        return result ? Results.NoContent() : Results.BadRequest();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.MapGet("/api/search", async (string type, string query, ICurrencyService service) =>
{
    try
    {
        var result = await service.SearchCurrency(type, query);
        return result is not null ? Results.Ok(result) : Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});


// Get all currencies
app.MapGet("/api/currencies", async (ICurrencyService service) =>
{
    var result = await service.GetAllCurrencies();
    return Results.Ok(result);
});

// Get all countries
app.MapGet("/api/countries", async (ICurrencyService service) =>
{
    var result = await service.GetAllCountries();
    return Results.Ok(result);
});

// Delete currency by name
app.MapDelete("/api/currency/{name}", async (string name, ICurrencyService service) =>
{
    var deleted = await service.DeleteCurrencyByName(name);
    return deleted ? Results.NoContent() : Results.NotFound();
});

// Delete country by ID
app.MapDelete("/api/country/{id:int}", async (int id, ICurrencyService service) =>
{
    var deleted = await service.DeleteCountryById(id);
    return deleted ? Results.NoContent() : Results.NotFound();
});

// Update rate for existing currency
app.MapPut("/api/currency/{name}", async (string name, float rate, ICurrencyService service) =>
{
    var updated = await service.UpdateCurrencyRate(name, rate);
    return updated ? Results.NoContent() : Results.NotFound();
});

// Get countries using a specific currency
app.MapGet("/api/currency/{name}/countries", async (string name, ICurrencyService service) =>
{
    var result = await service.GetCountriesForCurrency(name);
    return result is not null ? Results.Ok(result) : Results.NotFound();
});

// Get currencies used by a specific country
app.MapGet("/api/country/{name}/currencies", async (string name, ICurrencyService service) =>
{
    var result = await service.GetCurrenciesForCountry(name);
    return result is not null ? Results.Ok(result) : Results.NotFound();
});


app.Run();

// ldkjgslkdjf;lsdkfs;jd