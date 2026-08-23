using System.Diagnostics;
using CBP.Security.Cryptography;
using CBP.Security.Cryptography.Configuration;
using CBP.Security.Cryptography.Factories;
using CBP.Security.Cryptography.Models;
using CBP.Security.Cryptography.Validation.Policies;

const string PepperEnvVar = "PASSWORDS_PEPPER";

static string GetPepper()
{
    var pepper = Environment.GetEnvironmentVariable(PepperEnvVar);
    if (string.IsNullOrEmpty(pepper))
    {
        Console.Error.WriteLine($"Warning: {PepperEnvVar} not set. Using empty pepper (insecure).");
        Console.Error.WriteLine($"  Set: set {PepperEnvVar}=my-secret-pepper (PowerShell)");
        Console.Error.WriteLine($"  Or:  export {PepperEnvVar}=my-secret-pepper (bash)");
        return string.Empty;
    }
    return pepper;
}

static string ReadPassword(string prompt)
{
    Console.Write(prompt);
    var password = new System.Text.StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            break;
        }
        if (key.Key == ConsoleKey.Backspace && password.Length > 0)
        {
            password.Length--;
            Console.Write("\b \b");
        }
        else if (!char.IsControl(key.KeyChar))
        {
            password.Append(key.KeyChar);
            Console.Write('*');
        }
    }
    return password.ToString();
}

static void ShowInteractiveMenu()
{
    Console.Clear();
    Console.WriteLine("========================================");
    Console.WriteLine("  PassPlat.Consola — Password Manager");
    Console.WriteLine("========================================");
    Console.WriteLine();
    Console.WriteLine("  1. Hash a password");
    Console.WriteLine("  2. Verify a password against a hash");
    Console.WriteLine("  3. Generate a random password");
    Console.WriteLine("  4. Analyze password strength");
    Console.WriteLine("  5. Show pepper info");
    Console.WriteLine("  0. Exit");
    Console.WriteLine();
    Console.Write("Select an option: ");
}

var cmdArgs = Environment.GetCommandLineArgs().AsSpan(1).ToArray();

if (cmdArgs.Length > 0)
{
    var sw = Stopwatch.StartNew();
    try
    {
        var command = cmdArgs[0].ToLowerInvariant();
        var rest = cmdArgs.AsSpan(1).ToArray();

        switch (command)
        {
            case "hash":
                await CmdHash(rest);
                break;
            case "verify":
                await CmdVerify(rest);
                break;
            case "generate":
                await CmdGenerate(rest);
                break;
            case "analyze":
                await CmdAnalyze(rest);
                break;
            default:
                Console.Error.WriteLine($"Unknown command: {command}");
                PrintUsage();
                break;
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
    }
    sw.Stop();
    return;
}

while (true)
{
    ShowInteractiveMenu();
    var choice = Console.ReadLine()?.Trim();

    switch (choice)
    {
        case "1":
            Console.WriteLine();
            var pwdHash = ReadPassword("Enter password: ");
            if (string.IsNullOrEmpty(pwdHash)) { Pause(); continue; }
            Console.Write("Skip breach check? (y/N): ");
            var skip = Console.ReadLine()?.Trim().ToLowerInvariant();
            await CmdHash(skip is "y" or "yes" ? ["--no-breach", pwdHash] : [pwdHash]);
            break;

        case "2":
            Console.WriteLine();
            Console.Write("Enter stored hash: ");
            var storedHash = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(storedHash)) { Pause(); continue; }
            var pwdVerify = ReadPassword("Enter password to verify: ");
            if (string.IsNullOrEmpty(pwdVerify)) { Pause(); continue; }
            await CmdVerify([storedHash, pwdVerify]);
            break;

        case "3":
            Console.WriteLine();
            Console.Write("Password length [12]: ");
            var lenInput = Console.ReadLine()?.Trim();
            var generateArgs = string.IsNullOrEmpty(lenInput)
                ? Array.Empty<string>()
                : ["--length", lenInput];
            await CmdGenerate(generateArgs);
            break;

        case "4":
            Console.WriteLine();
            var pwdAnalyze = ReadPassword("Enter password: ");
            if (string.IsNullOrEmpty(pwdAnalyze)) { Pause(); continue; }
            await CmdAnalyze([pwdAnalyze]);
            break;

        case "5":
            Console.WriteLine();
            Console.WriteLine("Pepper info:");
            Console.WriteLine($"  Variable: {PepperEnvVar}");
            var current = Environment.GetEnvironmentVariable(PepperEnvVar);
            Console.WriteLine($"  Status:   {(string.IsNullOrEmpty(current) ? "NOT SET" : "configured")}");
            Console.WriteLine($"  Value:    {(!string.IsNullOrEmpty(current) ? "**** (hidden)" : "(empty)")}");
            break;

        case "0":
            Console.WriteLine("Goodbye!");
            return;

        default:
            Console.WriteLine("Invalid option. Press any key to continue...");
            Console.ReadKey(true);
            break;
    }

    Pause();
}

