using System;
using System.Collections.Generic;

namespace AsyncApi.Data.Entities;

/// <summary>
/// Stores the historic data a.k.a the audit trail
/// </summary>
public partial class RowLog
{
    /// <summary>
    /// The Primary Key
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    ///  The implicit link to a table&apos;s row
    /// </summary>
    public long AuditId { get; set; }

    /// <summary>
    /// Concatenated information of table event
    /// </summary>
    public string EventKey { get; set; } = null!;

    /// <summary>
    /// The old values of changed columns in a JSONB object
    /// </summary>
    public string? OldData { get; set; }

    /// <summary>
    /// The new values of changed columns in a JSONB object
    /// </summary>
    public string? NewData { get; set; }
}
