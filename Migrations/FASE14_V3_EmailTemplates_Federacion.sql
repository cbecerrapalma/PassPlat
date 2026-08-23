-- =============================================================================
-- FASE 14 V3 — Email Templates para Federación de Identidades
-- =============================================================================
-- Fecha: 2026-07-04
-- Templates:
--   27 - external-login
--   28 - external-identity-linked
--   29 - external-identity-unlinked
-- =============================================================================

SET IDENTITY_INSERT dbo.EmailTemplates ON;

-- Login externo exitoso
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(27, 'external-login', 'es',
 N'Inicio de sesión externo — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#1976d2;font-size:24px;margin:0 0 8px 0;">Inicio de sesión externo</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">Has iniciado sesión usando <strong>{{ProviderName}}</strong>. Si no fuiste tú, contacta al administrador inmediatamente.</p><p style="color:#999;font-size:12px;line-height:1.5;margin:0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación de inicio de sesión exitoso mediante proveedor externo.',
 'seguridad', 'publicado', 1,
 N'UserName (nombre del usuario), ProviderName (nombre del proveedor, ej: GOOGLE), ProviderCode (código del proveedor), FechaHora (fecha y hora UTC), AppName (nombre del sistema), LogoUrl (opcional)');

-- Identidad externa vinculada
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(28, 'external-identity-linked', 'es',
 N'Cuenta vinculada — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#388e3c;font-size:24px;margin:0 0 8px 0;">Cuenta vinculada</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">Tu cuenta ha sido vinculada exitosamente con un proveedor externo. Ahora puedes iniciar sesión usando ese proveedor.</p><p style="color:#999;font-size:12px;line-height:1.5;margin:0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación cuando un usuario vincula una identidad externa a su cuenta.',
 'permisos', 'publicado', 1,
 N'UserName (nombre del usuario), IdProvIden (ID del proveedor), FechaHora (fecha y hora UTC), AppName (nombre del sistema), LogoUrl (opcional)');

-- Identidad externa desvinculada
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(29, 'external-identity-unlinked', 'es',
 N'Cuenta desvinculada — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#f57c00;font-size:24px;margin:0 0 8px 0;">Cuenta desvinculada</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">Se ha desvinculado un proveedor externo de tu cuenta. Si no solicitaste esta acción, contacta al administrador.</p>{% partial "card-alert" Mensaje: "Si no reconoces esta actividad, contacta al administrador inmediatamente.", BgColor: "#fff3e0", BorderColor: "#f57c00" %}<p style="color:#999;font-size:12px;line-height:1.5;margin:16px 0 0 0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación cuando se desvincula una identidad externa de la cuenta del usuario.',
 'alerta', 'publicado', 1,
 N'UserName (nombre del usuario), IdProvIden (ID del proveedor), FechaHora (fecha y hora UTC), AppName (nombre del sistema), LogoUrl (opcional)');

SET IDENTITY_INSERT dbo.EmailTemplates OFF;
GO
