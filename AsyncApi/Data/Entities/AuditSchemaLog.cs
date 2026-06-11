using System;
using System.Collections.Generic;
using NpgsqlTypes;

namespace AsyncApi.Data.Entities;

/// <summary>
/// Stores information about how pgMemento is configured in audited database schema
/// </summary>
public partial class AuditSchemaLog
{
    /// <summary>
    /// The Primary Key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// ID to trace a changing database schema
    /// </summary>
    public int LogId { get; set; }

    /// <summary>
    /// The name of the database schema
    /// </summary>
    public string SchemaName { get; set; } = null!;

    /// <summary>
    /// The default name for the audit_id column added to audited tables
    /// </summary>
    public string DefaultAuditIdColumn { get; set; } = null!;

    /// <summary>
    /// Default setting for tables to log old values
    /// </summary>
    public bool? DefaultLogOldData { get; set; }

    /// <summary>
    /// Default setting for tables to log new values
    /// </summary>
    public bool? DefaultLogNewData { get; set; }

    /// <summary>
    /// Flag that shows if pgMemento starts auditing for newly created tables
    /// </summary>
    public bool? TriggerCreateTable { get; set; }

    /// <summary>
    /// Stores the transaction IDs when pgMemento has been activated or stopped in the schema
    /// </summary>
    public NpgsqlRange<decimal>? TxidRange { get; set; }
}
