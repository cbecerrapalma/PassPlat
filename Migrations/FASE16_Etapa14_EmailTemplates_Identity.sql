-- =============================================================================
-- FASE 16 Etapa 14 — Email Templates para Identity Enterprise
-- =============================================================================
-- Fecha: 2026-07-09
-- Templates:
--   30 - identity-principal-changed
--   31 - identity-linked-by-admin
--   32 - identity-removed-by-admin
--   33 - provider-disabled
--   34 - provider-enabled
--   35 - provider-authorization-revoked
--   36 - provider-authorization-granted
--   37 - oauth-consent-expired
--   38 - session-revoked
--   39 - security-notification
-- =============================================================================

SET IDENTITY_INSERT dbo.EmailTemplates ON;

-- Identidad principal cambiada
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(30, 'identity-principal-changed', 'es',
 N'Identidad principal actualizada — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#1976d2;font-size:24px;margin:0 0 8px 0;">Identidad principal cambiada</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">Tu identidad principal ha sido actualizada a <strong>{{NewProviderName}}</strong>. A partir de ahora, este será tu método de inicio de sesión preferido.</p><p style="color:#999;font-size:12px;line-height:1.5;margin:0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación cuando un usuario cambia su identidad externa principal.',
 'seguridad', 'publicado', 1,
 N'UserName, NewProviderName, OldProviderName, FechaHora, AppName');

-- Identidad vinculada por administrador
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(31, 'identity-linked-by-admin', 'es',
 N'Cuenta vinculada por administrador — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#388e3c;font-size:24px;margin:0 0 8px 0;">Cuenta vinculada por administrador</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">Un administrador ha vinculado la cuenta de <strong>{{ProviderName}}</strong> a tu perfil. Ahora puedes iniciar sesión usando ese proveedor.</p>{% partial "card-alert" Mensaje: "Si no solicitaste esta acción, contacta al administrador.", BgColor: "#fff3e0", BorderColor: "#f57c00" %}<p style="color:#999;font-size:12px;line-height:1.5;margin:16px 0 0 0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación cuando un administrador vincula una identidad externa a un usuario.',
 'permisos', 'publicado', 1,
 N'UserName, ProviderName, AdminName, FechaHora, AppName');

-- Identidad removida por administrador
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(32, 'identity-removed-by-admin', 'es',
 N'Cuenta desvinculada por administrador — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#d32f2f;font-size:24px;margin:0 0 8px 0;">Cuenta desvinculada por administrador</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">Un administrador ha desvinculado la cuenta de <strong>{{ProviderName}}</strong> de tu perfil. Si no reconoces esta actividad, contacta al administrador.</p>{% partial "card-alert" Mensaje: "Acción administrativa realizada en tu cuenta.", BgColor: "#ffebee", BorderColor: "#d32f2f" %}<p style="color:#999;font-size:12px;line-height:1.5;margin:16px 0 0 0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación cuando un administrador desvincula una identidad externa de un usuario.',
 'alerta', 'publicado', 1,
 N'UserName, ProviderName, AdminName, Motivo, FechaHora, AppName');

-- Proveedor deshabilitado
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(33, 'provider-disabled', 'es',
 N'Proveedor deshabilitado — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#d32f2f;font-size:24px;margin:0 0 8px 0;">Proveedor deshabilitado</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">El proveedor <strong>{{ProviderName}}</strong> ha sido deshabilitado. Ya no podrás iniciar sesión usando este proveedor hasta que sea reactivado.</p><p style="color:#999;font-size:12px;line-height:1.5;margin:0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación cuando un proveedor de identidad externa es deshabilitado.',
 'alerta', 'publicado', 1,
 N'UserName, ProviderName, FechaHora, AppName');

-- Proveedor habilitado
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(34, 'provider-enabled', 'es',
 N'Proveedor habilitado — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#388e3c;font-size:24px;margin:0 0 8px 0;">Proveedor habilitado</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">El proveedor <strong>{{ProviderName}}</strong> ha sido habilitado nuevamente. Ya puedes iniciar sesión usando este proveedor.</p><p style="color:#999;font-size:12px;line-height:1.5;margin:0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación cuando un proveedor de identidad externa es habilitado.',
 'transaccional', 'publicado', 1,
 N'UserName, ProviderName, FechaHora, AppName');

