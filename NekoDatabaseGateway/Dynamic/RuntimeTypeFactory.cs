using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NekoDbGateway.Dynamic
{
    /// <summary>
    /// Fábrica de tipos IL em runtime, com cache LRU por assinatura de schema (nome+tipo de coluna).
    /// </summary>
    public static class RuntimeTypeFactory
    {
        /// <summary>
        //        /// Representa as informações de esquema necessárias para mapeamento dinâmico.
        //        /// </summary>
        public sealed class SchemaInfo
        {
            public List<string> Columns { get; private set; }
            public Dictionary<string, Type> ColumnTypes { get; private set; }

            public SchemaInfo()
            {
                Columns = new List<string>();
                ColumnTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Extrai nomes e tipos de colunas usando <see cref="DbDataReader.GetSchemaTable()"/>.
        /// </summary>
        public static SchemaInfo ExtractSchema(DbDataReader reader)
        {
            SchemaInfo result = new SchemaInfo();
            DataTable dt = reader.GetSchemaTable();
            if(dt == null) return result;

            foreach(DataRow row in dt.Rows)
            {
                string name = row["ColumnName"] != null ? row["ColumnName"].ToString() : "Col" + result.Columns.Count;
                Type type = row["DataType"] as Type ?? typeof(string);

                result.Columns.Add(name);
                if(!result.ColumnTypes.ContainsKey(name))
                {
                    result.ColumnTypes[name] = type;
                }
            }

            return result;
        }
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

        /// <summary>
        /// Retorna um tipo gerado em runtime com propriedades matching nomes e tipos do schema.
        /// Usa cache LRU por assinatura de schema (ordem independente).
        /// </summary>
        public static Type GetOrCreate(SchemaInfo schema)
        {
            if(schema == null) throw new ArgumentNullException("schema");

            string sig = BuildSignature(schema);

            lock(_sync)
            {
                Type t;
                if(_cache.TryGetValue(sig, out t))
                {
                    MoveToTail(sig);
                    return t;
                }

                Type created = CreateType(schema);
                _cache[sig] = created;
                _lru.AddLast(sig);
                EnforceCapacity();
                return created;
            }
        }

        private static string BuildSignature(SchemaInfo schema)
        {
            StringBuilder sb = new StringBuilder();

            List<string> ordered = schema.Columns
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for(int i = 0; i < ordered.Count; i++)
            {
                string col = ordered[i];
                Type t;
                if(!schema.ColumnTypes.TryGetValue(col, out t) || t == null)
                    t = typeof(string);

                sb.Append(col.ToLowerInvariant());
                sb.Append(':');
                sb.Append(t.FullName);
                sb.Append('|');
            }

            return sb.ToString();
        }

        private static Type CreateType(SchemaInfo schema)
        {
            string typeName = "DynamicRowType_" + Interlocked.Increment(ref _typeCounter);
            TypeBuilder tb = _module.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);

            for(int i = 0; i < schema.Columns.Count; i++)
            {
                string propName = schema.Columns[i];
                Type propType;
                if(!schema.ColumnTypes.TryGetValue(propName, out propType) || propType == null)
                    propType = typeof(string);

                FieldBuilder field = tb.DefineField("_" + propName, propType, FieldAttributes.Private);
                PropertyBuilder prop = tb.DefineProperty(propName, System.Reflection.PropertyAttributes.HasDefault, propType, null);

                // getter
                MethodBuilder getter = tb.DefineMethod(
                    "get_" + propName,
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    propType,
                    Type.EmptyTypes);

                ILGenerator ilGet = getter.GetILGenerator();
                ilGet.Emit(OpCodes.Ldarg_0);
                ilGet.Emit(OpCodes.Ldfld, field);
                ilGet.Emit(OpCodes.Ret);

                // setter
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

        private static void MoveToTail(string key)
        {
            LinkedListNode<string> node = _lru.Find(key);
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
}
