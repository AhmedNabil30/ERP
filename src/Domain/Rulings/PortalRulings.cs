namespace Kaff.Domain.Rulings;

/// <summary>R5-Q1: whether the client portal is a separate deployment or the same API behind a second web address.</summary>
public sealed record PortalDeployment : IRuling
{
    public static readonly PortalDeployment Default = new();

    public RulingNote Note => new(
        "PortalDeployment",
        "R5-Q1: is the client portal a separate deployment, or the same API behind a second web address? (Nabil and the Architect.)",
        "Where the portal's routes, cookie and sign-in live relative to the staff application.",
        "Same API process. Every portal route is under /api/portal, its own types, its own sign-in, and a role-and-ownership gate "
        + "(Permission.PortalRead / PortalApprove, Role.Client only). When Kaff:PortalHost is set, a request to that host name is "
        + "answered only under /api/portal; every other path is 404. The session cookie carries the __Host- prefix, so a browser "
        + "never sends the staff host's cookie to the portal host or the reverse. ASSUMED.",
        "A separate deployment would move src/Api/Features/Portal into its own host project and give it its own JwtOptions audience "
        + "and signing key. The response types already share nothing with the staff features, so the move is mechanical.",
        RulingStatus.Assumed);
}

/// <summary>R5-Q7: how a client gets his first password and a reset.</summary>
public sealed record ClientCredentialHandover : IRuling
{
    public static readonly ClientCredentialHandover Default = new();

    public RulingNote Note => new(
        "ClientCredentialHandover",
        "R5-Q7: how does a client get his first password, and a reset?",
        "Who creates a portal account, how the first password reaches the client, and what happens at first use.",
        "The Owner creates the client user (POST /api/users, Role.Client, bound to one client file) and hands the temporary "
        + "password over from his own phone, as for staff. The first sign-in succeeds, and every portal read is then refused with "
        + "errors.auth.password_change_required until the client sets his own password. A reset is the Owner setting a new "
        + "temporary password. Lockout follows the staff numbers. No SMS or WhatsApp provider exists. ASSUMED.",
        "If a reset link or a message provider is ruled in, add a portal reset endpoint and a delivery service; the sign-in and "
        + "forced-change code do not change.",
        RulingStatus.Assumed);
}

/// <summary>R5-Q8: what the client sees of an extract, and how remaining is worked out.</summary>
public sealed record PortalExtractVisibility : IRuling
{
    public static readonly PortalExtractVisibility Default = new();

    public RulingNote Note => new(
        "PortalExtractVisibility",
        "R5-Q8: what does the client see of an extract, and how is remaining worked out?",
        "Which extracts the portal lists, which figures it shows, and the formula for paid and remaining.",
        "Only Issued and Collected (paid) extracts are listed; Draft, in-review, Disputed and Void ones are absent. Each shows "
        + "its number, period, certified work, net payable and the cash the client has paid on it. The project home shows paid = "
        + "all cash received from the client on the project (the advance at signing and every collection, net of reversals), and remaining = revised contract value less paid. No hold, advance or deduction line "
        + "is shown. Amounts are rounded to two decimals at the last step (spec.md section 15 amendment). ASSUMED.",
        "Line detail would add a second portal extract type; the visibility cut is one status filter in "
        + "Features/Portal/PortalQueries.cs.",
        RulingStatus.Assumed);
}

/// <summary>R5-Q12: who publishes a site photo, and may it be taken back.</summary>
public sealed record PhotoPublishers : IRuling
{
    public static readonly PhotoPublishers Default = new();

    public RulingNote Note => new(
        "PhotoPublishers",
        "R5-Q12: who publishes a site photo to the client, and may it be taken back?",
        "The holders of Permission.SitePhotoPublish.",
        "The supervising site engineer and the Technical Office publish (a junior engineer does not). There is no unpublish. "
        + "ASSUMED; the unpublish half is not built.",
        "Change the SitePhotoPublish row of PermissionCatalogue. Unpublish would add PhotoEntry.Unpublish and one route.",
        RulingStatus.Assumed);
}

/// <summary>R5-Q3: the client's own decision replaces the engineer recording it.</summary>
public sealed record ClientDecidesChangeOrders : IRuling
{
    public static readonly ClientDecidesChangeOrders Default = new();

    public RulingNote Note => new(
        "ClientDecidesChangeOrders",
        "R5-Q3: when the portal opens, does the client's own tap replace the engineer recording his approval?",
        "Who moves a priced change order out of ClientApproval, and what a client rejection does.",
        "The client approves or rejects through the portal (Permission.PortalApprove on his own project). Approval moves it to "
        + "OwnerApproval with the Client user as actor; a rejection needs a reason and ends it at Rejected. The portal lists a "
        + "change order from ClientApproval onward. The engineer's record-approval route is NOT removed yet: it stays until the "
        + "portal is live, and the first decision wins because the status check refuses the second. ASSUMED.",
        "To retire the engineer's route, delete the RecordClientApproval and client-rejection endpoints and the "
        + "ChangeOrderClientApprovalRecord row.",
        RulingStatus.Assumed);
}
