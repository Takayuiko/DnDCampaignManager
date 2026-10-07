using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using DnDCampingManager.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DnDCampaignManager.Api.Services.AI;

// A session advisory lock coordinates API instances without a transaction during AI calls.
internal sealed class ConversationOperation(DnDxDbContext db, long key, bool opened) : IAsyncDisposable
{
    internal static async Task<ConversationOperation?> TryAcquireAsync(DnDxDbContext db, Guid id, CancellationToken ct)
    {
        var key = BinaryPrimitives.ReadInt64LittleEndian(SHA256.HashData(id.ToByteArray()));
        var opened = db.Database.GetDbConnection().State != ConnectionState.Open;
        if (opened) await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT pg_try_advisory_lock(@key)";
            command.Parameters.Add(new NpgsqlParameter("key", key));
            if (db.Database.CurrentTransaction is { } transaction)
                command.Transaction = Microsoft.EntityFrameworkCore.Storage.DbContextTransactionExtensions.GetDbTransaction(transaction);
            if (await command.ExecuteScalarAsync(ct) is true)
                return new ConversationOperation(db, key, opened);
            if (opened) await db.Database.CloseConnectionAsync();
            return null;
        }
        catch
        {
            // Cancellation can happen after PostgreSQL acquired the lock but before we read the result.
            NpgsqlConnection.ClearPool((NpgsqlConnection)db.Database.GetDbConnection());
            if (opened) await db.Database.CloseConnectionAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT pg_advisory_unlock(@key)";
            command.Parameters.Add(new NpgsqlParameter("key", key));
            if (db.Database.CurrentTransaction is { } transaction)
                command.Transaction = Microsoft.EntityFrameworkCore.Storage.DbContextTransactionExtensions.GetDbTransaction(transaction);
            await command.ExecuteScalarAsync(CancellationToken.None);
        }
        catch
        {
            // Never return a session whose lock may still be held to the connection pool.
            NpgsqlConnection.ClearPool((NpgsqlConnection)db.Database.GetDbConnection());
            throw;
        }
        finally
        {
            if (opened) await db.Database.CloseConnectionAsync();
        }
    }
}
