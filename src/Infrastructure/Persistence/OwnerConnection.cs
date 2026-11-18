namespace Kaff.Infrastructure.Persistence;

/// <summary>
/// Switches one DI scope's <see cref="KaffDbContext"/> to the schema owner's connection.
/// </summary>
/// <remarks>
/// Start-up only: migrations, the guard scripts and the seeders need the owner, and nothing else does. A scope that
/// never calls <see cref="Use"/>, which is every request, connects as the least-privilege application role. Call it
/// before the scope resolves its first context; the context's options are built once per scope.
/// </remarks>
public sealed class OwnerConnection
{
    public bool InUse { get; private set; }

    public void Use() => InUse = true;
}
