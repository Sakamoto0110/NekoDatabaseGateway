using NekoDbGateway.Query;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace NekoDbGateway
{
    public partial class DatabaseGateway
    {
        #region Universal GET (DTO → fallback Dynamic)

        public async Task<List<T>> Get<TTranslator, T>(QueryExecutionContext ctx, QueryBuilder Builder, CancellationToken Ct = default(CancellationToken)) where TTranslator : IDbQueryTranslator, new() where T : new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));
            if(ctx == null) throw new ArgumentNullException(nameof(ctx));

            Type targetType = typeof(T);

            if(targetType == typeof(DynamicRow) || targetType == typeof(object))
            {
                List<DynamicRow> dynRows = await GetDynamic<TTranslator>(ctx, Builder, Ct).ConfigureAwait(false);
                List<T> castResult = dynRows.Cast<T>().ToList();
                return castResult;
            }

            if(targetType.GetConstructor(Type.EmptyTypes) != null)
            {
                try
                {
                    List<T> dtoResult = await GetDto<T>(ctx, Builder, Ct).ConfigureAwait(false);
                    return dtoResult;
                }
                catch
                {
                }
            }

            List<DynamicRow> dynFallback = await GetDynamic<TTranslator>(ctx, Builder, Ct).ConfigureAwait(false);
            List<T> fallbackCast = dynFallback.Cast<T>().ToList();
            return fallbackCast;
        }

        #endregion

        #region Universal READ (no <T> required + typed overload)

        /// <summary>
        /// Leitura universal usando apenas o tradutor e o delegate do callback.
        /// O tipo do parâmetro do callback determina a estratégia:
        /// DynamicRow → IL, object → IL, DTO com ctor padrão → DTO, senão fallback IL.
        /// </summary>
        public Task Read(QueryExecutionContext ctx,QueryBuilder builder,Delegate handler,CancellationToken ct = default)
        {
            if(builder == null) throw new ArgumentNullException(nameof(builder));
            if(ctx == null) throw new ArgumentNullException(nameof(ctx));
            if(handler == null) throw new ArgumentNullException(nameof(handler));

            return ReadUniversalDispatch(ctx, builder, handler, ct);
        }


        /// <summary>
        /// Versão tipada de leitura universal, com fallback automático para IL + DynamicRow.
        /// </summary>
        public Task Read<T>(QueryExecutionContext ctx,QueryBuilder builder,Action<T> callback,CancellationToken ct = default)
        {
            if(callback == null) throw new ArgumentNullException(nameof(callback));
            if(ctx == null) throw new ArgumentNullException(nameof(ctx));

            return Read(ctx, builder, (Delegate)callback, ct);
        }

        private async Task ReadUniversalDispatch(QueryExecutionContext ctx,QueryBuilder builder,Delegate handler,CancellationToken ct)
        {
            ParameterInfo[] pars = handler.Method.GetParameters();
            if(pars.Length != 1)
                throw new InvalidOperationException(
                    "The handler must have exactly one parameter.");

            Type targetType = pars[0].ParameterType;
            QueryModel model = builder.Build();
            DbQuery dbq = ctx.Translator.Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);

            try
            {
                await WithCommandAsync(ctx, dbq.Sql, dbq.Parameters, async cmd => {
                    using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, ct))
                    {
                        SchemaInfo schema = ExtractSchema(reader);

                        bool wantsDynamic = targetType == typeof(DynamicRow) || targetType == typeof(object);
                        bool wantsDto = !wantsDynamic && targetType.GetConstructor(Type.EmptyTypes) != null;

                        Type ilType = wantsDynamic ? RuntimeTypeFactory.GetOrCreate(schema) : null;

                        while(await ReadSafeAsync(reader, ct))
                        {
                            ct.ThrowIfCancellationRequested();

                            if(wantsDto)
                            {
                                // DTO path (SAFE)
                                // var record = ReadRecordRow(reader, schema);
                                var record = new Dictionary<string, RecordItem>(StringComparer.OrdinalIgnoreCase);                               
                                foreach(var col in schema.Columns)
                                    record[col] = new RecordItem
                                    {
                                        Name = col,
                                        Type = schema.ColumnTypes[col].FullName,
                                        Value = Convert.ToString(reader[col], CultureInfo.InvariantCulture)
                                    };

                                object dto = DataMapper.Map(record, targetType);
                                handler.DynamicInvoke(dto);
                                continue;
                            }
                            // ✅ Dynamic path ONLY calls handler with DynamicRow
                            if(ilType == null)
                                ilType = RuntimeTypeFactory.GetOrCreate(schema);

                            // Dynamic path (SAFE)
                            object inst = Activator.CreateInstance(ilType);
                            FillDynamicObject(inst, ilType, schema, reader);
                            try
                            {
                                handler.DynamicInvoke((object)new DynamicRow(inst));
                            }
                            catch { }
                        }
                    }

                    return 0;
                }, ct);


            }
            catch(Exception ex)
            {
                ctx.RaiseError(dbq.Sql, ex);
                throw;
            }
        }


        private static Dictionary<string, RecordItem> ReadRecordRow(DbDataReader reader,SchemaInfo schema)
        {
            var row = new Dictionary<string, RecordItem>(
                StringComparer.OrdinalIgnoreCase);

            for(int i = 0; i < schema.Columns.Count; i++)
            {
                string col = schema.Columns[i];
                object raw = reader[col];

                row[col] = new RecordItem
                {
                    Name = col,
                    Type = schema.ColumnTypes[col].FullName,
                    Value = Convert.ToString(raw, CultureInfo.InvariantCulture)
                };
            }

            return row;
        }

        #endregion
    }
}
