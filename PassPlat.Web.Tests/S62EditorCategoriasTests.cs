namespace PassPlat.Web.Tests;

/// <summary>
/// S62 · D4 — el editor y el filtro de plantillas ofrecen las 8 categorías que admite
/// CK_EmailTpl_Categoria (incluida app-externa, la única que acepta la API pública de correo).
/// </summary>
public class S62EditorCategoriasTests
{
    private static readonly string[] Categorias =
        ["transaccional", "marketing", "sistema", "alerta", "seguridad", "permisos", "plataforma", "app-externa"];

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PassPlat.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontro la raiz del repositorio");
    }

    [Theory]
    [InlineData("EmailTemplateDialog.razor")]
    [InlineData("Index.razor")]
    public void Ofrece_las_ocho_categorias(string archivo)
    {
        var fuente = File.ReadAllText(Path.Combine(RaizDelRepo(), "PassPlat.Web", "Pages", "EmailTemplates", archivo));

        foreach (var c in Categorias)
            Assert.Contains($"<MudSelectItem Value=\"@(\"{c}\")\">", fuente);
        Assert.Contains(">App externa</MudSelectItem>", fuente);
    }
}
