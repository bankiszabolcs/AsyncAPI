using System;
using System.Collections.Generic;

namespace AsyncApi.Data.Entities;

/// <summary>
/// Stores metadata about each transaction
/// </summary>
public partial class TransactionLog
{
    /// <summary>
    /// The Primary Key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The internal transaction ID by PostgreSQL (can cycle)
    /// </summary>
    public long Txid { get; set; }

    /// <summary>
    /// Stores the result of transaction_timestamp() function
    /// </summary>
    public DateTime TxidTime { get; set; }

    /// <summary>
    /// Stores the result of pg_backend_pid() function
    /// </summary>
    public int? ProcessId { get; set; }

    /// <summary>
    /// Stores the result of session_user function
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Stores the result of inet_client_addr() function
    /// </summary>
    public string? ClientName { get; set; }

    /// <summary>
    /// Stores the result of inet_client_port() function
    /// </summary>
    public int? ClientPort { get; set; }

    /// <summary>
    /// Stores the output of current_setting(&apos;application_name&apos;)
    /// </summary>
    public string? ApplicationName { get; set; }

    /// <summary>
    /// Stores any infos a client/user defines beforehand with set_config
    /// </summary>
    public string? SessionInfo { get; set; }

    public virtual ICollection<TableEventLog> TableEventLogs { get; set; } = new List<TableEventLog>();
}
