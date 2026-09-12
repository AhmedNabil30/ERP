using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Kaff.Domain.Common.Serialization;

/// <summary>
/// The single JSON configuration used for audit before/after snapshots and for API payloads.
/// </summary>
/// <remarks>
/// decisions.md D-135: every <c>decimal</c> — bare, <c>Money</c> or <c>Percentage</c> — is written as
/// a JSON string, exact and lossless, because a JSON number crosses a browser through an IEEE-754
/// double and a <c>decimal</c> does not survive that round trip
/// (<c>12345678901234.5678</c> measured back as <c>…4.5680</c>, <c>VRF-FIXTURE-011</c>). Reading
/// still accepts a bare number too — a .NET caller serialises <c>decimal</c> exactly, and audit
/// snapshots written before this ruling hold money as numbers. The browser is held to strings by its
/// TypeScript types, not by the server.
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
        options.Converters.Add(new DecimalJsonConverter());
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

/// <summary>
/// D-135: every bare <c>decimal</c> member of a Request or Response record. Writes a string; reads a
/// string through <see cref="DecimalText"/> or, for a .NET caller or a pre-ruling audit snapshot, a
/// JSON number. <c>decimal?</c> is derived by System.Text.Json from this converter on its own.
/// </summary>
internal sealed class DecimalJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ReadDecimal(ref reader);

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Shared by every decimal-shaped converter here — Money and Percentage included.</summary>
    internal static decimal ReadDecimal(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            if (!DecimalText.TryParse(reader.GetString(), out decimal value))
            {
                throw new JsonException("Expected a decimal string matching the D-135 wire grammar.");
            }

            return value;
        }

        return reader.GetDecimal();
    }
}

internal sealed class MoneyJsonConverter : JsonConverter<Money>
{
    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(DecimalJsonConverter.ReadDecimal(ref reader));

    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.Amount.ToString(CultureInfo.InvariantCulture));
    }
}

internal sealed class PercentageJsonConverter : JsonConverter<Percentage>
{
    public override Percentage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        decimal fraction = DecimalJsonConverter.ReadDecimal(ref reader);

        try
        {
            return Percentage.FromFraction(fraction);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            // D-151 §6b: a negative rate is refused by the type itself, and that refusal must surface
            // as a JsonException so the framework turns it into a 400 (ApiErrors.MalformedBody) rather
            // than an unhandled ArgumentOutOfRangeException reaching UseExceptionHandler as a 500.
            throw new JsonException("A rate must not be negative.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, Percentage value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.Fraction.ToString(CultureInfo.InvariantCulture));
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
