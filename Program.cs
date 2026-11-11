using FruitflyStock.Components;

// ===== Dependency injection and Blazor host setup =====
// Pseudocode:
// - Configure interactive server-rendered Razor components.
// - Register service contracts with their production implementations.
// - Build the web host, enable production error/security middleware, and serve static files.
// - Map the root Razor component with interactive server rendering and run the host.
// IHttpClientFactory owns the HTTP client lifetime for the Sheets service; scoped service
// registrations match the interactive server circuit/request lifetime.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents();
builder.Services.AddScoped<IStockSearchService, StockSearchService>();
builder.Services.AddScoped<ICommonTermsService, CommonTermsService>();
builder.Services.Configure<GoogleOptions>(builder.Configuration.GetSection("Google"));
builder.Services.AddScoped<IGoogleTokenStore, GoogleTokenStore>();
builder.Services.AddHttpClient<GoogleOAuthService>();
builder.Services.AddScoped<IGoogleOAuthService>(
	serviceProvider => serviceProvider.GetRequiredService<GoogleOAuthService>());
builder.Services.AddHttpClient<IGoogleSheetsService, GoogleSheetsService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
	app.UseHsts();
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
	.AddInteractiveServerRenderMode();

app.Run();
