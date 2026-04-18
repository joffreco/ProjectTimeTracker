using Microsoft.Extensions.DependencyInjection;
using ProjectTimeTracker.Application;
using ProjectTimeTracker.Infrastructure;

namespace ProjectTimeTracker;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        ServiceCollection services = new();
        ConfigureServices(services);

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        System.Windows.Forms.Application.Run(serviceProvider.GetRequiredService<Form1>());
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        string appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProjectTimeTracker");
        Directory.CreateDirectory(appData);

        services.AddSingleton<IGoogleFirestoreSession, GoogleFirestoreSession>();
        services.AddSingleton<FirestoreEventGateway>();
        services.AddSingleton<ProjectsRepository>();
        services.AddSingleton<IEventQueue>(_ => new FileEventQueue(Path.Combine(appData, "event-queue.json")));
        services.AddSingleton<BackgroundSyncWorker>();
        services.AddSingleton<DeviceIdentityProvider>();
        services.AddSingleton<TrackerAppService>();
        services.AddTransient<Form1>();
    }
}