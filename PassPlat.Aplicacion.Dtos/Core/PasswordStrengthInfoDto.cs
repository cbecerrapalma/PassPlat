namespace PassPlat.Aplicacion.Dtos.Core;

public class PasswordStrengthInfoDto
{
    public int Score { get; set; }
    public string Level { get; set; } = string.Empty;
    public int Length { get; set; }
    public bool HasUppercase { get; set; }
    public bool HasLowercase { get; set; }
    public bool HasNumbers { get; set; }
    public bool HasSpecialCharacters { get; set; }
    public bool IsCommon { get; set; }
    public bool HasSequentialChars { get; set; }
    public bool HasRepeatingChars { get; set; }
    public bool HasKeyboardPatterns { get; set; }
    public bool ContainsUserInfo { get; set; }
    public List<string> Warnings { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = [];
}

public class ValidarPasswordRequestDto
{
    public string Password { get; set; } = string.Empty;
    public string? NomUsuario { get; set; }
    public string? Email { get; set; }
}

public class PasswordGenerationResultDto
{
    public bool Success { get; set; }
    public string Password { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = [];
}
