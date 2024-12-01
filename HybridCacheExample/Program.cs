using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddOpenApi(); // Registro do OpenAPI

builder.Services.AddMemoryCache(); // Registro do MemoryCache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379"; // Redis no Docker
});

// Registro do HybridCache
#pragma warning disable EXTEXP0018
builder.Services.AddHybridCache(options =>
{
    options.MaximumPayloadBytes = 1024 * 1024; // 1 MB
    options.MaximumKeyLength = 256;
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(1)
    };
});
#pragma warning restore EXTEXP0018

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Endpoints
app.MapPost("/set", async ([FromBody] SetRequest request, HybridCache hybridCache) =>
{
    var tags = new []{ "tag1" };
    var entryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(2),
        Flags = HybridCacheEntryFlags.DisableDistributedCache
    };

    await hybridCache.SetAsync(request.Key, request.Value, entryOptions, tags);
    return Results.Ok($"Chave '{request.Key}' armazenada com valor '{request.Value}'.");
});




app.MapGet("/getOrCreate", async (string key, HybridCache hybridCache) =>
{
    var tags = new List<string> { "tag2" };
    var value = await hybridCache.GetOrCreateAsync<string>(
        key,
        async context =>
        {
            await Task.Delay(100); // Simula operação demorada
            return $"Valor gerado para a chave '{key}'";
        },
        new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromMinutes(10),
            LocalCacheExpiration = TimeSpan.FromMinutes(2)
        },
        tags);

    return Results.Ok($"Valor recuperado ou criado: {value}");
});

app.MapGet("/debug", async (string key, IMemoryCache memoryCache, IDistributedCache distributedCache) =>
{
    // Verifica se está no MemoryCache
    if (memoryCache.TryGetValue(key, out var memoryValue))
    {
        return Results.Ok($"Valor encontrado no MemoryCache: {memoryValue}");
    }

    return Results.NotFound("Valor não encontrado em camada Memory Cache.");
});

app.MapDelete("/remove", async (string key, HybridCache hybridCache) =>
{
    await hybridCache.RemoveAsync(key);
    return Results.Ok($"Removendo a key '{key}' com sucesso.");
});

app.MapDelete("/removeByTag", async (string tag, HybridCache hybridCache) =>
{
    await hybridCache.RemoveByTagAsync(tag);
    return Results.Ok($"Todas as chaves associadas à tag '{tag}' foram removidas.");
});

app.Run();

public class SetRequest
{
    public string Key { get; set; }
    public string Value { get; set; }
}