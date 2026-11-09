using System.Diagnostics;

namespace ContactTogetherApi.Helper;

/// <summary>
/// Opens Swagger UI in the default browser once the app has started. Meant for Development only.
/// </summary>
public static class SwaggerBrowserLauncher
{
    public static WebApplication OpenSwaggerOnStart(this WebApplication app)
    {
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            // app.Urls holds the real bound addresses only after the server has started.
            var baseUrl = app.Urls.FirstOrDefault(u => u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                ?? app.Urls.FirstOrDefault();
            if (baseUrl is null)
            {
                return;
            }

            var url = baseUrl.TrimEnd('/') + "/swagger";
            app.Logger.LogInformation("Swagger UI: {Url}", url);

            if (!app.Configuration.GetValue("Swagger:OpenBrowser", true))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                app.Logger.LogWarning(ex, "Could not open the browser at {Url}.", url);
            }
        });

        return app;
    }
}
