using LLama.Web.Common;
using LLama.Web.Hubs;
using LLama.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var mvcBuilder = builder.Services.AddRazorPages();

if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
    builder.Configuration.AddJsonFile("appsettings.Local.json", true);
}

builder.Services.AddSignalR();
builder.Logging.ClearProviders();
builder.Services.AddLogging((loggingBuilder) => loggingBuilder.SetMinimumLevel(LogLevel.Trace).AddConsole());

// Load InteractiveOptions
builder.Services.AddOptions<LLamaOptions>()
    .BindConfiguration(nameof(LLamaOptions));

// Services DI
builder.Services.AddHostedService<ModelLoaderService>();
builder.Services.AddSingleton<IModelService, ModelService>();
builder.Services.AddSingleton<IModelSessionService, ModelSessionService>();
builder.Services.AddSingleton<StreamService>();      // client service
builder.Services.AddHostedService<SignalRBootstrapper>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.MapHub<SessionConnectionHub>(nameof(SessionConnectionHub));

app.Run();