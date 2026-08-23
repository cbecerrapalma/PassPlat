SET QUOTED_IDENTIFIER ON;

PRINT 'Fixing Metadata Claims format from string to JSON array...';

UPDATE [dbo].[ProvIden]
SET [Metadata] = N'{"SupportsPAR":false,"SupportsNonce":true,"SupportsDynamicClientRegistration":false,"Claims":["openid","profile","email"]}'
WHERE [Codigo] = 'GOOGLE';

UPDATE [dbo].[ProvIden]
SET [Metadata] = N'{"SupportsPAR":false,"SupportsNonce":true,"SupportsDynamicClientRegistration":false,"Claims":["email","public_profile"]}'
WHERE [Codigo] = 'FACEBOOK';

UPDATE [dbo].[ProvIden]
SET [Metadata] = N'{"SupportsPAR":false,"SupportsNonce":false,"SupportsDynamicClientRegistration":false,"Claims":["user_profile","user_media"]}'
WHERE [Codigo] = 'INSTAGRAM';

UPDATE [dbo].[ProvIden]
SET [Metadata] = N'{"SupportsPAR":false,"SupportsNonce":true,"SupportsDynamicClientRegistration":false,"Claims":["openid","profile","email"]}'
WHERE [Codigo] = 'LINKEDIN';

PRINT 'Metadata corregido exitosamente.';
GO
