using NekoDbGateway.Query;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;

namespace NekoDbGateway
{
    public partial class DatabaseGateway
    {
        #region DynamicRow (IL-backed)

        /// <summary>
        /// Representa uma linha dinâmica retornada por IL,
        /// permitindo acesso tanto por indexador string quanto por membros dinâmicos.
        /// </summary>
        public sealed class DynamicRow : System.Dynamic.DynamicObject
        {
            private readonly object _instance;
            private readonly Dictionary<string, PropertyInfo> _props;

            public DynamicRow(object Instance)
            {
                if(Instance == null) throw new ArgumentNullException(nameof(Instance));
                _instance = Instance;

                PropertyInfo[] properties = Instance
                    .GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance);

                _props = new Dictionary<string, PropertyInfo>(properties.Length, StringComparer.OrdinalIgnoreCase);
                for(int i = 0; i < properties.Length; i++)
                {
                    PropertyInfo p = properties[i];
                    string key = p.Name.ToLowerInvariant();
                    _props[key] = p;
                }
            }

            public object this[string Name]
            {
                get { return Get(Name); }
                set { Set(Name, value); }
            }

            private object Get(string Name)
            {
                if(string.IsNullOrEmpty(Name)) return null;
                string key = Name.ToLowerInvariant();
                PropertyInfo pi;
                if(_props.TryGetValue(key, out pi))
                    return pi.GetValue(_instance, null);
                return null;
            }

            private void Set(string Name, object Value)
            {
                if(string.IsNullOrEmpty(Name)) return;
                string key = Name.ToLowerInvariant();
                PropertyInfo pi;
                if(_props.TryGetValue(key, out pi) && pi.CanWrite)
                    pi.SetValue(_instance, Value, null);
            }

            public override bool TryGetMember(System.Dynamic.GetMemberBinder Binder, out object Result)
            {
                Result = Get(Binder.Name);
                return true;
            }

            public override bool TrySetMember(System.Dynamic.SetMemberBinder Binder, object Value)
            {
                Set(Binder.Name, Value);
                return true;
            }

            public override bool TryGetIndex(System.Dynamic.GetIndexBinder Binder, object[] Indexes, out object Result)
            {
                if(Indexes != null && Indexes.Length == 1 && Indexes[0] is string)
                {
                    string name = (string)Indexes[0];
                    Result = Get(name);
                    return true;
                }

                Result = null;
                return false;
            }

            public override bool TrySetIndex(System.Dynamic.SetIndexBinder Binder, object[] Indexes, object Value)
            {
                if(Indexes != null && Indexes.Length == 1 && Indexes[0] is string)
                {
                    string name = (string)Indexes[0];
                    Set(name, Value);
                    return true;
                }

                return false;
            }
        }

        #endregion

        #region RuntimeTypeFactory + FillDynamicObject

        private static class RuntimeTypeFactory
        {
            private static readonly AssemblyName _assemblyName = new AssemblyName("DynamicRowTypesAsm");
            private static readonly ModuleBuilder _module;
            private static readonly Dictionary<string, Type> _cache = new Dictionary<string, Type>(StringComparer.Ordinal);
            private static readonly LinkedList<string> _lru = new LinkedList<string>();
            private const int MaxTypes = 256;
            private static int _typeCounter;
            private static readonly object _sync = new object();

            static RuntimeTypeFactory()
            {
                AssemblyBuilder ab = AppDomain.CurrentDomain.DefineDynamicAssembly(
                    _assemblyName, AssemblyBuilderAccess.Run);
                _module = ab.DefineDynamicModule("MainModule");
            }

            public static Type GetOrCreate(SchemaInfo Schema)
            {
                if(Schema == null) throw new ArgumentNullException(nameof(Schema));

                string signature = BuildSignature(Schema);

                lock(_sync)
                {
                    Type t;
                    if(_cache.TryGetValue(signature, out t))
                    {
                        MoveToTail(signature);
                        return t;
                    }

                    Type created = CreateType(Schema);
                    _cache[signature] = created;
                    _lru.AddLast(signature);
                    EnforceCapacity();
                    return created;
                }
            }

            private static string BuildSignature(SchemaInfo Schema)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();

                List<string> ordered = new List<string>(Schema.Columns);
                ordered.Sort(StringComparer.OrdinalIgnoreCase);

                for(int i = 0; i < ordered.Count; i++)
                {
                    string col = ordered[i];
                    Type t;
                    if(!Schema.ColumnTypes.TryGetValue(col, out t) || t == null)
                        t = typeof(string);

                    sb.Append(col.ToLowerInvariant());
                    sb.Append(':');
                    sb.Append(t.FullName);
                    sb.Append('|');
                }

                return sb.ToString();
            }

            private static Type CreateType(SchemaInfo Schema)
            {
                string typeName = "DynamicRowType_" + System.Threading.Interlocked.Increment(ref _typeCounter);
                TypeBuilder tb = _module.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);

