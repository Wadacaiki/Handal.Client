using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Handal.Client;
using Handal.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Регистрация HttpClient
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Регистрация сервисов платформы
builder.Services.AddScoped<AuctionPlatformService>();
builder.Services.AddScoped(sp => sp.GetRequiredService<AuctionPlatformService>().UserService);
builder.Services.AddScoped(sp => sp.GetRequiredService<AuctionPlatformService>().AuctionService);
builder.Services.AddScoped(sp => sp.GetRequiredService<AuctionPlatformService>().BidService);
builder.Services.AddScoped(sp => sp.GetRequiredService<AuctionPlatformService>().NotificationService);
builder.Services.AddScoped(sp => sp.GetRequiredService<AuctionPlatformService>().ChatService);

await builder.Build().RunAsync();
