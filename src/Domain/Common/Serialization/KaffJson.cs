using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Kaff.Domain.Common.Serialization;

/// <summary>
/// The single JSON configuration used for audit before/after snapshots and for API payloads.
/// </summary>
/// <remarks>
/// Money is written as a bare JSON number carrying its exact decimal value. System.Text.Json writes
/// <c>decimal</c> losslessly, so an audit record can be replayed against the ledger without drift.
/// A floating-point round trip anywhere in this path would silently corrupt the evidence trail.
/// </remarks>
public static class KaffJson
{
    public static readonly JsonSerializerOptions Options = Build();

    private static JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            NumberHandling = JsonNumberHandling.Strict,
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new MoneyJsonConverter());
        options.Converters.Add(new PercentageJsonConverter());
        options.Converters.Add(new PhoneNumberJsonConverter());
        // A TypeInfoResolver must be set before the options are frozen: the parameterless
        // MakeReadOnly() refuses to infer one, because doing so would silently opt the
        // application into reflection-based serialisation. Without this every audit write
        // throws on the first save — which is exactly what happened the first time these
        // options met a real database. See decisions.md D-041.
        options.TypeInfoResolver = new DefaultJsonTypeInfoResolver();
        options.MakeReadOnly();

        return options;
    }
}

internal sealed class MoneyJsonConverter : JsonConverter<Money>
{
    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetDecimal());

    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue(value.Amount);
    }
}

internal sealed class PercentageJsonConverter : JsonConverter<Percentage>
{
    public override Percentage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Percentage.FromFraction(reader.GetDecimal());

    public override void Write(Utf8JsonWriter writer, Percentage value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue(value.Fraction);
    }
}

internal sealed class PhoneNumberJsonConverter : JsonConverter<PhoneNumber>
{
    public override PhoneNumber Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string raw = reader.GetString() ?? string.Empty;
        Result<PhoneNumber> result = PhoneNumber.Create(raw);
        return result.IsSuccess ? result.Value : PhoneNumber.FromStorage(raw, raw);
    }

    public override void Write(Utf8JsonWriter writer, PhoneNumber value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.Entered);
    }
}
