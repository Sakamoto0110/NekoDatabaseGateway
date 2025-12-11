using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NekoDbGateway
{
    /// <summary>
    /// Gateway genérico para acesso a dados baseado em SQL bruto e em <see cref="QueryBuilder"/>.
    /// 
    /// Exposição pública em quatro camadas:
    /// <list type="bullet">
    ///   <item><b>Raw</b>: RecordItem (GetRaw/ReadRaw).</item>
    ///   <item><b>DTO</b>: GetDto/ReadDto (tipado forte via reflexão).</item>
    ///   <item><b>Dynamic</b>: DynamicRow + IL.</item>
    ///   <item><b>Universal</b>: Get/Read com fallback DTO → Dynamic.</item>
    /// </list>
    /// </summary>
    public partial class DatabaseGateway
    {
        private readonly IDbConnectionFactory _connectionFactory;

        #region ctor

        /// <summary>
        /// Cria um <see cref="DatabaseGateway"/> utilizando a fábrica de conexões informada.
        /// </summary>
        public DatabaseGateway(IDbConnectionFactory Factory)
        {
            if(Factory == null) throw new ArgumentNullException(nameof(Factory));
            _connectionFactory = Factory;
        }

        #endregion

        #region Connection / command helpers

        private async Task<DbConnection> OpenConnectionAsync(CancellationToken Ct)
        {
            DbConnection conn = await _connectionFactory.Create().ConfigureAwait(false);

            try
            {
                await conn.OpenAsync(Ct).ConfigureAwait(false);
            }
            catch(NotSupportedException)
            {
                conn.Open();
            }

            return conn;
        }

        private async Task<T> WithCommandAsync<T>(
            string Sql,
            Dictionary<string, object> Parameters,
            Func<DbCommand, Task<T>> Work,
            CancellationToken Ct)
        {
            if(Sql == null) throw new ArgumentNullException(nameof(Sql));
            if(Work == null) throw new ArgumentNullException(nameof(Work));

            using(DbConnection conn = await OpenConnectionAsync(Ct).ConfigureAwait(false))
            {
                using(DbCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = Sql;
                    cmd.CommandType = CommandType.Text;

                    ApplyParameters(cmd, Parameters);

                    T result = await Work(cmd).ConfigureAwait(false);
                    return result;
                }
            }
        }

        private Task<T> WithCommandAsync<T>(
            string Sql,
            Func<DbCommand, Task<T>> Work,
            CancellationToken Ct)
        {
            return WithCommandAsync(Sql, null, Work, Ct);
        }

        private static async Task<DbDataReader> ExecuteReaderSafeAsync(DbCommand Cmd, CancellationToken Ct)
        {
            try
            {
                DbDataReader reader = await Cmd.ExecuteReaderAsync(Ct).ConfigureAwait(false);
                return reader;
            }
            catch(NotSupportedException)
            {
                return Cmd.ExecuteReader();
            }
        }

        private static async Task<int> ExecuteNonQuerySafeAsync(DbCommand Cmd, CancellationToken Ct)
        {
            try
            {
                int count = await Cmd.ExecuteNonQueryAsync(Ct).ConfigureAwait(false);
                return count;
            }
            catch(NotSupportedException)
            {
                return Cmd.ExecuteNonQuery();
            }
        }

        private static async Task<bool> ReadSafeAsync(DbDataReader Reader, CancellationToken Ct)
        {
            try
            {
                bool has = await Reader.ReadAsync(Ct).ConfigureAwait(false);
                return has;
            }
            catch(NotSupportedException)
            {
                return Reader.Read();
            }
        }

        private void ApplyParameters(DbCommand Cmd, Dictionary<string, object> Parameters)
        {
            if(Parameters == null || Parameters.Count == 0)
                return;

            bool isAccess = Cmd is OleDbCommand;

            IEnumerable<KeyValuePair<string, object>> ordered;

            if(isAccess)
                ordered = new List<KeyValuePair<string, object>>(Parameters).OrderBy(p => p.Key, StringComparer.Ordinal);
            else
                ordered = Parameters;

            foreach(KeyValuePair<string, object> kv in ordered)
            {
                DbParameter p = Cmd.CreateParameter();
                if(!isAccess)
                    p.ParameterName = kv.Key;

                p.Value = kv.Value ?? DBNull.Value;
                Cmd.Parameters.Add(p);
            }
        }

        #endregion

        #region Schema helpers

        /// <summary>
        /// Representa informações de esquema (colunas e tipos) para mapeamento dinâmico.
        /// </summary>
        private sealed class SchemaInfo
        {
            public List<string> Columns { get; private set; }
            public Dictionary<string, Type> ColumnTypes { get; private set; }

            public SchemaInfo()
            {
                Columns = new List<string>();
                ColumnTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static SchemaInfo ExtractSchema(DbDataReader Reader)
        {
            SchemaInfo result = new SchemaInfo();
            DataTable dt = Reader.GetSchemaTable();
            if(dt == null) return result;

            foreach(DataRow row in dt.Rows)
            {
                string name = row["ColumnName"] != null
                    ? row["ColumnName"].ToString()
                    : "Col" + result.Columns.Count;

                Type type = row["DataType"] as Type ?? typeof(string);

                result.Columns.Add(name);
                if(!result.ColumnTypes.ContainsKey(name))
                    result.ColumnTypes[name] = type;
            }

            return result;
        }

        #endregion
    }
}
