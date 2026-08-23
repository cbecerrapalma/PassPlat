-- =============================================================================
-- FASE 16 ETAPA 14 — 10 nuevos Email Templates para Identidad Federada
-- =============================================================================
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

-- Principal cambiado
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(30, 'identity-principal-changed', 'es',
 N'Identidad principal actualizada — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#1976d2;font-size:24px;margin:0 0 8px 0;">Identidad principal actualizada</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">Tu identidad principal ha sido cambiada a <strong>{{NewProviderName}}</strong>. Los inicios de sesión ahora usarán este proveedor por defecto.</p><p style="color:#999;font-size:12px;line-height:1.5;margin:0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación cuando se cambia la identidad principal de un usuario.',
 'permisos', 'publicado', 1,
 N'UserName, NewProviderName (nuevo proveedor principal), OldProviderName (proveedor anterior), FechaHora, AppName');

-- Identidad vinculada por administrador
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(31, 'identity-linked-by-admin', 'es',
 N'Identidad vinculada por administrador — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#388e3c;font-size:24px;margin:0 0 8px 0;">Identidad vinculada por administrador</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">Un administrador ha vinculado una identidad externa (<strong>{{ProviderName}}</strong>) a tu cuenta.</p>{% partial "card-alert" Mensaje: "Si no reconoces esta actividad, contacta al administrador.", BgColor: "#fff3e0", BorderColor: "#f57c00" %}<p style="color:#999;font-size:12px;line-height:1.5;margin:16px 0 0 0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación cuando un administrador vincula una identidad externa a la cuenta de un usuario.',
 'alerta', 'publicado', 1,
 N'UserName, ProviderName (nombre del proveedor), AdminName (nombre del administrador), FechaHora, AppName');

-- Identidad removida por administrador
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(32, 'identity-removed-by-admin', 'es',
 N'Identidad removida por administrador — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#d32f2f;font-size:24px;margin:0 0 8px 0;">Identidad removida por administrador</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">Un administrador ha removido la identidad externa <strong>{{ProviderName}}</strong> de tu cuenta.</p>{% partial "card-alert" Mensaje: "Si no reconoces esta actividad, contacta al administrador inmediatamente.", BgColor: "#ffebee", BorderColor: "#d32f2f" %}<p style="color:#999;font-size:12px;line-height:1.5;margin:16px 0 0 0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación cuando un administrador remueve una identidad externa de la cuenta de un usuario.',
 'alerta', 'publicado', 1,
 N'UserName, ProviderName (nombre del proveedor), AdminName (nombre del administrador), FechaHora, AppName');

-- Proveedor deshabilitado
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(33, 'provider-disabled', 'es',
 N'Proveedor deshabilitado — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#d32f2f;font-size:24px;margin:0 0 8px 0;">Proveedor deshabilitado</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">El proveedor <strong>{{ProviderName}}</strong> ha sido deshabilitado. Ya no podrás iniciar sesión usando este proveedor hasta que sea reactivado.</p><p style="color:#999;font-size:12px;line-height:1.5;margin:0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación cuando un proveedor de identidad es deshabilitado.',
 'alerta', 'publicado', 1,
 N'UserName, ProviderName (nombre del proveedor), Motivo (razón del deshabilitado), FechaHora, AppName');

-- Proveedor habilitado
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(34, 'provider-enabled', 'es',
 N'Proveedor habilitado — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#388e3c;font-size:24px;margin:0 0 8px 0;">Proveedor habilitado</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">El proveedor <strong>{{ProviderName}}</strong> ha sido habilitado nuevamente. Ya puedes iniciar sesión usando este proveedor.</p><p style="color:#999;font-size:12px;line-height:1.5;margin:0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación cuando un proveedor de identidad es habilitado nuevamente.',
 'transaccional', 'publicado', 1,
 N'UserName, ProviderName (nombre del proveedor), FechaHora, AppName');

-- Autorización de proveedor revocada
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(35, 'provider-authorization-revoked', 'es',
 N'Autorización revocada — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#d32f2f;font-size:24px;margin:0 0 8px 0;">Autorización revocada</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">La autorización para acceder a <strong>{{AppName}}</strong> a través de <strong>{{ProviderName}}</strong> ha sido revocada.</p>{% partial "card-alert" Mensaje: "Si no solicitaste esta acción, contacta al administrador.", BgColor: "#fff3e0", BorderColor: "#f57c00" %}<p style="color:#999;font-size:12px;line-height:1.5;margin:16px 0 0 0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación cuando se revoca la autorización OAuth de un proveedor.',
 'alerta', 'publicado', 1,
 N'UserName, ProviderName (nombre del proveedor), Scopes (ámbitos revocados), FechaHora, AppName');

