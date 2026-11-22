using MudBlazor;

namespace PassPlat.Web.Styles;

/// <summary>
/// S55 · D1 — tema unico de la Web.
/// El login y la app usan la misma paleta (antes el login abria sin tema y las
/// paginas IAM se veian siempre oscuras) y los tokens de <c>app.css</c> se
/// derivan de las variables de MudBlazor, asi que cambian solos con el modo claro/oscuro.
/// </summary>
public static class AppTheme
{
    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#1e88e5",
            Secondary = "#26a69a",
            Error = "#ef5350",
            Success = "#66bb6a",
            Warning = "#ffa726",
            Info = "#42a5f5",
            AppbarBackground = "#1e293b",
            DrawerBackground = "#ffffff",
            Background = "#f4f6fb",
            Surface = "#ffffff"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#64b5f6",
            Secondary = "#80cbc4",
            Error = "#ef9a9a",
            Success = "#a5d6a7",
            Warning = "#ffcc80",
            Info = "#90caf9",
            AppbarBackground = "#0f172a",
            DrawerBackground = "#1e293b",
            Surface = "#1e293b",
            Background = "#0f172a",
            Dark = "#0b1121",
            DrawerText = "#cbd5e1",
            AppbarText = "#ffffff",
            TextPrimary = "#f1f5f9",
            TextSecondary = "#94a3b8"
        }
    };
}
