using System;
using System.Collections.Generic;

namespace AsyncApi.Data.Entities;

public partial class AuditTable
{
    /// <summary>
    /// Name of the audit_id column added to the audited table
    /// </summary>
    public string? AuditIdColumn { get; set; }

    /// <summary>
    /// Flag that shows if old values are logged for audited table
    /// </summary>
    public bool? LogOldData { get; set; }

    /// <summary>
    /// Flag that shows if new values are logged for audited table
    /// </summary>
    public bool? LogNewData { get; set; }

    /// <summary>
    /// The minimal transaction ID referenced to the audited table in the table_event_log
    /// </summary>
    public int? TxidMin { get; set; }

    /// <summary>
    /// The maximal transaction ID referenced to the audited table in the table_event_log
    /// </summary>
    public int? TxidMax { get; set; }

    /// <summary>
    /// Flag, that shows if logging is activated for the table or not
    /// </summary>
    public bool? TgIsActive { get; set; }
}
