using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace NekoDbGateway
{
    public partial class DatabaseGateway
    {
        #region Raw API (RecordItem)

        public async Task<bool> ContainsData(string Sql, CancellationToken Ct = default(CancellationToken))
        {
            bool has = await WithCommandAsync(Sql, async delegate (DbCommand cmd)
            {
                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, Ct).ConfigureAwait(false))
                {
                    bool any = await ReadSafeAsync(reader, Ct).ConfigureAwait(false);
                    return any;
                }
            }, Ct).ConfigureAwait(false);

            return has;
        }

        public Task<List<Dictionary<string, RecordItem>>> GetRaw(
            string Sql,
            CancellationToken Ct = default(CancellationToken))
        {
            return GetRaw(Sql, null, Ct);
        }

        public async Task<List<Dictionary<string, RecordItem>>> GetRaw(
            string Sql,
            Dictionary<string, object> Parameters,
            CancellationToken Ct = default(CancellationToken))
        {
            List<Dictionary<string, RecordItem>> result = await WithCommandAsync(Sql, Parameters, async delegate (DbCommand cmd)
            {
                List<Dictionary<string, RecordItem>> list = new List<Dictionary<string, RecordItem>>();

                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, Ct).ConfigureAwait(false))
                {
                    SchemaInfo schema = ExtractSchema(reader);

                    while(await ReadSafeAsync(reader, Ct).ConfigureAwait(false))
                    {
                        Dictionary<string, RecordItem> row = new Dictionary<string, RecordItem>(StringComparer.OrdinalIgnoreCase);
                        foreach(string col in schema.Columns)
                        {
                            object raw = reader[col];
                            string strValue = Convert.ToString(raw, CultureInfo.InvariantCulture);

                            RecordItem item = new RecordItem
                            {
                                Name = col,
                                Type = schema.ColumnTypes[col].FullName,
                                Value = strValue
                            };
                            row[col] = item;
                        }
                        list.Add(row);
                    }
                }

                return list;
            }, Ct).ConfigureAwait(false);

            return result;
        }

        public Task ReadRaw(
            string Sql,
            Action<Dictionary<string, RecordItem>> Callback,
            CancellationToken Ct = default(CancellationToken))
        {
            return ReadRaw(Sql, null, Callback, Ct);
        }

        public async Task ReadRaw(
            string Sql,
            Dictionary<string, object> Parameters,
            Action<Dictionary<string, RecordItem>> Callback,
            CancellationToken Ct = default(CancellationToken))
        {
            if(Callback == null) throw new ArgumentNullException(nameof(Callback));

            await WithCommandAsync(Sql, Parameters, async delegate (DbCommand cmd)
            {
                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, Ct).ConfigureAwait(false))
                {
                    SchemaInfo schema = ExtractSchema(reader);

                    while(await ReadSafeAsync(reader, Ct).ConfigureAwait(false))
                    {
                        Dictionary<string, RecordItem> row = new Dictionary<string, RecordItem>(StringComparer.OrdinalIgnoreCase);

                        foreach(string col in schema.Columns)
                        {
                            object raw = reader[col];
                            string strValue = Convert.ToString(raw, CultureInfo.InvariantCulture);

                            RecordItem item = new RecordItem
                            {
                                Name = col,
                                Type = schema.ColumnTypes[col].FullName,
                                Value = strValue
                            };
                            row[col] = item;
                        }

                        Callback(row);
                    }
                }

                return 0;
            }, Ct).ConfigureAwait(false);
        }

        protected async Task<int> Upsert(
            string Sql,
            Dictionary<string, object> Parameters,
            CancellationToken Ct = default(CancellationToken))
        {
            if(string.IsNullOrWhiteSpace(Sql))
                throw new ArgumentNullException(nameof(Sql));

            int affected = await WithCommandAsync(Sql, Parameters, delegate (DbCommand cmd)
            {
                return ExecuteNonQuerySafeAsync(cmd, Ct);
            }, Ct).ConfigureAwait(false);

            return affected;
        }

        public Task<int> Insert(
            string Sql,
            CancellationToken Ct = default(CancellationToken))
        {
            return Upsert(Sql, null, Ct);
        }

        public Task<int> Insert(
            string Sql,
            Dictionary<string, object> Parameters,
            CancellationToken Ct = default(CancellationToken))
        {
            return Upsert(Sql, Parameters, Ct);
        }

        public Task<int> Update(
            string Sql,
            CancellationToken Ct = default(CancellationToken))
        {
            return Upsert(Sql, null, Ct);
        }

        public Task<int> Update(
            string Sql,
            Dictionary<string, object> Parameters,
            CancellationToken Ct = default(CancellationToken))
        {
            return Upsert(Sql, Parameters, Ct);
        }

        public async Task<List<Dictionary<string, RecordItem>>> GetRaw<TTranslator>(
            QueryBuilder Builder,
            CancellationToken Ct = default(CancellationToken))
            where TTranslator : IDbQueryTranslator, new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));

            TTranslator translator = new TTranslator();
            QueryModel model = Builder.Build();
            DbQuery dbq = translator.Translate(model);

            List<Dictionary<string, RecordItem>> result =
                await GetRaw(dbq.Sql, dbq.Parameters, Ct).ConfigureAwait(false);
            return result;
        }

        public async Task ReadRaw<TTranslator>(
            QueryBuilder Builder,
            Action<Dictionary<string, RecordItem>> Callback,
            CancellationToken Ct = default(CancellationToken))
            where TTranslator : IDbQueryTranslator, new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));
            if(Callback == null) throw new ArgumentNullException(nameof(Callback));

            TTranslator translator = new TTranslator();
            QueryModel model = Builder.Build();
            DbQuery dbq = translator.Translate(model);

            await ReadRaw(dbq.Sql, dbq.Parameters, Callback, Ct)
                .ConfigureAwait(false);
        }

        public async Task<int> Insert<TTranslator>(
            QueryBuilder Builder,
            CancellationToken Ct = default(CancellationToken))
            where TTranslator : IDbQueryTranslator, new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));

            TTranslator translator = new TTranslator();
            QueryModel model = Builder.Build();
            DbQuery dbq = translator.Translate(model);

            int affected = await Insert(dbq.Sql, dbq.Parameters, Ct)
                .ConfigureAwait(false);
            return affected;
        }

        public async Task<int> Update<TTranslator>(
            QueryBuilder Builder,
            CancellationToken Ct = default(CancellationToken))
            where TTranslator : IDbQueryTranslator, new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));

            TTranslator translator = new TTranslator();
            QueryModel model = Builder.Build();
            DbQuery dbq = translator.Translate(model);

            int affected = await Update(dbq.Sql, dbq.Parameters, Ct)
                .ConfigureAwait(false);
            return affected;
        }

        #endregion

        #region DTO API (strong typed, no IL fallback)

        public async Task<List<T>> GetDto<TTranslator, T>(
            QueryBuilder Builder,
            CancellationToken Ct = default(CancellationToken))
            where TTranslator : IDbQueryTranslator, new()
            where T : new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));

            TTranslator translator = new TTranslator();
            QueryModel model = Builder.Build();
            DbQuery dbq = translator.Translate(model);

            List<T> list = new List<T>();

            await WithCommandAsync(dbq.Sql, dbq.Parameters, async delegate (DbCommand cmd)
            {
                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, Ct).ConfigureAwait(false))
                {
                    Type type = typeof(T);
                    PropertyInfo[] props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                    while(await ReadSafeAsync(reader, Ct).ConfigureAwait(false))
                    {
                        T inst = new T();
                        for(int i = 0; i < props.Length; i++)
                        {
                            PropertyInfo p = props[i];
                            if(!reader.HasColumn(p.Name))
                                continue;

                            object val = reader[p.Name];
                            if(val is DBNull) continue;

                            try
                            {
                                object converted = Convert.ChangeType(val, p.PropertyType, CultureInfo.InvariantCulture);
                                p.SetValue(inst, converted, null);
                            }
                            catch
                            {
                            }
                        }
                        list.Add(inst);
                    }
                }

                return 0;
            }, Ct).ConfigureAwait(false);

            return list;
        }

        public async Task ReadDto<TTranslator, T>(
            QueryBuilder Builder,
            Action<T> Callback,
            CancellationToken Ct = default(CancellationToken))
            where TTranslator : IDbQueryTranslator, new()
            where T : new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));
            if(Callback == null) throw new ArgumentNullException(nameof(Callback));

            TTranslator translator = new TTranslator();
            QueryModel model = Builder.Build();
            DbQuery dbq = translator.Translate(model);

            await WithCommandAsync(dbq.Sql, dbq.Parameters, async delegate (DbCommand cmd)
            {
                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, Ct).ConfigureAwait(false))
                {
                    Type type = typeof(T);
                    PropertyInfo[] props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                    while(await ReadSafeAsync(reader, Ct).ConfigureAwait(false))
                    {
                        T inst = new T();
                        for(int i = 0; i < props.Length; i++)
                        {
                            PropertyInfo p = props[i];
                            if(!reader.HasColumn(p.Name))
                                continue;

                            object val = reader[p.Name];
                            if(val is DBNull) continue;

                            try
                            {
                                object converted = Convert.ChangeType(val, p.PropertyType, CultureInfo.InvariantCulture);
                                p.SetValue(inst, converted, null);
                            }
                            catch
                            {
                            }
                        }

                        Callback(inst);
                    }
                }

                return 0;
            }, Ct).ConfigureAwait(false);
        }

        #endregion
    }
}
