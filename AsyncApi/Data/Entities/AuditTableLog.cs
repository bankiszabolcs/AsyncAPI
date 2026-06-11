using System;
using System.Collections.Generic;
using NpgsqlTypes;

namespace AsyncApi.Data.Entities;

/// <summary>
/// Stores information about audited tables, which is important when restoring a whole schema or database
/// </summary>
public partial class AuditTableLog
{
    /// <summary>
    /// The Primary Key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// ID to trace a changing table
    /// </summary>
    public int LogId { get; set; }

    /// <summary>
    /// [DEPRECATED] The table&apos;s OID to trace a table when changed
    /// </summary>
    public uint? Relid { get; set; }

    /// <summary>
    /// The name of the table
    /// </summary>
    public string TableName { get; set; } = null!;

    /// <summary>
    /// The schema the table belongs to
    /// </summary>
    public string SchemaName { get; set; } = null!;

    /// <summary>
    /// The name for the audit_id column added to the audited table
    /// </summary>
    public string AuditIdColumn { get; set; } = null!;

    /// <summary>
    /// Flag that shows if old values are logged for audited table
    /// </summary>
    public bool LogOldData { get; set; }

    /// <summary>
    /// Flag that shows if new values are logged for audited table
    /// </summary>
    public bool LogNewData { get; set; }

    /// <summary>
    /// Stores the transaction IDs when the table has been created and dropped
    /// </summary>
    public NpgsqlRange<decimal>? TxidRange { get; set; }

    public virtual ICollection<AuditColumnLog> AuditColumnLogs { get; set; } = new List<AuditColumnLog>();
}
