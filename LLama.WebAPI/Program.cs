using LLama.WebAPI.Services;
using LLama.WebAPI.Models;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<StatefulChatService>();
builder.Services.AddScoped<StatelessChatService>();
builder.Services.AddHttpClient<McpClientService>();


builder.Services.Configure<McpSettings>(
    builder.Configuration.GetSection("Mcp")
);


builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IOptions<McpSettings>>().Value
);

var app = builder.Build();
app.UseMiddleware<LLama.WebAPI.Services.McpLoggingMiddleware>();
app.UseRouting();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.UseEndpoints(endpoints =>
{
    _ = endpoints.MapControllers();
});

app.Run();
