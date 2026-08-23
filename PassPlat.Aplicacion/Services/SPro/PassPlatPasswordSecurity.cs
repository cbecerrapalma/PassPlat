using CBP.Security.Cryptography.Configuration;
using CBP.Security.Cryptography.Services;
using CBP.Security.Cryptography.Services.Analysis;
using CBP.Security.Cryptography.Services.BreachChecking;
using CBP.Security.Cryptography.Services.Generation;
using CBP.Security.Cryptography.Services.Hashing;
using CBP.Security.Cryptography.Services.Validation;
using CBP.Security.Cryptography.Services.Validation.Analyzers;
using CBP.Security.Cryptography.Services.Validation.Checkers;
using CBP.Security.Cryptography.Validation.Validators;

namespace PassPlat.Aplicacion.Services;

public interface IPassPlatPasswordSecurity
{
    Task<CBP.Security.Cryptography.Models.ValidationResult> ValidatePasswordAsync(
        string password, CBP.Security.Cryptography.Models.PoliticaPwd policy,
        CBP.Security.Cryptography.Models.ValidationContext? context = null,
        CancellationToken ct = default);

    Task<CBP.Security.Cryptography.Models.PwdStrengthAnalysis> AnalyzePasswordAsync(
        string password, CBP.Security.Cryptography.Models.ValidationContext? context = null,
        CancellationToken ct = default);

    Task<CBP.Security.Cryptography.Models.GenerationResult> GenerateTemporaryPasswordAsync(
        CBP.Security.Cryptography.Models.PoliticaPwd policy, CancellationToken ct = default);
}

public class PassPlatPasswordSecurity : IPassPlatPasswordSecurity
{
    private readonly CBP.Security.Cryptography.Services.IPasswordService _service;

    public PassPlatPasswordSecurity()
    {
        var hashingService = new HashingService();
        var patternAnalyzer = new PatternAnalyzer();
        var commonChecker = new InMemoryCommonChecker();
        var breachChecker = new HaveIBeenPwnedBreachChecker();

        var validators = new ValidationService(
            new IPolicyValidator[]
            {
                new BasicValidator(),
                new ComplexityValidator(patternAnalyzer, commonChecker),
                new ContextualValidator(patternAnalyzer),
                new HistoryValidator(hashingService),
                new BreachValidator(breachChecker)
            }
        );

        var strengthAnalyzer = new StrengthAnalyzer(patternAnalyzer);
        var generationOptions = GenerationOptions.CreateDefault();
        var generationService = new GenerationService(generationOptions, validators, strengthAnalyzer);

        _service = new CBP.Security.Cryptography.Services.PasswordService(hashingService, generationService, validators, strengthAnalyzer);
    }

    public Task<CBP.Security.Cryptography.Models.ValidationResult> ValidatePasswordAsync(
        string password, CBP.Security.Cryptography.Models.PoliticaPwd policy,
        CBP.Security.Cryptography.Models.ValidationContext? context = null,
        CancellationToken ct = default)
        => _service.ValidatePasswordAsync(password, policy, context, ct);

    public Task<CBP.Security.Cryptography.Models.PwdStrengthAnalysis> AnalyzePasswordAsync(
        string password, CBP.Security.Cryptography.Models.ValidationContext? context = null,
        CancellationToken ct = default)
        => _service.AnalyzePasswordAsync(password, context, ct);

    public Task<CBP.Security.Cryptography.Models.GenerationResult> GenerateTemporaryPasswordAsync(
        CBP.Security.Cryptography.Models.PoliticaPwd policy, CancellationToken ct = default)
        => _service.GenerateTemporaryPasswordAsync(policy, null, ct);
}
