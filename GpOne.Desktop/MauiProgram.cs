using GpOne.Services;
using Microsoft.Extensions.Logging;

namespace GpOne.Desktop;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

		// Servizi condivisi con l'applicazione web (GpOne.Shared)
		// Profili di connessione con password cifrate, nel profilo dell'utente Windows
		builder.Services.AddGpOneSessione(
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GpOne"),
			chiaviPerMacchina: false);
		// Microsoft 365 Copilot: accesso con l'account Windows del PC (broker WAM) tramite la finestra principale
		builder.Services.AddGpOneAi(
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GpOne"),
			finestraWindows: () => Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView is Microsoft.UI.Xaml.Window finestra
				? WinRT.Interop.WindowNative.GetWindowHandle(finestra)
				: IntPtr.Zero);

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