                for(int i = 0; i < Schema.Columns.Count; i++)
                {
                    string propName = Schema.Columns[i];
                    Type propType;
                    if(!Schema.ColumnTypes.TryGetValue(propName, out propType) || propType == null)
                        propType = typeof(string);

                    FieldBuilder field = tb.DefineField("_" + propName, propType, FieldAttributes.Private);
                    PropertyBuilder prop = tb.DefineProperty(
                        propName,
                        System.Reflection.PropertyAttributes.HasDefault,
                        propType,
                        null);

                    MethodBuilder getter = tb.DefineMethod(
                        "get_" + propName,
                        MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                        propType,
                        Type.EmptyTypes);

                    ILGenerator ilGet = getter.GetILGenerator();
                    ilGet.Emit(OpCodes.Ldarg_0);
                    ilGet.Emit(OpCodes.Ldfld, field);
                    ilGet.Emit(OpCodes.Ret);

                    MethodBuilder setter = tb.DefineMethod(
                        "set_" + propName,
                        MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                        null,
                        new Type[] { propType });

                    ILGenerator ilSet = setter.GetILGenerator();
                    ilSet.Emit(OpCodes.Ldarg_0);
                    ilSet.Emit(OpCodes.Ldarg_1);
                    ilSet.Emit(OpCodes.Stfld, field);
                    ilSet.Emit(OpCodes.Ret);

                    prop.SetGetMethod(getter);
                    prop.SetSetMethod(setter);
                }

                Type createdType = tb.CreateType();
                return createdType;
            }

            private static void MoveToTail(string Key)
            {
                LinkedListNode<string> node = _lru.Find(Key);
                if(node != null)
                {
                    _lru.Remove(node);
                    _lru.AddLast(node);
                }
            }

            private static void EnforceCapacity()
            {
                while(_lru.Count > MaxTypes)
                {
                    LinkedListNode<string> first = _lru.First;
                    if(first == null) break;

                    string key = first.Value;
                    _lru.RemoveFirst();
                    _cache.Remove(key);
                }
            }
        }

        private static void FillDynamicObject(object Instance, Type RuntimeType, SchemaInfo Schema, DbDataReader Reader)
        {
            for(int i = 0; i < Schema.Columns.Count; i++)
            {
                string col = Schema.Columns[i];
                PropertyInfo pi = RuntimeType.GetProperty(col);
                if(pi == null || !pi.CanWrite) continue;

                object raw = Reader[col];
                if(raw is DBNull) continue;

                try
                {
                    Type targetType = pi.PropertyType;
                    if(targetType.IsGenericType &&
                        targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        targetType = Nullable.GetUnderlyingType(targetType);
                    }

                    object converted = Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
                    pi.SetValue(Instance, converted, null);
                }
                catch
                {
                    try { pi.SetValue(Instance, raw, null); } catch { }
                }
            }
        }

        #endregion

        #region Dynamic API (IL + DynamicRow, no DTO)

        public async Task<List<DynamicRow>> GetDynamic<TTranslator>(QueryExecutionContext ctx,QueryBuilder Builder,  CancellationToken Ct = default(CancellationToken))where TTranslator : IDbQueryTranslator, new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));
            if(ctx == null) throw new ArgumentNullException(nameof(ctx));

            List<DynamicRow> list = new List<DynamicRow>();
            await ReadDynamic<TTranslator>(ctx, Builder, delegate (DynamicRow row) { list.Add(row); }, Ct)
                .ConfigureAwait(false);
            return list;
        }

        public async Task ReadDynamic<TTranslator>(QueryExecutionContext ctx,QueryBuilder Builder ,Action<DynamicRow> Callback,CancellationToken Ct = default(CancellationToken))where TTranslator : IDbQueryTranslator, new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));
            if(Callback == null) throw new ArgumentNullException(nameof(Callback));
            if(ctx == null) throw new ArgumentNullException(nameof(ctx));

            TTranslator translator = new TTranslator();
            QueryModel model = Builder.Build();
            ctx.RaiseSqlGenerated(model.Sql);
            DbQuery dbq = translator.Translate(model);

            await WithCommandAsync(ctx, dbq.Sql,  dbq.Parameters, async delegate (DbCommand cmd)
            {
                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, Ct).ConfigureAwait(false))
                {
                    SchemaInfo schema = ExtractSchema(reader);

                    if(schema.Columns.Count == 0)
                        return 0;

                    Type ilType = RuntimeTypeFactory.GetOrCreate(schema);

                    while(await ReadSafeAsync(reader, Ct).ConfigureAwait(false))
                    {
                        object inst = Activator.CreateInstance(ilType);
                        FillDynamicObject(inst, ilType, schema, reader);
                        DynamicRow row = new DynamicRow(inst);
                        try
                        {
                            ctx.RaiseSqlDispatch(dbq.Sql);
                            Callback(row);
                        }
                        catch(Exception ex) { ctx.RaiseError(dbq.Sql,ex); }
                        
                    }
                    
                }

                return 0;
            }, Ct).ConfigureAwait(false);
        }

        #endregion
    }
}
