-- FIX: TR_Accesos_ValidarTenant INSTEAD OF -> AFTER
-- Causa: EF Core con HasTrigger() usa OUTPUT INSERTED.[Id] que es incompatible
-- con INSTEAD OF triggers (retorna 0 filas -> DbUpdateConcurrencyException).
-- AFTER trigger mantiene la validación de tenant como defensa en profundidad.

PRINT 'Cambiando TR_Accesos_ValidarTenant de INSTEAD OF a AFTER...'
GO

DROP TRIGGER IF EXISTS dbo.TR_Accesos_ValidarTenant;
GO

CREATE TRIGGER dbo.TR_Accesos_ValidarTenant ON dbo.Accesos AFTER INSERT, UPDATE AS
BEGIN
  SET NOCOUNT ON;

  -- Validar que el tenant del acceso coincida con el del usuario
  IF EXISTS (SELECT 1 FROM inserted i JOIN dbo.Usuarios u ON i.IdUsuario = u.Id WHERE i.IdTenant <> u.IdTenant)
  BEGIN
    RAISERROR('El tenant del acceso debe coincidir con el del usuario.', 16, 1);
    ROLLBACK;
    RETURN;
  END;
END;
GO

PRINT 'Trigger actualizado correctamente.'
GO
