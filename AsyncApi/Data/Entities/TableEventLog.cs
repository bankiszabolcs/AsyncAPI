using System;
using System.Collections.Generic;

namespace AsyncApi.Data.Entities;

/// <summary>
/// Stores metadata about different kind of events happening during one transaction against one table
/// </summary>
public partial class TableEventLog
{
    /// <summary>
    /// The Primary Key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign Key to transaction_log table
    /// </summary>
    public int TransactionId { get; set; }

    /// <summary>
    /// Stores the result of statement_timestamp() function
    /// </summary>
    public DateTime StmtTime { get; set; }

    /// <summary>
    /// ID of event type
    /// </summary>
    public short OpId { get; set; }

    /// <summary>
    /// Text for of event type
    /// </summary>
    public string? TableOperation { get; set; }

    /// <summary>
    /// Name of table that fired the trigger
    /// </summary>
    public string TableName { get; set; } = null!;

    /// <summary>
    /// Schema of firing table
    /// </summary>
    public string SchemaName { get; set; } = null!;

    /// <summary>
    /// Concatenated information of most columns
    /// </summary>
    public string EventKey { get; set; } = null!;

    public virtual TransactionLog Transaction { get; set; } = null!;
}
