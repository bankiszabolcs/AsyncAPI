using System;
using System.Collections.Generic;
using NpgsqlTypes;

namespace AsyncApi.Data.Entities;

/// <summary>
/// Stores information about audited columns, which is important when restoring previous versions of tuples and tables
/// </summary>
public partial class AuditColumnLog
{
    /// <summary>
    /// The Primary Key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign Key to pgmemento.audit_table_log
    /// </summary>
    public int AuditTableId { get; set; }

    /// <summary>
    /// The name of the column
    /// </summary>
    public string ColumnName { get; set; } = null!;

    /// <summary>
    /// The ordinal position within the table
    /// </summary>
    public int? OrdinalPosition { get; set; }

    /// <summary>
    /// The column&apos;s data type (incl typemods)
    /// </summary>
    public string? DataType { get; set; }

    /// <summary>
    /// The column&apos;s default expression
    /// </summary>
    public string? ColumnDefault { get; set; }

    /// <summary>
    /// A flag to tell, if the column is a NOT NULL column or not
    /// </summary>
    public bool? NotNull { get; set; }

    /// <summary>
    /// Stores the transaction IDs when the column has been created and dropped
    /// </summary>
    public NpgsqlRange<decimal>? TxidRange { get; set; }

    public virtual AuditTableLog AuditTable { get; set; } = null!;
}
