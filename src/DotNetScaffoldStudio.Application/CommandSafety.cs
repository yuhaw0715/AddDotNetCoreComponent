using DotNetScaffoldStudio.Domain;

namespace DotNetScaffoldStudio.Application;

public static class CommandPreviewFormatter
{
    public const string SecretMask = "••••••";

    public static string Format(CommandRequest request) => string.Join(' ',
        new[] { Quote(request.Executable) }
            .Concat(request.Arguments.Select(argument => Quote(argument.IsSecret ? SecretMask : argument.Value))));

    private static string Quote(string value)
    {
        if (value.Length > 0 && value.All(character => char.IsLetterOrDigit(character) || "-._/:=".Contains(character)))
        {
            return value;
        }

        return $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }
}

public sealed class SensitiveDataRedactor
{
    private readonly string[] _secretValues;

    public SensitiveDataRedactor(CommandRequest request)
    {
        _secretValues = request.Arguments
            .Where(argument => argument.IsSecret && !string.IsNullOrEmpty(argument.Value))
            .Select(argument => argument.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(value => value.Length)
            .ToArray();
    }

    public string Redact(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return _secretValues.Aggregate(text, (current, secret) =>
            current.Replace(secret, CommandPreviewFormatter.SecretMask, StringComparison.Ordinal));
    }
}

public sealed record ConfirmationToken(Guid Id, string RequestFingerprint);

public sealed class CommandRiskPolicy
{
    private readonly HashSet<Guid> _activeTokens = [];

    public bool RequiresConfirmation(CommandRequest request) => request.Risk != FeatureRisk.Normal;

    public ConfirmationToken Issue(CommandRequest request)
    {
        if (!RequiresConfirmation(request))
        {
            throw new InvalidOperationException("一般風險命令不需要二次確認 token。");
        }

        var token = new ConfirmationToken(Guid.NewGuid(), request.GetConfirmationFingerprint());
        _activeTokens.Add(token.Id);
        return token;
    }

    public bool ValidateAndConsume(CommandRequest request, ConfirmationToken? token)
    {
        if (!RequiresConfirmation(request))
        {
            return true;
        }

        return token is not null &&
               string.Equals(token.RequestFingerprint, request.GetConfirmationFingerprint(), StringComparison.Ordinal) &&
               _activeTokens.Remove(token.Id);
    }
}
