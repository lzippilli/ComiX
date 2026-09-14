namespace ComiX;

/// <summary>
/// A person credited on a comic, together with the role they are credited for.
/// </summary>
/// <param name="Name">The person's name, exactly as written in the source metadata.</param>
/// <param name="Role">The role they are credited for.</param>
/// <remarks>
/// Credits are modelled as a list rather than as fixed per-role properties, so that several people
/// may share a role and roles specific to one standard require no schema change. A person credited
/// for two roles produces two entries; group by <see cref="Name"/> for one entry per person.
/// </remarks>
public sealed record ComicCredit(string Name, ComicCreditRole Role);
