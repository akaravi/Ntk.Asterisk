namespace Ntk.Asterisk.WebApi.Configuration;

/// <summary>Queue Panel multi-user ACL (Q13). Gate active when RequireLogin and at least one enabled user.</summary>
public sealed class QueueAclOptions
{
    public const string SectionName = "QueueAcl";
    public const string TokenHeaderName = "X-Queue-Acl-Token";

    /// <summary>When true and users exist, Queues APIs require a valid session token.</summary>
    public bool RequireLogin { get; set; }

    /// <summary>Session token lifetime in minutes (default 8 hours).</summary>
    public int TokenTtlMinutes { get; set; } = 480;
}
