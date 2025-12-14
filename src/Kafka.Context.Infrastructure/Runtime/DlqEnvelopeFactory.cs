using Kafka.Context.Messaging;
using System.Security.Cryptography;
using System.Text;

namespace Kafka.Context.Infrastructure.Runtime;

internal static class DlqEnvelopeFactory
{
    public static DlqEnvelope From(
        string sourceTopic,
        int partition,
        long offset,
        string timestampUtc,
        Dictionary<string, string> headers,
        Exception ex,
        string ingestedAtUtc)
    {
        var errorMessageShort = ex.Message ?? string.Empty;
        var stackTraceShort = ex.StackTrace;

        var fingerprintInput = $"{errorMessageShort}\n{NormalizeWhitespace(stackTraceShort)}";
        var fingerprintBytes = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintInput));
        var fingerprint = Convert.ToHexString(fingerprintBytes);

        return new DlqEnvelope
        {
            Topic = sourceTopic,
            Partition = partition,
            Offset = offset,
            TimestampUtc = timestampUtc,
            IngestedAtUtc = ingestedAtUtc,
            PayloadFormatKey = "none",
            PayloadFormatValue = "avro",
            SchemaIdKey = string.Empty,
            SchemaIdValue = string.Empty,
            KeyIsNull = true,
            ErrorType = ex.GetType().Name,
            ErrorMessageShort = errorMessageShort,
            StackTraceShort = stackTraceShort,
            ErrorFingerprint = fingerprint,
            Headers = headers
        };
    }

    private static string NormalizeWhitespace(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return string.Join(" ", value.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
    }
}

