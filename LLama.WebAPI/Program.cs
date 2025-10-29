using LLama.WebAPI.Services;
using LLama.WebAPI.Models;
using LLama.WebAPI.Hubs;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// SignalR and CORS  
builder.Services.AddSignalR();
builder.Services.AddCors(o =>
{
    o.AddPolicy("AllowLocalhost", p => p
        .WithOrigins("http://localhost:7038")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

//  Services 
builder.Services.AddSingleton<StatefulChatService>();   // For Local
builder.Services.AddScoped<StatelessChatService>();     // For MCP
builder.Services.AddHttpClient<McpClientService>();

builder.Services.Configure<McpSettings>(
    builder.Configuration.GetSection("Mcp")
);

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IOptions<McpSettings>>().Value
);

builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowLocalhost");
app.UseMiddleware<McpLoggingMiddleware>();
app.UseAuthorization();


app.MapControllers();
app.MapRazorPages();
app.MapHub<LlamaHub>("/LlamaHub");

app.Run();