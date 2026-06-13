namespace HTWind.Services.Application;

public enum ThemeOption
{
    Device,
    Light,
    Dark
}

public interface IThemeService
{
    ThemeOption CurrentTheme { get; }
    void ApplyTheme(ThemeOption option);
}
