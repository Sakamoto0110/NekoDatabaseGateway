using NekoDbGateway.Query;
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

        public async Task<bool> ContainsData(QueryExecutionContext ctx,string Sql,CancellationToken Ct = default(CancellationToken))
        {
            bool has = await WithCommandAsync(ctx,Sql, async delegate (DbCommand cmd)
            {
                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, Ct).ConfigureAwait(false))
                {
                    bool any = await ReadSafeAsync(reader, Ct).ConfigureAwait(false);
                    return any;
                }
            }, Ct).ConfigureAwait(false);

            return has;
        }

        public Task<List<Dictionary<string, RecordItem>>> GetRaw(QueryExecutionContext ctx,string Sql,CancellationToken Ct = default(CancellationToken))
        {
            return GetRaw(ctx,Sql,null, Ct);
        }

        public async Task<List<Dictionary<string, RecordItem>>> GetRaw(QueryExecutionContext ctx,string Sql, Dictionary<string, object> Parameters,CancellationToken Ct = default(CancellationToken))
        {
            List<Dictionary<string, RecordItem>> result = await WithCommandAsync(ctx,Sql,Parameters, async delegate (DbCommand cmd)
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

        public Task ReadRaw(QueryExecutionContext ctx,string Sql,Action<Dictionary<string, RecordItem>> Callback,CancellationToken Ct = default(CancellationToken))
        {
            return ReadRaw(ctx,Sql, null, Callback, Ct);
        }

        public async Task ReadRaw(QueryExecutionContext ctx,string Sql,Dictionary<string, object> Parameters,Action<Dictionary<string, RecordItem>> Callback,CancellationToken Ct = default(CancellationToken))
        {
            if(Callback == null) throw new ArgumentNullException(nameof(Callback));
            if(ctx == null) throw new ArgumentNullException(nameof(ctx));

            await WithCommandAsync(ctx,Sql, Parameters, async delegate (DbCommand cmd)
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
                        try
                        {
                            Callback(row);
                        }
                        catch(Exception ex) { ctx.RaiseError(Sql, ex); }
                    }
                }

                return 0;
            }, Ct).ConfigureAwait(false);
        }

        protected async Task<int> Upsert(QueryExecutionContext ctx,string Sql,Dictionary<string, object> Parameters,CancellationToken Ct = default(CancellationToken))
        {
            if(string.IsNullOrWhiteSpace(Sql))
                throw new ArgumentNullException(nameof(Sql));
            if(ctx == null) throw new ArgumentNullException(nameof(ctx));

            int affected = await WithCommandAsync(ctx,Sql,Parameters, delegate (DbCommand cmd)
            {
                try
                {
                    return ExecuteNonQuerySafeAsync(cmd, Ct);
                }
                catch(Exception ex) { ctx.RaiseError(Sql, ex); return Task.FromResult(-1); }
                
                
            }, Ct).ConfigureAwait(false);

            return affected;
        }

        public Task<int> Insert(QueryExecutionContext ctx,string Sql,CancellationToken Ct = default(CancellationToken))
        {
            return Upsert(ctx,Sql,null, Ct);
        }

        public Task<int> Insert(QueryExecutionContext ctx,string Sql,Dictionary<string, object> Parameters,CancellationToken Ct = default(CancellationToken))
        {
            return Upsert(ctx,Sql,Parameters, Ct);
        }

        public Task<int> Update(QueryExecutionContext ctx,string Sql,CancellationToken Ct = default(CancellationToken))
        {
            return Upsert(ctx,Sql, null, Ct);
        }

        public Task<int> Update(QueryExecutionContext ctx,string Sql,Dictionary<string, object> Parameters,CancellationToken Ct = default(CancellationToken))
        {
            return Upsert(ctx,Sql,Parameters, Ct);
        }

        public async Task<List<Dictionary<string, RecordItem>>> GetRaw<TTranslator>(QueryExecutionContext ctx,QueryBuilder Builder,CancellationToken Ct = default(CancellationToken))where TTranslator : IDbQueryTranslator, new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));
            if(ctx == null) throw new ArgumentNullException(nameof(ctx));

            TTranslator translator = new TTranslator();
            QueryModel model = Builder.Build();
            DbQuery dbq = translator.Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);
            List<Dictionary<string, RecordItem>> result = null;
            try 
            {
                ctx.RaiseSqlDispatch(dbq.Sql);
                result = await GetRaw(ctx, dbq.Sql, dbq.Parameters, Ct).ConfigureAwait(false);
                ctx.RaiseSuccess(dbq.Sql);
            }
            catch(Exception ex) { ctx.RaiseError(dbq.Sql, ex);  }
            
            return result;
        }

        public async Task ReadRaw<TTranslator>(QueryExecutionContext ctx,QueryBuilder Builder ,Action<Dictionary<string, RecordItem>> Callback,CancellationToken Ct = default(CancellationToken))where TTranslator : IDbQueryTranslator, new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));
            if(Callback == null) throw new ArgumentNullException(nameof(Callback));
            if(ctx == null) throw new ArgumentNullException(nameof(ctx));

            TTranslator translator = new TTranslator();
            QueryModel model = Builder.Build();
            DbQuery dbq = translator.Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);

            
            try
            {
                ctx.RaiseSqlDispatch(dbq.Sql);
                await ReadRaw(ctx, dbq.Sql, dbq.Parameters, Callback, Ct).ConfigureAwait(false);
                ctx.RaiseSuccess(dbq.Sql);
            }
            catch(Exception ex) { ctx.RaiseError(dbq.Sql,ex); }

        }

        public async Task<int> Insert<TTranslator>(QueryExecutionContext ctx,QueryBuilder Builder , CancellationToken Ct = default(CancellationToken))where TTranslator : IDbQueryTranslator, new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));

            TTranslator translator = new TTranslator();
            QueryModel model = Builder.Build();
            DbQuery dbq = translator.Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);

            int affected = 0;
            try
            {
                ctx.RaiseSqlDispatch(dbq.Sql);
                 affected = await Insert(ctx, dbq.Sql, dbq.Parameters, Ct).ConfigureAwait(false);
                ctx.RaiseSuccess(dbq.Sql);
            }
            catch(Exception ex) { ctx.RaiseError(dbq.Sql, ex); affected = -1; }
            
            return affected;
        }

        public async Task<int> Update<TTranslator>(QueryExecutionContext ctx,QueryBuilder Builder , CancellationToken Ct = default(CancellationToken))where TTranslator : IDbQueryTranslator, new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));
            if(ctx == null) throw new ArgumentNullException(nameof(ctx));

            TTranslator translator = new TTranslator();
            QueryModel model = Builder.Build();
            DbQuery dbq = translator.Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);

            int affected = 0;
            try
            {
                ctx.RaiseSqlDispatch(dbq.Sql);
                affected = await Update(ctx, dbq.Sql, dbq.Parameters, Ct).ConfigureAwait(false);
                ctx.RaiseSuccess(dbq.Sql);
            }
            catch(Exception ex) { ctx.RaiseError(dbq.Sql, ex); affected = -1; }
            return affected;
        }

        #endregion

        #region DTO API (strong typed, no IL fallback)

        public async Task<List<T>> GetDto<T>(QueryExecutionContext ctx,QueryBuilder Builder , CancellationToken Ct = default(CancellationToken)) where T : new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));
            if(ctx == null) throw new ArgumentNullException(nameof(ctx));
            
            QueryModel model = Builder.Build();
            DbQuery dbq = ctx.Translator.Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);

            List<T> list = new List<T>();

            await WithCommandAsync(ctx,dbq.Sql,dbq.Parameters, async delegate (DbCommand cmd)
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

        public async Task ReadDto<T>(QueryExecutionContext ctx,QueryBuilder Builder , Action<T> Callback,CancellationToken Ct = default(CancellationToken))where T : new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));
            if(Callback == null) throw new ArgumentNullException(nameof(Callback));
            if(ctx == null) throw new ArgumentNullException(nameof(ctx));

           
            QueryModel model = Builder.Build();
            DbQuery dbq = ctx.Translator.Translate(model);

            await WithCommandAsync(ctx,dbq.Sql, dbq.Parameters, async delegate (DbCommand cmd)
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
