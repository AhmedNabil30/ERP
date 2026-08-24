using System.Reflection;
using System.Text.Json;
using Kaff.Domain.Common;

namespace Kaff.Domain.Tests;

/// <summary>
/// Every domain error must be sayable, in both languages.
/// </summary>
/// <remarks>
/// <para>
/// CLAUDE.md: "No hardcoded user-facing strings. Everything through i18n from the first commit."
/// <see cref="Error"/> honours that by carrying a <c>MessageKey</c> rather than a sentence — but a
/// key with no entry behind it is not translated text, it is a raw key rendered at the user. The
/// compiler cannot see the gap, because one side is C# and the other is JSON.
/// </para>
/// <para>
/// This is the test that would have caught it. It was added on 2026-08-20 after
/// <c>errors.identity.hr_role_requires_hr_department</c> shipped into the domain with no entry in
/// either catalogue, and was found by an agent reading the files rather than by anything automated.
/// Adding an error is the most routine change in this codebase; it should not depend on somebody
/// remembering two files.
/// </para>
/// <para>
/// A Domain test reaching into <c>src/Web</c> is unusual and deliberate: the contract being checked
/// has one end in each place, so it belongs to neither alone. Domain.Tests is the cheaper home —
/// it needs no database and runs first.
/// </para>
/// </remarks>
public sealed class TranslationCatalogueTests
{
    private const string ArabicCatalogue = "src/Web/public/locales/ar.json";
    private const string EnglishCatalogue = "src/Web/public/locales/en.json";

    [Fact]
    public void Every_domain_error_key_has_an_arabic_and_an_english_translation()
    {
        Dictionary<string, string> arabic = LoadCatalogue(ArabicCatalogue);
        Dictionary<string, string> english = LoadCatalogue(EnglishCatalogue);

        List<string> missing = [];

        foreach ((string key, string owner) in DomainErrorKeys())
        {
            if (!arabic.ContainsKey(key))
            {
                missing.Add($"{key} (from {owner}) — missing from ar.json");
            }

            if (!english.ContainsKey(key))
            {
                missing.Add($"{key} (from {owner}) — missing from en.json");
            }
        }

        missing.Should().BeEmpty(
            "an error key with no entry renders as itself: the user is shown "
            + "'errors.identity.hr_role_requires_hr_department' instead of a sentence");
    }

    [Fact]
    public void The_two_catalogues_describe_the_same_set_of_keys()
    {
        // Arabic is the product language and English is the second, so drift shows up as a key
        // added to one and forgotten in the other. Either direction is a defect: a missing Arabic
        // entry is visible to every user, and a missing English one is visible to whoever is
        // reading the system in English to review it.
        Dictionary<string, string> arabic = LoadCatalogue(ArabicCatalogue);
        Dictionary<string, string> english = LoadCatalogue(EnglishCatalogue);

        IEnumerable<string> onlyInArabic = arabic.Keys.Except(english.Keys, StringComparer.Ordinal);
        IEnumerable<string> onlyInEnglish = english.Keys.Except(arabic.Keys, StringComparer.Ordinal);

        onlyInArabic.Should().BeEmpty("these keys have no English translation");
        onlyInEnglish.Should().BeEmpty("these keys have no Arabic translation");
    }

    [Fact]
    public void No_translation_is_left_empty()
    {
        // An empty string is worse than a missing key: the key at least renders as something the
        // reader can search for, and a blank label renders as nothing at all.
        foreach (string path in new[] { ArabicCatalogue, EnglishCatalogue })
        {
            Dictionary<string, string> catalogue = LoadCatalogue(path);

            catalogue
                .Where(entry => !entry.Key.StartsWith('_') && string.IsNullOrWhiteSpace(entry.Value))
                .Select(entry => entry.Key)
                .Should().BeEmpty($"{path} has blank entries");
        }
    }

    /// <summary>
    /// Every <see cref="Error"/> declared on a <c>*Errors</c> catalogue class, with the class it
    /// came from so a failure names the file to edit.
    /// </summary>
    private static IEnumerable<(string Key, string Owner)> DomainErrorKeys()
    {
        IEnumerable<Type> catalogues = typeof(Error).Assembly
            .GetTypes()
            .Where(type => type.IsClass && type.IsAbstract && type.IsSealed) // static classes
            .Where(type => type.Name.EndsWith("Errors", StringComparison.Ordinal));

        foreach (Type catalogue in catalogues)
        {
            IEnumerable<FieldInfo> fields = catalogue
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.FieldType == typeof(Error));

            foreach (FieldInfo field in fields)
            {
                if (field.GetValue(null) is not Error error || string.IsNullOrWhiteSpace(error.MessageKey))
                {
                    continue;
                }

                yield return (error.MessageKey, $"{catalogue.Name}.{field.Name}");
            }
        }
    }

    private static Dictionary<string, string> LoadCatalogue(string relativePath)
    {
        string full = Path.Combine(RepositoryRoot(), relativePath);

        File.Exists(full).Should().BeTrue($"the translation catalogue must exist at {relativePath}");

        using FileStream stream = File.OpenRead(full);
        using JsonDocument document = JsonDocument.Parse(stream);

        return document.RootElement
            .EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => property.Value.GetString() ?? string.Empty,
                StringComparer.Ordinal);
    }

    /// <summary>
    /// Walks up from the test binary to the directory holding the solution.
    /// </summary>
    /// <remarks>
    /// Located by the solution file rather than by counting <c>../</c> segments, so the test keeps
    /// working if the output path changes with a configuration or a target framework.
    /// </remarks>
    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "KaffErp.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests must run from inside the repository");

        return directory!.FullName;
    }
}