static void Pause()
{
    Console.WriteLine();
    Console.Write("Press any key to continue...");
    Console.ReadKey(true);
}

static void PrintUsage()
{
    Console.WriteLine("PassPlat.Consola — Password hashing & verification tool");
    Console.WriteLine();
    Console.WriteLine("Uses CBP.Security.Cryptography (Argon2id)");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  hash [--no-breach] <password>   Hash a password (--no-breach skips breach check)");
    Console.WriteLine("  verify <hash> <password>        Verify a password against a stored hash");
    Console.WriteLine("  generate [--length <n>]         Generate a random password");
    Console.WriteLine("  analyze <password>              Analyze password strength");
    Console.WriteLine();
    Console.WriteLine("With no arguments, starts interactive mode.");
    Console.WriteLine();
    Console.WriteLine("Environment variable:");
    Console.WriteLine($"  {PepperEnvVar} — optional pepper for HMAC pre-hashing");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  dotnet run -- hash \"MyP@ssw0rd!\"");
    Console.WriteLine("  dotnet run -- hash --no-breach \"MyP@ssw0rd!\"");
    Console.WriteLine("  dotnet run -- verify \"$argon2id$...\" \"MyP@ssw0rd!\"");
    Console.WriteLine("  dotnet run -- generate --length 16");
    Console.WriteLine("  dotnet run -- analyze \"MyP@ssw0rd!\"");
}

static async Task CmdHash(string[] args)
{
    var noBreach = false;
    var passwordArgs = new List<string>();

    foreach (var arg in args)
    {
        if (arg is "--no-breach")
            noBreach = true;
        else
            passwordArgs.Add(arg);
    }

    if (passwordArgs.Count == 0)
    {
        Console.Error.WriteLine("Usage: hash [--no-breach] <password>");
        return;
    }

    var password = string.Join(" ", passwordArgs);
    Console.Error.WriteLine($"Hashing password ({password.Length} chars)...");

    var policy = PolicyNormalizer.CreateDefaultPolicy();
    if (noBreach)
    {
        policy.VerificarBrechas = false;
        policy.ProhPwdComun = false;
        policy.ProhSecuenciales = false;
        policy.ProhRepetitivos = false;
        policy.ProhPatrones = false;
        policy.ReqMayuscula = false;
        policy.ReqMinuscula = false;
        policy.ReqNumero = false;
        policy.ReqEspecial = false;
        policy.LongMin = 1;
        Console.Error.WriteLine("All validation checks disabled (--no-breach mode).");
    }

    var service = ServiceFactory.CreateDefault();
    var pepper = GetPepper();

    var result = await service.CreatePasswordAsync(password, policy, pepper, pepperVersion: 1);

    if (!result.Success)
    {
        Console.Error.WriteLine("Failed to create password hash:");
        foreach (var err in result.Errors ?? [])
            Console.Error.WriteLine($"  - {err}");
        return;
    }

    Console.WriteLine();
    Console.WriteLine($"Algorithm: {result.HashInfo?.Algorithm}");
    Console.WriteLine($"Hash:      {result.HashInfo?.Hash}");
    Console.WriteLine($"Duration:  {result.ProcessingDuration.TotalMilliseconds:F1} ms");
    Console.WriteLine($"Strength:  {result.PwdStrengthAnalysis?.StrengthLevel} ({result.PwdStrengthAnalysis?.StrengthScore:F0}/100)");

    if (result.HashInfo?.Hash is { } hash)
    {
        Console.WriteLine();
        Console.WriteLine("To verify:");
        Console.WriteLine($"  dotnet run -- verify \"{hash}\" \"{password}\"");
    }
}

