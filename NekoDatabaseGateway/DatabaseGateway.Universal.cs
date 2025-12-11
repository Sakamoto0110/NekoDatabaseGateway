using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace NekoDbGateway
{
    public partial class DatabaseGateway
    {
        #region Universal GET (DTO → fallback Dynamic)

        public async Task<List<T>> Get<TTranslator, T>(
            QueryBuilder Builder,
            CancellationToken Ct = default(CancellationToken))
            where TTranslator : IDbQueryTranslator, new()
            where T : new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));

            Type targetType = typeof(T);

            if(targetType == typeof(DynamicRow) || targetType == typeof(object))
            {
                List<DynamicRow> dynRows = await GetDynamic<TTranslator>(Builder, Ct).ConfigureAwait(false);
                List<T> castResult = dynRows.Cast<T>().ToList();
                return castResult;
            }

            if(targetType.GetConstructor(Type.EmptyTypes) != null)
            {
                try
                {
                    List<T> dtoResult = await GetDto<TTranslator, T>(Builder, Ct).ConfigureAwait(false);
                    return dtoResult;
                }
                catch
                {
                }
            }

            List<DynamicRow> dynFallback = await GetDynamic<TTranslator>(Builder, Ct).ConfigureAwait(false);
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
        public Task Read<TTranslator>(
            QueryBuilder Builder,
            Delegate Handler,
            CancellationToken Ct = default(CancellationToken))
            where TTranslator : IDbQueryTranslator, new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));
            if(Handler == null) throw new ArgumentNullException(nameof(Handler));

            return ReadUniversalDispatch<TTranslator>(Builder, Handler, Ct);
        }

        /// <summary>
        /// Versão tipada de leitura universal, com fallback automático para IL + DynamicRow.
        /// </summary>
        public Task Read<TTranslator, T>(
            QueryBuilder Builder,
            Action<T> Callback,
            CancellationToken Ct = default(CancellationToken))
            where TTranslator : IDbQueryTranslator, new()
            where T : new()
        {
            if(Callback == null) throw new ArgumentNullException(nameof(Callback));
            return Read<TTranslator>(Builder, Callback, Ct);
        }

        private Task ReadUniversalDispatch<TTranslator>(
            QueryBuilder Builder,
            Delegate Handler,
            CancellationToken Ct)
            where TTranslator : IDbQueryTranslator, new()
        {
            ParameterInfo[] parameters = Handler.Method.GetParameters();
            if(parameters == null || parameters.Length != 1)
                throw new InvalidOperationException(
                    "O callback deve possuir exatamente um parâmetro.");

            Type paramType = parameters[0].ParameterType;

            if(paramType == typeof(DynamicRow))
            {
                return ReadDynamic<TTranslator>(Builder,
                    row => Handler.DynamicInvoke(row),
                    Ct);
            }

            if(paramType == typeof(object))
            {
                return ReadDynamic<TTranslator>(Builder,
                    row => Handler.DynamicInvoke(row),
                    Ct);
            }

            ConstructorInfo ctor = paramType.GetConstructor(Type.EmptyTypes);
            if(ctor != null)
            {
                try
                {
                    MethodInfo mi = typeof(DatabaseGateway)
                        .GetMethod("ReadDto", BindingFlags.Instance | BindingFlags.Public)
                        .MakeGenericMethod(typeof(TTranslator), paramType);

                    return (Task)mi.Invoke(this, new object[] { Builder, Handler, Ct });
                }
                catch
                {
                }
            }

            return ReadDynamic<TTranslator>(Builder,
                row => Handler.DynamicInvoke(row),
                Ct);
        }

        #endregion
    }
}
