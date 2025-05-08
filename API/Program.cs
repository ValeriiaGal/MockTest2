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

app.Run();