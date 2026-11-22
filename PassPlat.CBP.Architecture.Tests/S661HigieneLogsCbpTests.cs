using System.IO;
using Xunit;

namespace PassPlat.CBP.Architecture.Tests;

public class S661HigieneLogsCbpTests
{
    [Fact]
    public void Datos_no_conserva_copias_de_la_higiene_SQL()
    {
        var redaccionPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PassPlat.Datos", "Logging", "RedaccionSql.cs");
        var interceptorPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PassPlat.Datos", "Logging", "ErrorSqlRedactadoInterceptor.cs");

        Assert.False(File.Exists(redaccionPath));
        Assert.False(File.Exists(interceptorPath));
    }
}