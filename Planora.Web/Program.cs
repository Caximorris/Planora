using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Planora.Web;
using Planora.Web.Auth;
using Planora.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5009";

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();

builder.Services.AddScoped<PlanorAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    sp => sp.GetRequiredService<PlanorAuthStateProvider>());

builder.Services.AddTransient<AuthHeaderHandler>();

// Main client: uses AuthHeaderHandler for automatic token injection
builder.Services.AddHttpClient("PlanorApi", client =>
    client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<AuthHeaderHandler>();

builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("PlanorApi"));

// Bare refresh client: no auth handler — used for silent token refresh to avoid circular calls
builder.Services.AddHttpClient("PlanoraRefresh", client =>
    client.BaseAddress = new Uri(apiBaseUrl));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<WorkspaceService>();
builder.Services.AddScoped<BoardService>();
builder.Services.AddScoped<ColumnService>();
builder.Services.AddScoped<CardService>();
builder.Services.AddScoped<InvitationService>();
builder.Services.AddScoped<CommentService>();
builder.Services.AddScoped<LabelService>();
builder.Services.AddScoped<ChecklistService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<UserService>();

await builder.Build().RunAsync();
