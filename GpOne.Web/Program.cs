using GpOne.Web.Components;
using GpOne.Services;
using GpOne.Web.Dashboard;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();


// Profili di connessione e chiavi di cifratura: cartella configurabile (GpOne:CartellaDati), predefinita App_Data.
// In produzione usare una cartella fuori dal progetto, leggibile solo dall'account Windows con cui gira GpOne,
// con GpOne:ChiaviPerMacchina = false: così solo quell'account può decifrare le password.
var cartellaDati = builder.Configuration["GpOne:CartellaDati"] is { Length: > 0 } cartella
    ? cartella
    : Path.Combine(builder.Environment.ContentRootPath, "App_Data");
builder.Services.AddGpOneSessione(cartellaDati, chiaviPerMacchina: builder.Configuration.GetValue("GpOne:ChiaviPerMacchina", true));

// GpOne AI con motore AI configurabile (Sistema > Intelligenza artificiale)
builder.Services.AddGpOneAi(cartellaDati);

// Dashboard DevExpress in Home (file in <CartellaDati>/Dashboards)
builder.Services.AddGpOneDashboard(cartellaDati);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.UseGpOneDashboard();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(GpOne.Components.Layout.MainLayout).Assembly);

app.Run();
