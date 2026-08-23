using CBP.MultiTenant.Abstractions;
using PassPlat.Dominio.Entities.Catalogos;

namespace PassPlat.WebAPI.MultiTenant;

public class PassPlatTenantMapper : ITenantMapper<Tenant>
{
    public TenantInfo Map(Tenant source) =>
        TenantInfo.Create(source.Id, source.Codigo, source.Nombre, source.Activo);
}