static async Task CmdVerify(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: verify <hash> <password>");
        return;
    }

    var hash = args[0];
    var password = string.Join(" ", args.AsSpan(1).ToArray());
    var pepper = GetPepper();

    var preview = hash.Length > 40 ? hash[..40] + "..." : hash;
    Console.Error.WriteLine($"Verifying password against hash ({preview})...");

    var service = ServiceFactory.CreateDefault();
    var isValid = await service.VerifyAsync(hash, password, pepper);

    if (isValid)
        Console.WriteLine("Result: MATCH — password is valid");
    else
        Console.WriteLine("Result: NO MATCH — password is invalid");
}

static async Task CmdGenerate(string[] args)
{
    var length = 12;

    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "--length" && i + 1 < args.Length)
        {
            if (int.TryParse(args[i + 1], out var l))
                length = Math.Clamp(l, 4, 128);
            i++;
        }
    }

    Console.Error.WriteLine($"Generating password (length={length})...");

    var policy = PolicyNormalizer.CreateDefaultPolicy();

    var genOptions = new GenerationOptions
    {
        DefaultLength = length,
        IncludeUppercase = true,
        IncludeLowercase = true,
        IncludeNumbers = true,
        IncludeSpecialChars = true,
        ExcludeAmbiguousChars = true
    };

    var service = ServiceFactory.Create(generationOptions: genOptions);
    var result = await service.GenerateTemporaryPasswordAsync(policy);

    if (!result.Success)
    {
        Console.Error.WriteLine("Failed to generate password:");
        foreach (var err in result.Errors ?? [])
            Console.Error.WriteLine($"  - {err}");
        return;
    }

    Console.WriteLine($"Password:  {result.Password}");
    Console.WriteLine($"Length:    {result.Password.Length}");
    Console.WriteLine($"Strength:  {result.PwdStrengthAnalysis?.StrengthLevel} ({result.PwdStrengthAnalysis?.StrengthScore:F0}/100)");
    Console.WriteLine($"Duration:  {result.GenerationDuration.TotalMilliseconds:F1} ms");

    Console.WriteLine();
    Console.WriteLine("To hash:");
    Console.WriteLine($"  dotnet run -- hash \"{result.Password}\"");
}

static async Task CmdAnalyze(string[] args)
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Usage: analyze <password>");
        return;
    }

    var password = string.Join(" ", args);

    Console.Error.WriteLine($"Analyzing password ({password.Length} chars)...");

    var service = ServiceFactory.CreateDefault();
    var analysis = await service.AnalyzePasswordAsync(password);

    Console.WriteLine($"Length:              {analysis.Length}");
    Console.WriteLine($"Score:               {analysis.StrengthScore:F0}/100");
    Console.WriteLine($"Level:               {analysis.StrengthLevel} — {analysis.StrengthDescription}");
    Console.WriteLine($"Has uppercase:       {analysis.HasUppercase}");
    Console.WriteLine($"Has lowercase:       {analysis.HasLowercase}");
    Console.WriteLine($"Has numbers:         {analysis.HasNumbers}");
    Console.WriteLine($"Has special chars:   {analysis.HasSpecialCharacters}");
    Console.WriteLine($"Distinct characters: {analysis.DistinctCharacterCount}");
    Console.WriteLine($"Is common:           {analysis.IsCommon}");
    Console.WriteLine($"Has sequential:      {analysis.HasSequentialChars}");
    Console.WriteLine($"Has repeating:       {analysis.HasRepeatingChars}");
    Console.WriteLine($"Has keyboard pat:    {analysis.HasKeyboardPatterns}");
    Console.WriteLine($"Contains user info:  {analysis.ContainsUserInfo}");
    Console.WriteLine($"Is secure:           {analysis.IsSecure}");

    if (analysis.Warnings.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Warnings:");
        foreach (var w in analysis.Warnings)
            Console.WriteLine($"  ! {w}");
    }

    if (analysis.Recommendations.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Recommendations:");
        foreach (var r in analysis.Recommendations)
            Console.WriteLine($"  > {r}");
    }
}
