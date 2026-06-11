using System;
using System.Collections.Generic;

namespace AsyncApi.Data.Entities;

public partial class AuditTablesDependency
{
    /// <summary>
    /// The OID of the table
    /// </summary>
    public uint? Relid { get; set; }

    /// <summary>
    /// The tracing log ID from audit_table_log
    /// </summary>
    public int? TableLogId { get; set; }

    /// <summary>
    /// The name of the table
    /// </summary>
    public string? Tablename { get; set; }

    /// <summary>
    /// The depth of foreign key references
    /// </summary>
    public int? Depth { get; set; }
}