-- Autorización de proveedor revocada
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(35, 'provider-authorization-revoked', 'es',
 N'Autorización revocada — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#d32f2f;font-size:24px;margin:0 0 8px 0;">Autorización revocada</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">Se ha revocado la autorización para el proveedor <strong>{{ProviderName}}</strong>. La aplicación <strong>{{AppName}}</strong> ya no podrá acceder a los permisos concedidos.</p>{% partial "card-alert" Mensaje: "Si no reconoces esta actividad, contacta al administrador inmediatamente.", BgColor: "#ffebee", BorderColor: "#d32f2f" %}<p style="color:#999;font-size:12px;line-height:1.5;margin:16px 0 0 0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación cuando se revoca la autorización de un proveedor externo.',
 'alerta', 'publicado', 1,
 N'UserName, ProviderName, AppName, FechaHora');

-- Autorización de proveedor concedida
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(36, 'provider-authorization-granted', 'es',
 N'Autorización concedida — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#388e3c;font-size:24px;margin:0 0 8px 0;">Autorización concedida</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">Has concedido autorización al proveedor <strong>{{ProviderName}}</strong> para acceder a tu información. Los permisos otorgados están vigentes hasta <strong>{{ExpiraEn}}</strong>.</p><p style="color:#999;font-size:12px;line-height:1.5;margin:0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación cuando se concede autorización a un proveedor externo.',
 'transaccional', 'publicado', 1,
 N'UserName, ProviderName, ExpiraEn, Scopes, FechaHora, AppName');

-- Consentimiento OAuth expirado
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(37, 'oauth-consent-expired', 'es',
 N'Consentimiento expirado — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#f57c00;font-size:24px;margin:0 0 8px 0;">Consentimiento expirado</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">El consentimiento otorgado al proveedor <strong>{{ProviderName}}</strong> ha expirado. Para seguir utilizando este proveedor, deberás conceder autorización nuevamente.</p><p style="color:#999;font-size:12px;line-height:1.5;margin:0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación cuando expira el consentimiento OAuth de un proveedor externo.',
 'alerta', 'publicado', 1,
 N'UserName, ProviderName, FechaHora, AppName');

-- Sesión revocada
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(38, 'session-revoked', 'es',
 N'Sesión revocada — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#d32f2f;font-size:24px;margin:0 0 8px 0;">Sesión revocada</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">La sesión iniciada el <strong>{{SessionDate}}</strong> desde <strong>{{DeviceName}}</strong> ({{IpAddress}}) ha sido revocada. Si no reconoces esta actividad, contacta al administrador.</p>{% partial "card-alert" Mensaje: "Si no reconoces esta actividad, contacta al administrador inmediatamente.", BgColor: "#ffebee", BorderColor: "#d32f2f" %}<p style="color:#999;font-size:12px;line-height:1.5;margin:16px 0 0 0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación cuando se revoca una sesión de usuario.',
 'seguridad', 'publicado', 1,
 N'UserName, SessionDate, DeviceName, IpAddress, FechaHora, AppName');

-- Notificación de seguridad
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(39, 'security-notification', 'es',
 N'Notificación de seguridad — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#d32f2f;font-size:24px;margin:0 0 8px 0;">Notificación de seguridad</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">{{SecurityMessage}}</p>{% partial "card-alert" Mensaje: "Si no reconoces esta actividad, contacta al administrador inmediatamente.", BgColor: "#ffebee", BorderColor: "#d32f2f" %}<p style="color:#999;font-size:12px;line-height:1.5;margin:16px 0 0 0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación de seguridad genérica para eventos de identidad.',
 'seguridad', 'publicado', 1,
 N'UserName, SecurityMessage, FechaHora, AppName');

SET IDENTITY_INSERT dbo.EmailTemplates OFF;
GO