-- Autorización de proveedor concedida
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(36, 'provider-authorization-granted', 'es',
 N'Autorización concedida — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#388e3c;font-size:24px;margin:0 0 8px 0;">Autorización concedida</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">Has concedido autorización a <strong>{{AppName}}</strong> para acceder a tu cuenta a través de <strong>{{ProviderName}}</strong>.</p><p style="color:#999;font-size:12px;line-height:1.5;margin:4px 0 0 0;">Ámbitos: <strong>{{Scopes}}</strong></p><p style="color:#999;font-size:12px;line-height:1.5;margin:0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación cuando se concede autorización OAuth a un proveedor.',
 'transaccional', 'publicado', 1,
 N'UserName, ProviderName (nombre del proveedor), Scopes (ámbitos concedidos), FechaHora, AppName');

-- Consentimiento OAuth expirado
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(37, 'oauth-consent-expired', 'es',
 N'Consentimiento expirado — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#f57c00;font-size:24px;margin:0 0 8px 0;">Consentimiento expirado</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">El consentimiento otorgado a <strong>{{AppName}}</strong> ha expirado. Deberás volver a autorizar la aplicación para continuar usándola.</p><p style="color:#999;font-size:12px;line-height:1.5;margin:0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación cuando el consentimiento OAuth otorgado a una aplicación expira.',
 'transaccional', 'publicado', 1,
 N'UserName, ProviderName (nombre del proveedor), FechaHora, AppName');

-- Sesión revocada
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(38, 'session-revoked', 'es',
 N'Sesión revocada — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#d32f2f;font-size:24px;margin:0 0 8px 0;">Sesión revocada</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">Una de tus sesiones ha sido revocada. Detalles:</p><p style="color:#555;font-size:14px;line-height:1.5;margin:0 0 4px 0;">Dispositivo: <strong>{{DeviceName}}</strong></p><p style="color:#555;font-size:14px;line-height:1.5;margin:0 0 20px 0;">IP: <strong>{{IpAddress}}</strong></p>{% partial "card-alert" Mensaje: "Si no reconoces esta actividad, contacta al administrador inmediatamente.", BgColor: "#ffebee", BorderColor: "#d32f2f" %}<p style="color:#999;font-size:12px;line-height:1.5;margin:16px 0 0 0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación cuando una sesión de usuario es revocada.',
 'alerta', 'publicado', 1,
 N'UserName, DeviceName (nombre del dispositivo), IpAddress (dirección IP), FechaHora, AppName');

-- Notificación de seguridad genérica
INSERT INTO dbo.EmailTemplates (Id, Nombre, Cultura, Asunto, CuerpoHtml, Descripcion, Categoria, Estado, Version, VariablesDoc) VALUES
(39, 'security-notification', 'es',
 N'Notificación de seguridad — {{AppName}}',
 N'<html><body style="font-family:Arial,sans-serif;background:#f4f4f4;padding:20px;margin:0;"><table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width:100%;max-width:600px;margin:0 auto;background:#ffffff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);"><tr><td style="padding:32px;"><h1 style="color:#d32f2f;font-size:24px;margin:0 0 8px 0;">Notificación de seguridad</h1><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 4px 0;">Hola <strong>{{UserName}}</strong>,</p><p style="color:#555;font-size:16px;line-height:1.5;margin:0 0 20px 0;">{{MensajeSeguridad}}</p>{% partial "card-alert" Mensaje: "Si no reconoces esta actividad, contacta al administrador inmediatamente.", BgColor: "#ffebee", BorderColor: "#d32f2f" %}<p style="color:#999;font-size:12px;line-height:1.5;margin:16px 0 0 0;">Fecha: <strong>{{FechaHora}}</strong></p></td></tr></table></body></html>',
 N'Notificación de seguridad genérica para eventos no cubiertos por otros templates.',
 'alerta', 'publicado', 1,
 N'UserName, MensajeSeguridad (mensaje descriptivo del evento), FechaHora, AppName');

SET IDENTITY_INSERT dbo.EmailTemplates OFF;
GO
