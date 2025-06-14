using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Heatwise.Views;
using Heatwise.ViewModels;
using System.Linq;
using Avalonia.Styling;  
using Avalonia.Controls;
using System;
using Heatwise.Services;

namespace Heatwise;

public partial class App : Application
{
    public static event Action? ThemeChanged;
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            // Fetch settings
            SettingsViewModel settings = new();

            // Apply the initial theme
            UpdateTheme(settings.Light_Mode_On_Toggle);

            // Load the application separately
            MainWindowViewModel mainVM = await StartupService.LoadApplicationAsync();
            MainWindow mainWindow = new() { DataContext = mainVM };

            // Starting the application in Developer Mode or in Regular Mode
            if (settings.Developer_Mode_On_Toggle)
            {
                mainWindow.Show();
            }
            else
            {
                LoadingWindowViewModel loadingViewModel = new();
                LoadingWindow loadingWindow = new() { DataContext = loadingViewModel };
                LoginViewModel loginViewModel = new();
                LoginView loginView = new() { DataContext = loginViewModel };

                loadingWindow.Topmost = true; // Switching 'Topmost' to true, to make the loading screen popup on top
                loadingWindow.Show();
                loadingWindow.Activate();  // This does not seem to work on macOS, but it's here just to be safe
                loadingWindow.Topmost = false; // Switching 'Topmost' to false, so it's not on top when the main window appears
                loadingViewModel.Finished += () =>
                {
                    desktop.MainWindow = loginView;
                    loginView.Show();
                    loginView.Activate();
                    loadingWindow.Close();
                };
                await loadingViewModel.Start();

                loginViewModel.Success += () =>
                {
                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();
                    mainWindow.Activate();
                    loginView.Close();
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void UpdateTheme(bool Light_Mode_On_Toggle)
    {
        if (Application.Current == null) return;
        
        var mergedDictionaries = Application.Current.Resources.MergedDictionaries;
        var themeColors = mergedDictionaries.FirstOrDefault() as ResourceDictionary;

        if (themeColors == null)
        {
            return;
        }

        try
        {
            if (!Light_Mode_On_Toggle)
            {
                Current.Resources["BackgroundColor"] = themeColors["LightBackgroundColor"];
                Current.Resources["ForegroundColor"] = themeColors["LightForegroundColor"];
                Current.Resources["AccentColor"] = themeColors["LightAccentColor"];
                Current.Resources["TextColor"] = themeColors["LightTextColor"];
                Current.Resources["SecondaryBackground"] = themeColors["LightSecondaryBackground"];
                Current.Resources["BorderColor"] = themeColors["LightBorderColor"];
                Current.Resources["IconColor"] = themeColors["LightIconColor"];
                Current.Resources["GradientStartColor"] = themeColors["LightGradientStartColor"];
                Current.Resources["GradientEndColor"] = themeColors["LightGradientEndColor"];

                Application.Current.RequestedThemeVariant = ThemeVariant.Light;
            }
            else
            {
                Current.Resources["BackgroundColor"] = themeColors["DarkBackgroundColor"];
                Current.Resources["ForegroundColor"] = themeColors["DarkForegroundColor"];
                Current.Resources["AccentColor"] = themeColors["DarkAccentColor"];
                Current.Resources["TextColor"] = themeColors["DarkTextColor"];
                Current.Resources["SecondaryBackground"] = themeColors["DarkSecondaryBackground"];
                Current.Resources["BorderColor"] = themeColors["DarkBorderColor"];
                Current.Resources["IconColor"] = themeColors["DarkIconColor"];
                Current.Resources["GradientStartColor"] = themeColors["DarkGradientStartColor"];
                Current.Resources["GradientEndColor"] = themeColors["DarkGradientEndColor"];

                Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
            }
            ThemeChanged?.Invoke();
        }
        catch 
        {
          return; 
        }

        // Force UI update
        if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var window in desktop.Windows)
            {
                window.InvalidateVisual();
            }
        }
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}