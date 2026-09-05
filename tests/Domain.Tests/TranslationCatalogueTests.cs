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
    /// KAFF-120, <c>AC-120-H</c> — §6.7's refusal has exactly one key, and finding F-08 stays closed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two documents carried two spellings of one refusal: the KAFF-120 story said
    /// <c>errors.master.individual_client_does_not_withhold</c> and <c>ux/slice-1-flows.md</c> S-012
    /// said <c>errors.master.individual_does_not_withhold</c>. Only the second exists. A second key
    /// invented from the first document would pass
    /// <see cref="The_two_catalogues_describe_the_same_set_of_keys"/> the moment somebody added it to
    /// both files, and would render a refusal nobody had translated the day a handler used it.
    /// </para>
    /// <para>
    /// <b>Written as a whitelist over everything that mentions withholding, not as "this one wrong
    /// key is absent".</b> An absence test for a string nobody was going to type is a test that
    /// cannot fail (D-106). This one fails on <i>any</i> new withholding key — including the right
    /// one spelled a second way. <b>Slice 3 will legitimately add keys here</b> (KAFF-317, KAFF-318:
    /// the withholding Kaff carries as a liability on subcontractor and supplier payments); adding
    /// them to this list is the deliberate edit this test exists to force.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_withholding_refusal_has_exactly_one_key_in_the_code_and_in_both_catalogues()
    {
        const string TheKey = "errors.master.individual_does_not_withhold";

        static bool MentionsWithholding(string key) =>
            key.Contains("withhold", StringComparison.OrdinalIgnoreCase);

        foreach (string path in new[] { ArabicCatalogue, EnglishCatalogue })
        {
            LoadCatalogue(path).Keys.Where(MentionsWithholding).Should().BeEquivalentTo(
                [TheKey],
                $"{path} — §6.7 has one refusal, and a second spelling of it is a message that will "
                + "reach a screen untranslated");
        }

        DomainErrorKeys()
            .Where(entry => MentionsWithholding(entry.Key))
            .Select(entry => entry.Key)
            .Distinct(StringComparer.Ordinal)
            .Should().BeEquivalentTo(
                [TheKey],
                "the domain declares one withholding refusal; Client.SetClassification and "
                + "Project.SetWithholding return the same Error because they refuse the same claim");
    }

    /// <summary>
    /// KAFF-127, <c>AC-127-H</c> — <b>"no key is added that no template uses"</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The other half of <c>AC-127-H</c> is already covered:
    /// <see cref="The_two_catalogues_describe_the_same_set_of_keys"/> says every key exists in both
    /// files. That says nothing about whether anything <i>reads</i> them, and a catalogue accumulates
    /// keys for screens that were redesigned or never built — <c>landing.pending.*</c> and
    /// <c>nav.home</c> were three such entries on 2026-09-05, orphaned when the last role that
    /// reached that surface got a real one.
    /// </para>
    /// <para>
    /// <b>Scoped to the namespaces a screen owns</b>, not to the whole catalogue. <c>errors.*</c> keys
    /// are the backend's and are resolved from a <c>ProblemDetails</c> at run time, so their names
    /// appear in C# rather than in a template — a repository-wide version of this test would demand
    /// every domain error be typed into a template, which is exactly the coupling
    /// <c>Error.MessageKey</c> exists to avoid. <c>enum.*</c> keys are built by the exhaustive
    /// switches in <c>enum-keys.ts</c> and never appear as literals either. What is left is the text a
    /// screen writes down, and a screen is the only thing that can read it.
    /// </para>
    /// <para>
    /// <b>The positive control is the prefix list itself</b> (D-116 §3): if the sources ever stop
    /// being readable — a moved directory, an empty glob — <see cref="WebSourceText"/> asserts it
    /// found real files with real content, so "every key is used" cannot pass by having searched
    /// nothing.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_screen_key_in_the_catalogues_is_read_by_a_template_or_a_component()
    {
        string[] screenPrefixes =
        [
            "users.",
            "clients.",
            "profile.",
            "hr.",
            "nav.",
            "shell.",
            "auth.",
            "not_found.",
            "landing.",
            "a11y.",
            "action.",
            "validation.",
        ];

        string sources = WebSourceText();

        IEnumerable<string> orphans = LoadCatalogue(ArabicCatalogue).Keys
            .Where(key => screenPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal)))
            .Where(key => !sources.Contains($"'{key}'", StringComparison.Ordinal)
                          && !sources.Contains($"\"{key}\"", StringComparison.Ordinal));

        orphans.Should().BeEmpty(
            "AC-127-H — a key no screen reads is a translation somebody maintains for nothing, and "
            + "the next reader cannot tell it apart from one whose screen is merely not built yet");
    }

    /// <summary>
    /// Every `.ts` and `.html` under <c>src/Web/src</c>, concatenated.
    /// </summary>
    /// <remarks>
    /// Asserts it actually read something. An absence test over an empty haystack finds nothing wrong
    /// with everything — D-116 §3, and the reason two of three tests there went red on their positive
    /// control rather than on the assertion they were named for.
    /// </remarks>
    private static string WebSourceText()
    {
        string root = Path.Combine(RepositoryRoot(), "src", "Web", "src");

        Directory.Exists(root).Should().BeTrue($"the web sources must be at {root}");

        string[] files =
        [
            .. Directory.EnumerateFiles(root, "*.ts", SearchOption.AllDirectories),
            .. Directory.EnumerateFiles(root, "*.html", SearchOption.AllDirectories),
        ];

        files.Length.Should().BeGreaterThan(
            20, "the search must have found the application, not an empty directory");

        string text = string.Concat(files.Select(File.ReadAllText));

        text.Should().Contain(
            "i18n.t(", "the concatenated sources must contain the call this test is searching for");

        return text;
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
