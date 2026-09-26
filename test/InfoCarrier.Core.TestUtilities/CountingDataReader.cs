// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections;
using System.Data;
using System.Data.Common;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     A reader that counts every <see cref="Read" /> and <see cref="ReadAsync" />, the last one that
///     returns false included, and passes every call to the reader it wraps (#167).
/// </summary>
/// <remarks>
///     <para>
///         <b>Why not EF's own <c>ReadCount</c>.</b> EF counts in <c>RelationalDataReader.Read</c>,
///         and <c>GroupBySingleQueryingEnumerable</c> reads a group's first row through it and the
///         rest from the raw <see cref="DbDataReader" />, so plain EF reported 2 reads for a final
///         <c>GroupBy</c> of 92 rows, and 35 methods of the spike's Tier B run differed only there.
///         This reader is the raw one EF holds, so every read passes through it.
///     </para>
///     <para>
///         <b>Only in a slow run</b> (<see cref="LiveComparison.IsEnabled" />), and on both halves.
///         A provider that reads its own reader type would meet one it did not create, which is the
///         review's reason for EF's count; the risk stays confined to the slow run, where it would
///         fail both halves at once.
///     </para>
/// </remarks>
internal sealed class CountingDataReader(DbDataReader inner) : DbDataReader
{
    /// <summary>The calls to <see cref="Read" /> and <see cref="ReadAsync" /> so far.</summary>
    public int Reads { get; private set; }

    public override int Depth => inner.Depth;

    public override int FieldCount => inner.FieldCount;

    public override bool HasRows => inner.HasRows;

    public override bool IsClosed => inner.IsClosed;

    public override int RecordsAffected => inner.RecordsAffected;

    public override int VisibleFieldCount => inner.VisibleFieldCount;

    public override object this[int ordinal] => inner[ordinal];

    public override object this[string name] => inner[name];

    public override bool Read()
    {
        Reads++;
        return inner.Read();
    }

    public override Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        Reads++;
        return inner.ReadAsync(cancellationToken);
    }

    public override bool NextResult()
        => inner.NextResult();

    public override Task<bool> NextResultAsync(CancellationToken cancellationToken)
        => inner.NextResultAsync(cancellationToken);

    public override bool GetBoolean(int ordinal) => inner.GetBoolean(ordinal);

    public override byte GetByte(int ordinal) => inner.GetByte(ordinal);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
        => inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);

    public override char GetChar(int ordinal) => inner.GetChar(ordinal);

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
        => inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);

    public override string GetDataTypeName(int ordinal) => inner.GetDataTypeName(ordinal);

    public override DateTime GetDateTime(int ordinal) => inner.GetDateTime(ordinal);

    public override decimal GetDecimal(int ordinal) => inner.GetDecimal(ordinal);

    public override double GetDouble(int ordinal) => inner.GetDouble(ordinal);

    public override Type GetFieldType(int ordinal) => inner.GetFieldType(ordinal);

    public override float GetFloat(int ordinal) => inner.GetFloat(ordinal);

    public override Guid GetGuid(int ordinal) => inner.GetGuid(ordinal);

    public override short GetInt16(int ordinal) => inner.GetInt16(ordinal);

    public override int GetInt32(int ordinal) => inner.GetInt32(ordinal);

    public override long GetInt64(int ordinal) => inner.GetInt64(ordinal);

    public override string GetName(int ordinal) => inner.GetName(ordinal);

    public override int GetOrdinal(string name) => inner.GetOrdinal(name);

    public override string GetString(int ordinal) => inner.GetString(ordinal);

    public override object GetValue(int ordinal) => inner.GetValue(ordinal);

    public override int GetValues(object[] values) => inner.GetValues(values);

    public override bool IsDBNull(int ordinal) => inner.IsDBNull(ordinal);

    public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken)
        => inner.IsDBNullAsync(ordinal, cancellationToken);

    public override T GetFieldValue<T>(int ordinal) => inner.GetFieldValue<T>(ordinal);

    public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken)
        => inner.GetFieldValueAsync<T>(ordinal, cancellationToken);

    public override Stream GetStream(int ordinal) => inner.GetStream(ordinal);

    public override TextReader GetTextReader(int ordinal) => inner.GetTextReader(ordinal);

    public override Type GetProviderSpecificFieldType(int ordinal) => inner.GetProviderSpecificFieldType(ordinal);

    public override object GetProviderSpecificValue(int ordinal) => inner.GetProviderSpecificValue(ordinal);

    public override int GetProviderSpecificValues(object[] values) => inner.GetProviderSpecificValues(values);

    public override DataTable? GetSchemaTable() => inner.GetSchemaTable();

    public override Task<DataTable?> GetSchemaTableAsync(CancellationToken cancellationToken = default)
        => inner.GetSchemaTableAsync(cancellationToken);

    public override IEnumerator GetEnumerator() => ((IEnumerable)inner).GetEnumerator();

    public override void Close() => inner.Close();

    public override Task CloseAsync() => inner.CloseAsync();

    public override ValueTask DisposeAsync() => inner.DisposeAsync();

    protected override DbDataReader GetDbDataReader(int ordinal) => inner.GetData(ordinal);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
