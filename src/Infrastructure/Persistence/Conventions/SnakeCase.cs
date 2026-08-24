using System.Globalization;
using System.Text;

namespace Kaff.Infrastructure.Persistence.Conventions;

/// <summary>
/// PascalCase to snake_case for database identifiers.
/// </summary>
/// <remarks>
/// Written here rather than taken from a package. PostgreSQL folds unquoted identifiers to lower
/// case, so a PascalCase model produces columns that must be quoted in every hand-written statement —
/// and this system has hand-written statements that matter: the append-only triggers, the
/// non-negative balance guard and the balances view. Readable SQL in the guards is worth fifty lines
/// here, and it avoids a dependency that only reformats strings.
/// </remarks>
internal static class SnakeCase
{
    public static string Convert(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var builder = new StringBuilder(name.Length + 8);

        for (int i = 0; i < name.Length; i++)
        {
            char current = name[i];

            if (char.IsUpper(current))
            {
                bool previousIsLowerOrDigit = i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]));
                bool startsNewWord = i > 0 && i + 1 < name.Length && char.IsUpper(name[i - 1]) && char.IsLower(name[i + 1]);

                if (previousIsLowerOrDigit || startsNewWord)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLower(current, CultureInfo.InvariantCulture));
            }
            else
            {
                builder.Append(current);
            }
        }

        return builder.ToString();
    }
}
