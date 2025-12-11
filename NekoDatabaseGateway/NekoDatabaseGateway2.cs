//using System;
//using System.Collections.Generic;
//using System.Data;
//using System.Data.Common;
//using System.Data.OleDb;
//using System.Data.SqlClient;
//using System.Dynamic;
//using System.Globalization;
//using System.Linq;
//using System.Reflection;
//using System.Reflection.Emit;
//using System.Text;
//using System.Text.RegularExpressions;
//using System.Threading;
//using System.Threading.Tasks;

//namespace NekoDbGateway
//{
//    /// <summary>
//    /// Fábrica abstrata de conexões para o <see cref="DatabaseGateway"/>.
//    /// Permite desacoplar o tipo concreto de <see cref="DbConnection"/> (SqlClient, OleDb, etc).
//    /// </summary>
//    public interface IDbConnectionFactory
//    {
//        /// <summary>
//        /// Cria uma nova instância de <see cref="DbConnection"/>.
//        /// A conexão é retornada ainda fechada.
//        /// </summary>
//        Task<DbConnection> Create();
//    }

//    /// <summary>
//    /// Implementação genérica de <see cref="IDbConnectionFactory"/> utilizando <see cref="Activator"/>.
//    /// </summary>
//    /// <typeparam name="T">Tipo concreto de <see cref="DbConnection"/>.</typeparam>
//    public class DbConnectionAbstractFactory<T> : IDbConnectionFactory where T : DbConnection
//    {
//        private readonly string _connectionString;

//        /// <summary>
//        /// Inicializa a fábrica com a connection string informada.
//        /// </summary>
//        public DbConnectionAbstractFactory(string connectionString)
//            => _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

//        /// <inheritdoc />
//        public Task<DbConnection> Create()
//        {
//            var conn = (DbConnection)Activator.CreateInstance(typeof(T), _connectionString);
//            return Task.FromResult(conn);
//        }
//    }

//    /// <summary>
//    /// Fábrica de conexões para Access / OleDb.
//    /// </summary>
//    public sealed class AccessConnectionFactory : DbConnectionAbstractFactory<OleDbConnection>
//    {
//        public AccessConnectionFactory(string connectionString) : base(connectionString) { }
//    }

//    /// <summary>
//    /// Fábrica de conexões para SQL Server.
//    /// </summary>
//    public sealed class SqlConnectionFactory : DbConnectionAbstractFactory<SqlConnection>
//    {
//        public SqlConnectionFactory(string connectionString) : base(connectionString) { }
//    }
//    public static class DataMapper
//    {
//        private static readonly Dictionary<Type, PropertyInfo[]> _cache =
//           new Dictionary<Type, PropertyInfo[]>();

//        /// <summary>
//        /// Converte um dicionário de RecordItem em uma instância tipada de TTranslator.
//        /// Usa conversão segura via RecordItem.As{TTranslator}().  
//        /// </summary>
//        public static T Map<T>(Dictionary<string, RecordItem> row) where T : new()
//        {
//            if(row == null)
//                throw new ArgumentNullException(nameof(row));

//            var obj = new T();
//            var type = typeof(T);

//            if(!_cache.TryGetValue(type, out var props))
//            {
//                props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
//                            .Where(p => p.CanWrite)
//                            .ToArray();
//                _cache[type] = props;
//            }

//            foreach(var prop in props)
//            {
//                // Match by column name (case-insensitive)
//                var kv = row.FirstOrDefault(r =>
//                    string.Equals(r.Key, prop.Name, StringComparison.OrdinalIgnoreCase));

//                if(kv.Key == null)
//                    continue; // column does not exist in the query

//                var record = kv.Value;
//                object converted = ConvertValue(record, prop.PropertyType);

//                try
//                {
//                    prop.SetValue(obj, converted);
//                }
//                catch
//                {
//                    // ignore assignments that fail silently (type mismatch)
//                }
//            }

//            return obj;
//        }

//        /// <summary>
//        /// Converte RecordItem para o tipo esperado pela propriedade.
//        /// </summary>
//        private static object ConvertValue(RecordItem record, Type target)
//        {
//            if(target == typeof(string)) return record.As<string>();

//            if(target == typeof(int) || target == typeof(int?))
//                return record.As<int>();

//            if(target == typeof(long) || target == typeof(long?))
//                return record.As<long>();

//            if(target == typeof(double) || target == typeof(double?))
//                return record.As<double>();

//            if(target == typeof(decimal) || target == typeof(decimal?))
//            {
//                if(decimal.TryParse(record.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
//                    return d;
//                return default(decimal);
//            }

//            if(target == typeof(bool) || target == typeof(bool?))
//                return record.As<bool>();

//            if(target == typeof(DateTime) || target == typeof(DateTime?))
//                return record.As<DateTime>();

//            // fallback: try ChangeType
//            try
//            {
//                return Convert.ChangeType(record.Value, target, CultureInfo.InvariantCulture);
//            }
//            catch
//            {
//                return null;
//            }
//        }
//    }
//    /// <summary>
//    /// Modelo neutro de consulta SQL gerado pelo <see cref="QueryBuilder"/>.
//    /// Contém o SQL base (sem TOP específico de provedor), parâmetros e metadados como TOP.
//    /// </summary>
//    public sealed class QueryModel
//    {
//        /// <summary>
//        /// SQL neutra gerada pelo <see cref="QueryBuilder"/>, sem sintaxe específica de provedor (ex.: sem TOP).
//        /// </summary>
//        public string Sql { get; }

//        /// <summary>
//        /// Parâmetros do comando, imutáveis a partir da perspectiva externa.
//        /// </summary>
//        public IReadOnlyDictionary<string, object> Parameters { get; }

//        /// <summary>
//        /// Valor de TOP solicitado (caso exista). Interpretação fica a cargo do tradutor.
//        /// </summary>
//        public int? Top { get; }

//        public QueryModel(string sql, Dictionary<string, object> parameters, int? top = null)
//        {
//            if(sql == null) throw new ArgumentNullException("sql");
//            if(parameters == null) throw new ArgumentNullException("parameters");

//            Sql = sql;
//            Parameters = new Dictionary<string, object>(parameters);
//            Top = top;
//        }
//    }

   

//    /// <summary>
//    /// Construtor fluente neutro de SQL parametrizada (SELECT, INSERT, UPDATE).
//    /// Gera um <see cref="QueryModel"/> que é traduzido para SQL específico
//    /// por implementações de <see cref="IDbQueryTranslator"/>.
//    /// </summary>
//    public class QueryBuilder
//    {
//        internal enum QueryType
//        {
//            Select,
//            Insert,
//            Update
//        }

//        private QueryType Type;

//        private string _table;

//        private readonly List<string> _columns = new List<string>();
//        private readonly List<string> _conditions = new List<string>();
//        private readonly List<string> _groupByColumns = new List<string>();
//        private readonly List<string> _orderByColumns = new List<string>();
//        private readonly List<string> _joins = new List<string>();

//        private readonly Dictionary<string, object> _insertValues = new Dictionary<string, object>();
//        private readonly Dictionary<string, object> _updateValues = new Dictionary<string, object>();

//        private readonly Dictionary<string, object> _parameters = new Dictionary<string, object>();
//        private int _paramIndex = 0;

//        /// <summary>
//        /// Parâmetros acumulados durante a construção da query.
//        /// </summary>
//        public IReadOnlyDictionary<string, object> Parameters => _parameters;

//        /// <summary>
//        /// SELECT TOP n.
//        /// Armazenado no modelo neutro, aplicado pelo tradutor.
//        /// </summary>
//        private int? _top = null;

//        /// <summary>
//        /// SELECT DISTINCT?
//        /// </summary>
//        private bool IsDistinctSelect = false;

//        /// <summary>
//        /// COUNT(col)? Armazena a coluna alvo ou "*" para COUNT(*).
//        /// </summary>
//        private string CountColumn = null;

//        /// <summary>
//        /// COUNT(DISTINCT col)?
//        /// </summary>
//        private bool IsDistinctCount = false;

//        private string NewParamName()
//        {
//            _paramIndex++;
//            return "@p" + _paramIndex;
//        }

//        // ======================================================
//        // TOP
//        // ======================================================
//        public QueryBuilder Top(int n)
//        {
//            if(n > 0)
//                _top = n;
//            return this;
//        }

//        // ======================================================
//        // SELECT
//        // ======================================================

//        public QueryBuilder Select(params string[] cols)
//        {
//            Type = QueryType.Select;
//            if(cols != null && cols.Length > 0)
//                _columns.AddRange(cols);
//            return this;
//        }

//        /// <summary>
//        /// SELECT DISTINCT colunas...
//        /// </summary>
//        public QueryBuilder SelectDistinct(params string[] cols)
//        {
//            Type = QueryType.Select;
//            IsDistinctSelect = true;
//            if(cols != null && cols.Length > 0)
//                _columns.AddRange(cols);
//            return this;
//        }

//        /// <summary>
//        /// Converte o SELECT atual em DISTINCT (ex.: já havia um SELECT anterior).
//        /// </summary>
//        public QueryBuilder Distinct()
//        {
//            Type = QueryType.Select;
//            IsDistinctSelect = true;
//            return this;
//        }

//        // ======================================================
//        // COUNT / COUNT DISTINCT
//        // ======================================================

//        public QueryBuilder Count()
//        {
//            Type = QueryType.Select;
//            CountColumn = "*";
//            IsDistinctCount = false;
//            return this;
//        }

//        public QueryBuilder Count(string column)
//        {
//            Type = QueryType.Select;
//            CountColumn = column;
//            IsDistinctCount = false;
//            return this;
//        }

//        public QueryBuilder DistinctCount(string column)
//        {
//            Type = QueryType.Select;
//            CountColumn = column;
//            IsDistinctCount = true;
//            return this;
//        }

//        // ======================================================
//        // FROM / JOIN
//        // ======================================================

//        public QueryBuilder From(string table)
//        {
//            _table = table;
//            return this;
//        }

//        /// <summary>
//        /// Adiciona um JOIN genérico.
//        /// Ex: Join("Produtos P", "P.Id = V.ProdutoId", "LEFT")
//        /// </summary>
//        public QueryBuilder Join(string table, string on, string type = "INNER")
//        {
//            _joins.Add(type + " JOIN " + table + " ON " + on);
//            return this;
//        }

//        // ======================================================
//        // WHERE
//        // ======================================================

//        /// <summary>
//        /// Adiciona uma condição WHERE com placeholders do tipo @p1, @p2, ...
//        /// Exemplo: Where("Id = @p1 AND Nome LIKE @p2", 10, "%abc%")
//        /// </summary>
//        public QueryBuilder Where(string condition, params object[] values)
//        {
//            if(!string.IsNullOrWhiteSpace(condition) && values != null && values.Length > 0)
//            {
//                for(int i = 0; i < values.Length; i++)
//                {
//                    string placeholder = "@p" + (i + 1);
//                    string pname = NewParamName();

//                    // Usa Regex para evitar substituir @p1 dentro de @p10, por exemplo.
//                    condition = Regex.Replace(
//                        condition,
//                        Regex.Escape(placeholder) + @"\b",
//                        pname);

//                    _parameters[pname] = values[i];
//                }
//            }

//            if(!string.IsNullOrWhiteSpace(condition))
//                _conditions.Add(condition);

//            return this;
//        }

//        public QueryBuilder WhereIn(string column, IEnumerable<object> values)
//        {
//            if(string.IsNullOrEmpty(column) || values == null)
//                return this;

//            List<object> list = values.ToList();
//            if(list.Count == 0) return this;

//            List<string> prmNames = new List<string>();

//            foreach(object value in list)
//            {
//                string pname = NewParamName();
//                prmNames.Add(pname);
//                _parameters[pname] = value;
//            }

//            _conditions.Add(column + " IN (" + string.Join(", ", prmNames) + ")");
//            return this;
//        }

//        public QueryBuilder WhereNotIn(string column, IEnumerable<object> values)
//        {
//            if(string.IsNullOrEmpty(column) || values == null)
//                return this;

//            List<object> list = values.ToList();
//            if(list.Count == 0) return this;

//            List<string> prmNames = new List<string>();

//            foreach(object value in list)
//            {
//                string pname = NewParamName();
//                prmNames.Add(pname);
//                _parameters[pname] = value;
//            }

//            _conditions.Add(column + " NOT IN (" + string.Join(", ", prmNames) + ")");
//            return this;
//        }

//        public QueryBuilder WhereBetween(string column, object start, object end)
//        {
//            string p1 = NewParamName();
//            string p2 = NewParamName();

//            _parameters[p1] = start;
//            _parameters[p2] = end;

//            _conditions.Add(column + " BETWEEN " + p1 + " AND " + p2);
//            return this;
//        }

//        public QueryBuilder WhereLike(string column, string pattern)
//        {
//            string p = NewParamName();
//            _parameters[p] = pattern;
//            _conditions.Add(column + " LIKE " + p);
//            return this;
//        }

//        public QueryBuilder WhereExists(QueryBuilder subquery)
//        {
//            if(subquery == null) return this;

//            QueryModel model = subquery.Build();
//            _conditions.Add("EXISTS (" + model.Sql + ")");

//            foreach(var kv in model.Parameters)
//                _parameters[kv.Key] = kv.Value;

//            return this;
//        }

//        public QueryBuilder WhereNotExists(QueryBuilder subquery)
//        {
//            if(subquery == null) return this;

//            QueryModel model = subquery.Build();
//            _conditions.Add("NOT EXISTS (" + model.Sql + ")");

//            foreach(var kv in model.Parameters)
//                _parameters[kv.Key] = kv.Value;

//            return this;
//        }

//        // ======================================================
//        // GROUP BY / ORDER BY
//        // ======================================================

//        public QueryBuilder GroupBy(params string[] cols)
//        {
//            if(cols != null && cols.Length > 0)
//                _groupByColumns.AddRange(cols);
//            return this;
//        }

//        /// <summary>
//        /// OrderBy simples. Se quiser direção, passe "Coluna ASC" ou "Coluna DESC".
//        /// Ex: OrderBy("Nome ASC", "Id DESC")
//        /// </summary>
//        public QueryBuilder OrderBy(params string[] cols)
//        {
//            if(cols != null && cols.Length > 0)
//                _orderByColumns.AddRange(cols);
//            return this;
//        }

//        // ======================================================
//        // INSERT
//        // ======================================================

//        public QueryBuilder InsertInto(string table, Dictionary<string, object> values)
//        {
//            Type = QueryType.Insert;
//            _table = table;
//            _insertValues.Clear();

//            if(values != null)
//            {
//                foreach(var kv in values)
//                    _insertValues[kv.Key] = kv.Value;
//            }

//            return this;
//        }

//        // ======================================================
//        // UPDATE
//        // ======================================================

//        public QueryBuilder Update(string table, Dictionary<string, object> values)
//        {
//            Type = QueryType.Update;
//            _table = table;
//            _updateValues.Clear();

//            if(values != null)
//            {
//                foreach(var kv in values)
//                    _updateValues[kv.Key] = kv.Value;
//            }

//            return this;
//        }

//        // ======================================================
//        // BUILD
//        // ======================================================

//        public QueryModel Build()
//        {
//            string sql;

//            switch(Type)
//            {
//                case QueryType.Select:
//                    sql = BuildSelect();
//                    break;
//                case QueryType.Insert:
//                    sql = BuildInsert();
//                    break;
//                case QueryType.Update:
//                    sql = BuildUpdate();
//                    break;
//                default:
//                    throw new InvalidOperationException("Unknown query type.");
//            }

//            return new QueryModel(sql, new Dictionary<string, object>(_parameters), _top);
//        }

//        // ----------------------
//        // SELECT BUILDER
//        // ----------------------
//        private string BuildSelect()
//        {
//            if(string.IsNullOrEmpty(_table))
//                throw new InvalidOperationException("FROM table not specified.");

//            string cols;

//            if(!string.IsNullOrEmpty(CountColumn))
//            {
//                if(IsDistinctCount)
//                    cols = "COUNT(DISTINCT " + CountColumn + ")";
//                else
//                    cols = "COUNT(" + CountColumn + ")";
//            }
//            else
//            {
//                if(IsDistinctSelect)
//                {
//                    if(_columns.Count > 0)
//                        cols = "DISTINCT " + string.Join(", ", _columns);
//                    else
//                        cols = "DISTINCT *";
//                }
//                else
//                {
//                    cols = _columns.Count > 0 ? string.Join(", ", _columns) : "*";
//                }
//            }

//            StringBuilder sb = new StringBuilder();
//            sb.Append("SELECT ").Append(cols).Append(" FROM ").Append(_table);

//            if(_joins.Count > 0)
//                sb.Append(" ").Append(string.Join(" ", _joins));

//            if(_conditions.Count > 0)
//                sb.Append(" WHERE ").Append(string.Join(" AND ", _conditions));

//            if(_groupByColumns.Count > 0)
//                sb.Append(" GROUP BY ").Append(string.Join(", ", _groupByColumns));

//            if(_orderByColumns.Count > 0)
//                sb.Append(" ORDER BY ").Append(string.Join(", ", _orderByColumns));

//            // Importante: NÃO aplica TOP aqui. Fica para o tradutor.
//            return sb.ToString();
//        }

//        // ----------------------
//        // INSERT BUILDER
//        // ----------------------
//        private string BuildInsert()
//        {
//            if(string.IsNullOrEmpty(_table))
//                throw new InvalidOperationException("INSERT table not specified.");

//            if(_insertValues.Count == 0)
//                throw new InvalidOperationException("No values specified for INSERT.");

//            List<string> cols = _insertValues.Keys.ToList();
//            List<string> prmNames = new List<string>();

//            foreach(string col in cols)
//            {
//                string pname = NewParamName();
//                prmNames.Add(pname);
//                _parameters[pname] = _insertValues[col];
//            }

//            string sql = "INSERT INTO " + _table +
//                         " (" + string.Join(", ", cols) + ")" +
//                         " VALUES (" + string.Join(", ", prmNames) + ")";
//            return sql;
//        }

//        // ----------------------
//        // UPDATE BUILDER
//        // ----------------------
//        private string BuildUpdate()
//        {
//            if(string.IsNullOrEmpty(_table))
//                throw new InvalidOperationException("UPDATE table not specified.");

//            if(_updateValues.Count == 0)
//                throw new InvalidOperationException("No values specified for UPDATE.");

//            List<string> sets = new List<string>();

//            foreach(var kv in _updateValues)
//            {
//                string pname = NewParamName();
//                sets.Add(kv.Key + " = " + pname);
//                _parameters[pname] = kv.Value;
//            }

//            StringBuilder sb = new StringBuilder();
//            sb.Append("UPDATE ").Append(_table).Append(" SET ").Append(string.Join(", ", sets));

//            if(_conditions.Count > 0)
//                sb.Append(" WHERE ").Append(string.Join(" AND ", _conditions));

//            return sb.ToString();
//        }
//    }

//    // =========================================================
//    // TRANSLATORS
//    // =========================================================

//    /// <summary>
//    /// Tradutor de <see cref="QueryModel"/> para SQL Server.
//    /// Aplica TOP com sintaxe "SELECT DISTINCT TOP (n)" ou "SELECT TOP (n)".
//    /// </summary>
//    public sealed class SqlServerQueryTranslator : IDbQueryTranslator
//    {
//        public DbQuery Translate(QueryModel model)
//        {
//            if(model == null) throw new ArgumentNullException("model");

//            string sql = model.Sql;

//            if(model.Top.HasValue)
//            {
//                int top = model.Top.Value;

//                const string selectDistinct = "SELECT DISTINCT ";
//                const string select = "SELECT ";

//                if(sql.StartsWith(selectDistinct, StringComparison.OrdinalIgnoreCase))
//                {
//                    // SELECT DISTINCT TOP (n) ...
//                    string rest = sql.Substring(selectDistinct.Length);
//                    sql = "SELECT DISTINCT TOP (" + top + ") " + rest;
//                }
//                else if(sql.StartsWith(select, StringComparison.OrdinalIgnoreCase))
//                {
//                    // SELECT TOP (n) ...
//                    string rest = sql.Substring(select.Length);
//                    sql = "SELECT TOP (" + top + ") " + rest;
//                }
//            }

//            // Clona parâmetros para que DbQuery possua dicionário mutável próprio.
//            var parameters = new Dictionary<string, object>((Dictionary<string, object>)model.Parameters);
//            return new DbQuery(sql, parameters);
//        }
//    }

//    /// <summary>
//    /// Tradutor de <see cref="QueryModel"/> para Access (OleDb).
//    /// Aplica TOP com sintaxe "SELECT TOP n DISTINCT" ou "SELECT TOP n".
//    /// </summary>
//    public sealed class AccessQueryTranslator : IDbQueryTranslator
//    {
//        public DbQuery Translate(QueryModel model)
//        {
//            if(model == null) throw new ArgumentNullException("model");

//            string sql = model.Sql;

//            if(model.Top.HasValue)
//            {
//                int top = model.Top.Value;

//                const string selectDistinct = "SELECT DISTINCT ";
//                const string select = "SELECT ";

//                if(sql.StartsWith(selectDistinct, StringComparison.OrdinalIgnoreCase))
//                {
//                    // SELECT TOP n DISTINCT ...
//                    string rest = sql.Substring(selectDistinct.Length);
//                    sql = "SELECT TOP " + top + " DISTINCT " + rest;
//                }
//                else if(sql.StartsWith(select, StringComparison.OrdinalIgnoreCase))
//                {
//                    // SELECT TOP n ...
//                    string rest = sql.Substring(select.Length);
//                    sql = "SELECT TOP " + top + " " + rest;
//                }
//            }

//            var parameters = new Dictionary<string, object>((Dictionary<string, object>)model.Parameters);
//            return new DbQuery(sql, parameters);
//        }
//    }
//    /// <summary>
//    /// Representa um item de registro (coluna) retornado do banco,
//    /// armazenando <see cref="Type"/> (nome completo), <see cref="Name"/> (nome da coluna)
//    /// e <see cref="Value"/> (valor em texto, cultura invariável).
//    /// </summary>
//    /// <remarks>
//    /// A conversão tipada é oferecida por <see cref="As{T}(T)"/>, com tratamento tolerante a falhas.
//    /// A serialização textual usa <see cref="ToString()"/> e o formato customizável em <see cref="ToString(string, IFormatProvider)"/>.
//    /// </remarks>
//    public class RecordItem
//    {
//        /// <summary>
//        /// Nome completo do tipo original sugerido para o valor (ex.: <c>System.Int32</c>).
//        /// </summary>
//        public string Type;

//        /// <summary>
//        /// Nome da coluna/título do campo.
//        /// </summary>
//        public string Name;

//        /// <summary>
//        /// Valor do campo em representação textual (cultura invariável).
//        /// </summary>
//        public string Value;

//        /// <summary>Construtor padrão.</summary>
//        public RecordItem() { }

//        /// <summary>
//        /// Constrói a partir de um <see cref="int"/> e preenche <see cref="Type"/> e <see cref="Value"/>.
//        /// </summary>
//        public RecordItem(int v)
//        {
//            Type = typeof(int).FullName;
//            Value = $"{v}";
//        }

//        /// <summary>
//        /// Constrói a partir de uma <see cref="string"/> e preenche <see cref="Type"/> e <see cref="Value"/>.
//        /// </summary>
//        public RecordItem(string v)
//        {
//            Type = typeof(string).FullName;
//            Value = $"{v}";
//        }

//        /// <summary>
//        /// Constrói a partir de um <see cref="double"/> e preenche <see cref="Type"/> e <see cref="Value"/>.
//        /// </summary>
//        public RecordItem(double v)
//        {
//            Type = typeof(double).FullName;
//            Value = $"{v}";
//        }

//        /// <summary>
//        /// Converte o <see cref="Value"/> para o tipo <typeparamref name="T"/> de maneira segura.
//        /// </summary>
//        /// <typeparam name="T">Tipo destino da conversão.</typeparam>
//        /// <param name="defaultValue">Valor padrão retornado quando a conversão falhar.</param>
//        /// <returns>Valor convertido ou <paramref name="defaultValue"/> se ocorrer falha.</returns>
//        /// <remarks>
//        /// Usa <see cref="CultureInfo.InvariantCulture"/> ao fazer <c>TryParse</c> de numéricos/data.
//        /// Para <see cref="bool"/>, aceita ainda <c>"0"</c> como <c>false</c> e <c>"1"</c> como <c>true</c>.
//        /// Caso não encontre conversor direto, tenta <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/>.
//        /// </remarks>
//        public T As<T>(T defaultValue = default)
//        {
//            if(string.IsNullOrWhiteSpace(Value))
//                return defaultValue;

//            try
//            {
//                var target = typeof(T);

//                if(target == typeof(string))
//                    return (T)(object)Value;

//                if(target == typeof(int) || target == typeof(int?))
//                {
//                    if(int.TryParse(Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var i))
//                        return (T)(object)i;
//                    return defaultValue;
//                }
//                if(target == typeof(long) || target == typeof(long?))
//                {
//                    if(long.TryParse(Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var i))
//                        return (T)(object)i;
//                    return defaultValue;
//                }

//                if(target == typeof(double) || target == typeof(double?))
//                {
//                    if(double.TryParse(Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
//                        return (T)(object)d;
//                    return defaultValue;
//                }

//                if(target == typeof(DateTime) || target == typeof(DateTime?))
//                {
//                    if(DateTime.TryParse(Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
//                        return (T)(object)dt;
//                    return defaultValue;
//                }

//                if(target == typeof(bool) || target == typeof(bool?))
//                {
//                    if(bool.TryParse(Value, out var b))
//                        return (T)(object)b;
//                    // Aceita "0"/"1"
//                    if(Value == "0") return (T)(object)false;
//                    if(Value == "1") return (T)(object)true;
//                    return defaultValue;
//                }

//                // Fallback geral
//                try
//                {
//                    return (T)Convert.ChangeType(Value, typeof(T), CultureInfo.InvariantCulture);
//                }
//                catch
//                {
//                    // Fallback falhou -> retorna default
//                    Console.WriteLine($"Fallback conversion from {Type} to {nameof(T)} has failed");
//                    return defaultValue;
//                }
//            }
//            catch
//            {
//                return defaultValue;
//            }
//        }

//        /// <summary>
//        /// Retorna <see cref="Value"/> (ou string vazia) como representação textual.
//        /// </summary>
//        public override string ToString() => Value ?? "";

//        /// <summary>
//        /// Retorna uma representação textual customizável com base em especificadores.
//        /// </summary>
//        /// <param name="frmt">
//        /// Formato composto: caracteres de especificação:
//        /// <list type="bullet">
//        /// <item><description><c>t</c>=Type, <c>n</c>=Name, <c>v</c>=Value;</description></item>
//        /// <item><description><c>TTranslator</c>=<c>"Type: "</c>+Type; <c>N</c>=<c>"Name: "</c>+Name; <c>V</c>=<c>"Value: "</c>+Value;</description></item>
//        /// <item><description><c>tnv</c>=atalho para <c>[type: x, name: y, value: z]</c>;</description></item>
//        /// <item><description><c>nv</c>=atalho para <c>[name: y, value: z]</c>.</description></item>
//        /// </list>
//        /// </param>
//        /// <param name="frmtProvider">Provedor de formatação (não utilizado; reservado).</param>
//        /// <returns>Texto formatado conforme especificadores.</returns>
//        public string ToString(string frmt, IFormatProvider frmtProvider)
//        {
//            if(string.IsNullOrEmpty(frmt))
//                return ToString();

//            // Atalhos compostos
//            if(string.Equals(frmt, "tnv", StringComparison.Ordinal))
//                return $"[type: {Type}, name: {Name}, value: {Value}]";
//            if(string.Equals(frmt, "nv", StringComparison.Ordinal))
//                return $"[name: {Name}, value: {Value}]";

//            var sb = new StringBuilder();
//            sb.Append('[');

//            for(int i = 0; i < frmt.Length; i++)
//            {
//                char c = frmt[i];
//                switch(c)
//                {
//                    // Valores simples
//                    case 't': sb.Append(Type); break;
//                    case 'n': sb.Append(Name); break;
//                    case 'v': sb.Append(Value); break;

//                    // Rotulados
//                    case 'T': sb.Append("Type: ").Append(Type); break;
//                    case 'N': sb.Append("Name: ").Append(Name); break;
//                    case 'V': sb.Append("Value: ").Append(Value); break;

//                    // Separadores / literais comuns
//                    case ',': sb.Append(", "); break;
//                    case ' ': sb.Append(' '); break;

//                    // Permite colchetes ou outros literais
//                    case '[': case ']': sb.Append(c); break;
//                    default: sb.Append(c); break;
//                }
//            }

//            sb.Append(']');
//            return sb.ToString();
//        }

//        /// <summary>Conversão explícita para <see cref="int"/> usando <see cref="As{T}(T)"/>.</summary>
//        public static explicit operator int(RecordItem ri) => ri.As<int>();

//        /// <summary>Conversão explícita para <see cref="double"/> usando <see cref="As{T}(T)"/>.</summary>
//        public static explicit operator double(RecordItem ri) => ri.As<double>();

//        /// <summary>Conversão implícita para <see cref="string"/> usando <see cref="As{T}(T)"/>.</summary>
//        public static implicit operator string(RecordItem ri) => ri.As<string>();
//    }

//    /// <summary>
//    /// Gateway genérico para acesso a dados baseado em SQL bruto e em <see cref="QueryBuilder"/>.
//    /// 
//    /// Exposição pública em quatro camadas:
//    /// <list type="bullet">
//    ///   <item><b>Raw</b>: <see cref="GetRaw(string, CancellationToken)"/> / <see cref="ReadRaw(string, Action{Dictionary{string, RecordItem}}, CancellationToken)"/> usando <see cref="RecordItem"/>.</item>
//    ///   <item><b>DTO</b>: <see cref="GetDto{TTranslator, T}(QueryBuilder, CancellationToken)"/> / <see cref="ReadDto{TTranslator, T}(QueryBuilder, Action{T}, CancellationToken)"/> (mapeamento forte via reflexão).</item>
//    ///   <item><b>Dynamic</b>: <see cref="GetDynamic{TTranslator}(QueryBuilder, CancellationToken)"/> / <see cref="ReadDynamic{TTranslator}(QueryBuilder, Action{DynamicRow}, CancellationToken)"/> usando tipos IL + <see cref="DynamicRow"/>.</item>
//    ///   <item><b>Universal</b>: <see cref="Get{TTranslator, T}(QueryBuilder, CancellationToken)"/> / <see cref="Read{TTranslator, T}(QueryBuilder, Action{T}, CancellationToken)"/> com fallback DTO → Dynamic.</item>
//    /// </list>
//    /// </summary>
//    public class DatabaseGateway
//    {
//        private readonly IDbConnectionFactory _connectionFactory;

//        #region ctor

//        /// <summary>
//        /// Cria um <see cref="DatabaseGateway"/> utilizando a fábrica de conexões informada.
//        /// </summary>
//        /// <param name="factory">Fábrica responsável por criar instâncias de <see cref="DbConnection"/>.</param>
//        public DatabaseGateway(IDbConnectionFactory factory)
//        {
//            if(factory == null) throw new ArgumentNullException("factory");
//            _connectionFactory = factory;
//        }

//        #endregion

//        #region Connection / command helpers

//        /// <summary>
//        /// Abre uma conexão usando a fábrica configurada.
//        /// Tenta <see cref="DbConnection.OpenAsync(CancellationToken)"/> com fallback para <see cref="DbConnection.Open()"/>.
//        /// </summary>
//        private async Task<DbConnection> OpenConnectionAsync(CancellationToken ct)
//        {
//            DbConnection conn = await _connectionFactory.Create().ConfigureAwait(false);

//            try
//            {
//                await conn.OpenAsync(ct).ConfigureAwait(false);
//            }
//            catch(NotSupportedException)
//            {
//                conn.Open();
//            }

//            return conn;
//        }

//        /// <summary>
//        /// Helper que cria um <see cref="DbCommand"/> com o SQL fornecido, aplica parâmetros e executa o delegate <paramref name="work"/>.
//        /// Garante abertura e descarte da conexão/comando.
//        /// </summary>
//        private async Task<T> WithCommandAsync<T>(
//            string sql,
//            Dictionary<string, object> parameters,
//            Func<DbCommand, Task<T>> work,
//            CancellationToken ct)
//        {
//            if(sql == null) throw new ArgumentNullException("sql");
//            if(work == null) throw new ArgumentNullException("work");

//            using(DbConnection conn = await OpenConnectionAsync(ct).ConfigureAwait(false))
//            {
//                using(DbCommand cmd = conn.CreateCommand())
//                {
//                    cmd.CommandText = sql;
//                    cmd.CommandType = CommandType.Text;

//                    ApplyParameters(cmd, parameters);

//                    T result = await work(cmd).ConfigureAwait(false);
//                    return result;
//                }
//            }
//        }

//        /// <summary>
//        /// Sobrecarga conveniência quando não há parâmetros.
//        /// </summary>
//        private Task<T> WithCommandAsync<T>(
//            string sql,
//            Func<DbCommand, Task<T>> work,
//            CancellationToken ct)
//        {
//            return WithCommandAsync(sql, null, work, ct);
//        }

//        /// <summary>
//        /// Executa <see cref="DbCommand.ExecuteReaderAsync(CancellationToken)"/> com fallback para <see cref="DbCommand.ExecuteReader()"/>.
//        /// </summary>
//        private static async Task<DbDataReader> ExecuteReaderSafeAsync(DbCommand cmd, CancellationToken ct)
//        {
//            try
//            {
//                DbDataReader reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
//                return reader;
//            }
//            catch(NotSupportedException)
//            {
//                return cmd.ExecuteReader();
//            }
//        }

//        /// <summary>
//        /// Executa <see cref="DbCommand.ExecuteNonQueryAsync(CancellationToken)"/> com fallback para <see cref="DbCommand.ExecuteNonQuery()"/>.
//        /// </summary>
//        private static async Task<int> ExecuteNonQuerySafeAsync(DbCommand cmd, CancellationToken ct)
//        {
//            try
//            {
//                int count = await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
//                return count;
//            }
//            catch(NotSupportedException)
//            {
//                return cmd.ExecuteNonQuery();
//            }
//        }

//        /// <summary>
//        /// Lê a próxima linha do <see cref="DbDataReader"/> com fallback para o método síncrono.
//        /// </summary>
//        private static async Task<bool> ReadSafeAsync(DbDataReader reader, CancellationToken ct)
//        {
//            try
//            {
//                bool has = await reader.ReadAsync(ct).ConfigureAwait(false);
//                return has;
//            }
//            catch(NotSupportedException)
//            {
//                return reader.Read();
//            }
//        }

//        /// <summary>
//        /// Aplica parâmetros ao comando, suportando Access (posicional) e SQL Server (nomeado).
//        /// </summary>
//        private void ApplyParameters(DbCommand cmd, Dictionary<string, object> parameters)
//        {
//            if(parameters == null || parameters.Count == 0)
//                return;

//            bool isAccess = cmd is OleDbCommand;

//            IEnumerable<KeyValuePair<string, object>> ordered;
//            if(isAccess)
//                ordered = parameters.OrderBy(p => p.Key, StringComparer.Ordinal);
//            else
//                ordered = parameters;

//            foreach(KeyValuePair<string, object> kv in ordered)
//            {
//                DbParameter p = cmd.CreateParameter();
//                if(!isAccess)
//                    p.ParameterName = kv.Key;

//                p.Value = kv.Value ?? DBNull.Value;
//                cmd.Parameters.Add(p);
//            }
//        }

//        #endregion

//        #region Schema helpers

//        /// <summary>
//        /// Representa as informações de esquema necessárias para mapeamento dinâmico.
//        /// </summary>
//        private sealed class SchemaInfo
//        {
//            public List<string> Columns { get; private set; }
//            public Dictionary<string, Type> ColumnTypes { get; private set; }

//            public SchemaInfo()
//            {
//                Columns = new List<string>();
//                ColumnTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
//            }
//        }

//        /// <summary>
//        /// Extrai nomes e tipos de colunas usando <see cref="DbDataReader.GetSchemaTable()"/>.
//        /// </summary>
//        private static SchemaInfo ExtractSchema(DbDataReader reader)
//        {
//            SchemaInfo result = new SchemaInfo();
//            DataTable dt = reader.GetSchemaTable();
//            if(dt == null) return result;

//            foreach(DataRow row in dt.Rows)
//            {
//                string name = row["ColumnName"] != null ? row["ColumnName"].ToString() : "Col" + result.Columns.Count;
//                Type type = row["DataType"] as Type ?? typeof(string);

//                result.Columns.Add(name);
//                if(!result.ColumnTypes.ContainsKey(name))
//                {
//                    result.ColumnTypes[name] = type;
//                }
//            }

//            return result;
//        }

//        #endregion

//        #region Raw API (RecordItem)

//        /// <summary>
//        /// Verifica se a expressão SELECT retorna ao menos uma linha.
//        /// </summary>
//        /// <param name="sql">Comando SELECT a ser executado.</param>
//        /// <param name="ct">Token de cancelamento.</param>
//        public async Task<bool> ContainsData(string sql, CancellationToken ct = default(CancellationToken))
//        {
//            bool has = await WithCommandAsync(sql, async delegate (DbCommand cmd)
//            {
//                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, ct).ConfigureAwait(false))
//                {
//                    bool any = await ReadSafeAsync(reader, ct).ConfigureAwait(false);
//                    return any;
//                }
//            }, ct).ConfigureAwait(false);

//            return has;
//        }

//        /// <summary>
//        /// Executa um SELECT simples e retorna uma lista de linhas,
//        /// cada uma como dicionário de <see cref="RecordItem"/> por coluna.
//        /// </summary>
//        public Task<List<Dictionary<string, RecordItem>>> GetRaw(
//            string sql,
//            CancellationToken ct = default(CancellationToken))
//        {
//            return GetRaw(sql, null, ct);
//        }

//        /// <summary>
//        /// Executa um SELECT parametrizado e retorna uma lista de linhas,
//        /// cada uma como dicionário de <see cref="RecordItem"/> por coluna.
//        /// </summary>
//        public async Task<List<Dictionary<string, RecordItem>>> GetRaw(
//            string sql,
//            Dictionary<string, object> parameters,
//            CancellationToken ct = default(CancellationToken))
//        {
//            List<Dictionary<string, RecordItem>> result = await WithCommandAsync(sql, parameters, async delegate (DbCommand cmd)
//            {
//                List<Dictionary<string, RecordItem>> list = new List<Dictionary<string, RecordItem>>();

//                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, ct).ConfigureAwait(false))
//                {
//                    SchemaInfo schema = ExtractSchema(reader);

//                    while(await ReadSafeAsync(reader, ct).ConfigureAwait(false))
//                    {
//                        Dictionary<string, RecordItem> row = new Dictionary<string, RecordItem>(StringComparer.OrdinalIgnoreCase);
//                        foreach(string col in schema.Columns)
//                        {
//                            object raw = reader[col];
//                            string strValue = Convert.ToString(raw, CultureInfo.InvariantCulture);

//                            RecordItem item = new RecordItem
//                            {
//                                Name = col,
//                                Type = schema.ColumnTypes[col].FullName,
//                                Value = strValue
//                            };
//                            row[col] = item;
//                        }
//                        list.Add(row);
//                    }
//                }

//                return list;
//            }, ct).ConfigureAwait(false);

//            return result;
//        }

//        /// <summary>
//        /// Lê linha a linha de um SELECT simples, invocando o callback para cada dicionário de <see cref="RecordItem"/>.
//        /// </summary>
//        public Task ReadRaw(
//            string sql,
//            Action<Dictionary<string, RecordItem>> callback,
//            CancellationToken ct = default(CancellationToken))
//        {
//            return ReadRaw(sql, null, callback, ct);
//        }

//        /// <summary>
//        /// Lê linha a linha de um SELECT parametrizado, invocando o callback para cada dicionário de <see cref="RecordItem"/>.
//        /// </summary>
//        public async Task ReadRaw(
//            string sql,
//            Dictionary<string, object> parameters,
//            Action<Dictionary<string, RecordItem>> callback,
//            CancellationToken ct = default(CancellationToken))
//        {
//            if(callback == null) throw new ArgumentNullException("callback");

//            await WithCommandAsync(sql, parameters, async delegate (DbCommand cmd)
//            {
//                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, ct).ConfigureAwait(false))
//                {
//                    SchemaInfo schema = ExtractSchema(reader);

//                    while(await ReadSafeAsync(reader, ct).ConfigureAwait(false))
//                    {
//                        Dictionary<string, RecordItem> row = new Dictionary<string, RecordItem>(StringComparer.OrdinalIgnoreCase);

//                        foreach(string col in schema.Columns)
//                        {
//                            object raw = reader[col];
//                            string strValue = Convert.ToString(raw, CultureInfo.InvariantCulture);

//                            RecordItem item = new RecordItem
//                            {
//                                Name = col,
//                                Type = schema.ColumnTypes[col].FullName,
//                                Value = strValue
//                            };
//                            row[col] = item;
//                        }

//                        callback(row);
//                    }
//                }

//                return 0;
//            }, ct).ConfigureAwait(false);
//        }

//        /// <summary>
//        /// Executa um comando INSERT/UPDATE/DELETE simples (sem validação de prefixo) com ou sem parâmetros.
//        /// </summary>
//        protected async Task<int> Upsert(
//            string sql,
//            Dictionary<string, object> parameters,
//            CancellationToken ct = default(CancellationToken))
//        {
//            if(string.IsNullOrWhiteSpace(sql))
//                throw new ArgumentNullException("sql");

//            int affected = await WithCommandAsync(sql, parameters, delegate (DbCommand cmd)
//            {
//                return ExecuteNonQuerySafeAsync(cmd, ct);
//            }, ct).ConfigureAwait(false);

//            return affected;
//        }

//        /// <summary>
//        /// Executa um INSERT simples (sem parâmetros).
//        /// </summary>
//        public Task<int> Insert(
//            string sql,
//            CancellationToken ct = default(CancellationToken))
//        {
//            return Upsert(sql, null, ct);
//        }

//        /// <summary>
//        /// Executa um INSERT parametrizado.
//        /// </summary>
//        public Task<int> Insert(
//            string sql,
//            Dictionary<string, object> parameters,
//            CancellationToken ct = default(CancellationToken))
//        {
//            return Upsert(sql, parameters, ct);
//        }

//        /// <summary>
//        /// Executa um UPDATE simples (sem parâmetros).
//        /// </summary>
//        public Task<int> Update(
//            string sql,
//            CancellationToken ct = default(CancellationToken))
//        {
//            return Upsert(sql, null, ct);
//        }

//        /// <summary>
//        /// Executa um UPDATE parametrizado.
//        /// </summary>
//        public Task<int> Update(
//            string sql,
//            Dictionary<string, object> parameters,
//            CancellationToken ct = default(CancellationToken))
//        {
//            return Upsert(sql, parameters, ct);
//        }

//        /// <summary>
//        /// Executa um <see cref="QueryBuilder"/> com um tradutor específico e retorna os registros como <see cref="RecordItem"/>.
//        /// </summary>
//        public async Task<List<Dictionary<string, RecordItem>>> GetRaw<TTranslator>(
//            QueryBuilder builder,
//            CancellationToken ct = default(CancellationToken))
//            where TTranslator : IDbQueryTranslator, new()
//        {
//            if(builder == null) throw new ArgumentNullException("builder");

//            TTranslator translator = new TTranslator();
//            QueryModel model = builder.Build();
//            dynamic dbq = translator.Translate(model);

//            List<Dictionary<string, RecordItem>> result =
//                await GetRaw((string)dbq.Sql, (Dictionary<string, object>)dbq.Parameters, ct).ConfigureAwait(false);
//            return result;
//        }

//        /// <summary>
//        /// Executa um <see cref="QueryBuilder"/> com um tradutor específico e faz leitura linha a linha como <see cref="RecordItem"/>.
//        /// </summary>
//        public async Task ReadRaw<TTranslator>(
//            QueryBuilder builder,
//            Action<Dictionary<string, RecordItem>> callback,
//            CancellationToken ct = default(CancellationToken))
//            where TTranslator : IDbQueryTranslator, new()
//        {
//            if(builder == null) throw new ArgumentNullException("builder");
//            if(callback == null) throw new ArgumentNullException("callback");

//            TTranslator translator = new TTranslator();
//            QueryModel model = builder.Build();
//            dynamic dbq = translator.Translate(model);

//            await ReadRaw((string)dbq.Sql, (Dictionary<string, object>)dbq.Parameters, callback, ct)
//                .ConfigureAwait(false);
//        }

//        /// <summary>
//        /// Executa um INSERT usando um <see cref="QueryBuilder"/> e um tradutor específico.
//        /// </summary>
//        public async Task<int> Insert<TTranslator>(
//            QueryBuilder builder,
//            CancellationToken ct = default(CancellationToken))
//            where TTranslator : IDbQueryTranslator, new()
//        {
//            if(builder == null) throw new ArgumentNullException("builder");

//            TTranslator translator = new TTranslator();
//            QueryModel model = builder.Build();
//            dynamic dbq = translator.Translate(model);

//            int affected = await Insert((string)dbq.Sql, (Dictionary<string, object>)dbq.Parameters, ct)
//                .ConfigureAwait(false);
//            return affected;
//        }

//        /// <summary>
//        /// Executa um UPDATE usando um <see cref="QueryBuilder"/> e um tradutor específico.
//        /// </summary>
//        public async Task<int> Update<TTranslator>(
//            QueryBuilder builder,
//            CancellationToken ct = default(CancellationToken))
//            where TTranslator : IDbQueryTranslator, new()
//        {
//            if(builder == null) throw new ArgumentNullException("builder");

//            TTranslator translator = new TTranslator();
//            QueryModel model = builder.Build();
//            dynamic dbq = translator.Translate(model);

//            int affected = await Update((string)dbq.Sql, (Dictionary<string, object>)dbq.Parameters, ct)
//                .ConfigureAwait(false);
//            return affected;
//        }

//        #endregion

//        #region DTO API (strong typed, no IL fallback)

//        /// <summary>
//        /// Executa um <see cref="QueryBuilder"/> e mapeia o resultado para uma lista de objetos do tipo <typeparamref name="T"/>.
//        /// Usa reflexão simples (sem IL dinâmico) e exige que <typeparamref name="T"/> possua construtor padrão.
//        /// </summary>
//        public async Task<List<T>> GetDto<TTranslator, T>(
//            QueryBuilder builder,
//            CancellationToken ct = default(CancellationToken))
//            where TTranslator : IDbQueryTranslator, new()
//            where T : new()
//        {
//            if(builder == null) throw new ArgumentNullException("builder");

//            TTranslator translator = new TTranslator();
//            QueryModel model = builder.Build();
//            dynamic dbq = translator.Translate(model);

//            List<T> list = new List<T>();

//            await WithCommandAsync((string)dbq.Sql, (Dictionary<string, object>)dbq.Parameters, async delegate (DbCommand cmd)
//            {
//                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, ct).ConfigureAwait(false))
//                {
//                    Type type = typeof(T);
//                    PropertyInfo[] props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

//                    while(await ReadSafeAsync(reader, ct).ConfigureAwait(false))
//                    {
//                        T inst = new T();
//                        for(int i = 0; i < props.Length; i++)
//                        {
//                            PropertyInfo p = props[i];
//                            if(!reader.HasColumn(p.Name))
//                                continue;

//                            object val = reader[p.Name];
//                            if(val is DBNull) continue;

//                            try
//                            {
//                                object converted = Convert.ChangeType(val, p.PropertyType, CultureInfo.InvariantCulture);
//                                p.SetValue(inst, converted, null);
//                            }
//                            catch
//                            {
//                                // ignora falhas de conversão individuais
//                            }
//                        }
//                        list.Add(inst);
//                    }
//                }

//                return 0;
//            }, ct).ConfigureAwait(false);

//            return list;
//        }

//        /// <summary>
//        /// Executa um <see cref="QueryBuilder"/> e chama o callback para cada objeto do tipo <typeparamref name="T"/>.
//        /// Usa reflexão simples (sem IL dinâmico) e exige que <typeparamref name="T"/> possua construtor padrão.
//        /// </summary>
//        public async Task ReadDto<TTranslator, T>(
//            QueryBuilder builder,
//            Action<T> callback,
//            CancellationToken ct = default(CancellationToken))
//            where TTranslator : IDbQueryTranslator, new()
//            where T : new()
//        {
//            if(builder == null) throw new ArgumentNullException("builder");
//            if(callback == null) throw new ArgumentNullException("callback");

//            TTranslator translator = new TTranslator();
//            QueryModel model = builder.Build();
//            dynamic dbq = translator.Translate(model);

//            await WithCommandAsync((string)dbq.Sql, (Dictionary<string, object>)dbq.Parameters, async delegate (DbCommand cmd)
//            {
//                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, ct).ConfigureAwait(false))
//                {
//                    Type type = typeof(T);
//                    PropertyInfo[] props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

//                    while(await ReadSafeAsync(reader, ct).ConfigureAwait(false))
//                    {
//                        T inst = new T();
//                        for(int i = 0; i < props.Length; i++)
//                        {
//                            PropertyInfo p = props[i];
//                            if(!reader.HasColumn(p.Name))
//                                continue;

//                            object val = reader[p.Name];
//                            if(val is DBNull) continue;

//                            try
//                            {
//                                object converted = Convert.ChangeType(val, p.PropertyType, CultureInfo.InvariantCulture);
//                                p.SetValue(inst, converted, null);
//                            }
//                            catch
//                            {
//                                // ignora falhas de conversão individuais
//                            }
//                        }

//                        callback(inst);
//                    }
//                }

//                return 0;
//            }, ct).ConfigureAwait(false);
//        }

//        #endregion

//        #region DynamicRow + RuntimeTypeFactory (IL)

        

      

//        /// <summary>
//        /// Preenche um objeto IL gerado em runtime com base nas colunas e valores do <see cref="DbDataReader"/>.
//        /// </summary>
//        private static void FillDynamicObject(object instance, Type runtimeType, SchemaInfo schema, DbDataReader reader)
//        {
//            for(int i = 0; i < schema.Columns.Count; i++)
//            {
//                string col = schema.Columns[i];
//                PropertyInfo pi = runtimeType.GetProperty(col);
//                if(pi == null || !pi.CanWrite) continue;

//                object raw = reader[col];
//                if(raw is DBNull) continue;

//                try
//                {
//                    Type targetType = pi.PropertyType;
//                    if(targetType.IsGenericType &&
//                        targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
//                    {
//                        targetType = Nullable.GetUnderlyingType(targetType);
//                    }

//                    object converted = Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
//                    pi.SetValue(instance, converted, null);
//                }
//                catch
//                {
//                    try { pi.SetValue(instance, raw, null); } catch { }
//                }
//            }
//        }

//        #endregion

//        #region Dynamic API (IL + DynamicRow, no DTO)

//        /// <summary>
//        /// Executa um <see cref="QueryBuilder"/> e retorna uma lista de <see cref="DynamicRow"/>,
//        /// utilizando tipos IL gerados em runtime conforme o schema.
//        /// </summary>
//        public async Task<List<DynamicRow>> GetDynamic<TTranslator>(
//            QueryBuilder builder,
//            CancellationToken ct = default(CancellationToken))
//            where TTranslator : IDbQueryTranslator, new()
//        {
//            if(builder == null) throw new ArgumentNullException("builder");

//            List<DynamicRow> list = new List<DynamicRow>();
//            await ReadDynamic<TTranslator>(builder, delegate (DynamicRow row) { list.Add(row); }, ct)
//                .ConfigureAwait(false);
//            return list;
//        }

//        /// <summary>
//        /// Executa um <see cref="QueryBuilder"/> e chama o callback para cada <see cref="DynamicRow"/>,
//        /// gerado via IL a partir do schema.
//        /// </summary>
//        public async Task ReadDynamic<TTranslator>(
//            QueryBuilder builder,
//            Action<DynamicRow> callback,
//            CancellationToken ct = default(CancellationToken))
//            where TTranslator : IDbQueryTranslator, new()
//        {
//            if(builder == null) throw new ArgumentNullException("builder");
//            if(callback == null) throw new ArgumentNullException("callback");

//            TTranslator translator = new TTranslator();
//            QueryModel model = builder.Build();
//            dynamic dbq = translator.Translate(model);

//            await WithCommandAsync((string)dbq.Sql, (Dictionary<string, object>)dbq.Parameters, async delegate (DbCommand cmd)
//            {
//                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, ct).ConfigureAwait(false))
//                {
//                    SchemaInfo schema = ExtractSchema(reader);

//                    if(schema.Columns.Count == 0)
//                        return 0;

//                    Type ilType = RuntimeTypeFactory.GetOrCreate(schema);

//                    while(await ReadSafeAsync(reader, ct).ConfigureAwait(false))
//                    {
//                        object inst = Activator.CreateInstance(ilType);
//                        FillDynamicObject(inst, ilType, schema, reader);
//                        DynamicRow row = new DynamicRow(inst);
//                        callback(row);
//                    }
//                }

//                return 0;
//            }, ct).ConfigureAwait(false);
//        }

//        #endregion

//        #region Universal API (DTO → fallback Dynamic)

       
//        // ============================================================================
//        // NEW PUBLIC UNIVERSAL READ (no <T> required)
//        // ============================================================================

//        /// <summary>
//        /// Leitura universal usando apenas o tradutor e o delegate do callback.
//        /// O tipo do parâmetro do callback determina a estratégia:
//        /// <list type="bullet">
//        ///   <item><b>DynamicRow</b> → pipeline dinâmico por IL.</item>
//        ///   <item><b>object</b> → pipeline dinâmico por IL.</item>
//        ///   <item><b>DTO com ctor padrão</b> → tentativa de mapeamento forte (DTO).</item>
//        ///   <item><b>Qualquer outro</b> → fallback para IL.</item>
//        /// </list>
//        /// </summary>
//        public Task Read<TTranslator>(
//            QueryBuilder builder,
//            Delegate handler,
//            CancellationToken ct = default)
//            where TTranslator : IDbQueryTranslator, new()
//        {
//            if(builder == null) throw new ArgumentNullException("builder");
//            if(handler == null) throw new ArgumentNullException("handler");

//            return ReadUniversalDispatch<TTranslator>(builder, handler, ct);
//        }

//        // ============================================================================
//        // INTERNAL UNIVERSAL DISPATCHER
//        // ============================================================================

//        /// <summary>
//        /// Roteia automaticamente para:
//        /// - DTO forte
//        /// - DynamicRow
//        /// - object
//        /// - fallback IL
//        /// baseado no tipo do parâmetro do callback.
//        /// </summary>
//        private Task ReadUniversalDispatch<TTranslator>(
//            QueryBuilder builder,
//            Delegate handler,
//            CancellationToken ct)
//            where TTranslator : IDbQueryTranslator, new()
//        {
//            ParameterInfo[] pars = handler.Method.GetParameters();
//            if(pars == null || pars.Length != 1)
//                throw new InvalidOperationException(
//                    "O callback deve possuir exatamente um parâmetro.");

//            Type paramType = pars[0].ParameterType;

//            // 1) DynamicRow → pipeline IL direto
//            if(paramType == typeof(DynamicRow))
//            {
//                return ReadDynamic<TTranslator>(builder,
//                    row => handler.DynamicInvoke(row), ct);
//            }

//            // 2) object → pipeline IL direto
//            if(paramType == typeof(object))
//            {
//                return ReadDynamic<TTranslator>(builder,
//                    row => handler.DynamicInvoke(row), ct);
//            }

//            // 3) DTO com ctor padrão → tenta mapeamento forte
//            ConstructorInfo ctor = paramType.GetConstructor(Type.EmptyTypes);
//            if(ctor != null) // possui ctor padrão → pode ser DTO
//            {
//                try
//                {
//                    // Invoca ReadDto<TTranslator, paramType> via reflexão
//                    MethodInfo mi = typeof(DatabaseGateway)
//                        .GetMethod("ReadDto",
//                            BindingFlags.Instance | BindingFlags.Public)
//                        .MakeGenericMethod(typeof(TTranslator), paramType);

//                    return (Task)mi.Invoke(this, new object[] { builder, handler, ct });
//                }
//                catch
//                {
//                    // falha → fallback abaixo
//                }
//            }

//            // 4) Fallback final → IL + cast dinâmico
//            return ReadDynamic<TTranslator>(builder,
//                row => handler.DynamicInvoke(row),
//                ct);
//        }




//        #region Universal GET (DTO → fallback Dynamic)

//        // ============================================================================
//        // PUBLIC UNIVERSAL GET (no <T> required)
//        // Default return type = List<dynamic> (based on DynamicRow)
//        // ============================================================================

//        /// <summary>
//        /// GET universal:
//        /// <list type="bullet">
//        ///   <item>If caller does not specify <typeparamref name="T"/>, returns <c>List&lt;dynamic&gt;</c>.</item>
//        ///   <item>If <typeparamref name="T"/> == <see cref="DynamicRow"/> → IL dynamic pipeline.</item>
//        ///   <item>If <typeparamref name="T"/> == <see cref="object"/> → IL dynamic pipeline.</item>
//        ///   <item>If <typeparamref name="T"/> has default ctor → tries DTO mapping.</item>
//        ///   <item>Otherwise → IL fallback.</item>
//        /// </list>
//        /// </summary>
//        public async Task<List<dynamic>> Get<TTranslator>(
//            QueryBuilder builder,
//            CancellationToken ct = default(CancellationToken))
//            where TTranslator : IDbQueryTranslator, new()
//        {
//            return await Get<TTranslator>(builder, typeof(DynamicRow), ct)
//                .ConfigureAwait(false);
//        }

//        /// <summary>
//        /// GET universal receiving an explicit target type at runtime.
//        /// </summary>
//        public async Task<List<dynamic>> Get<TTranslator>(
//            QueryBuilder builder,
//            Type targetType,
//            CancellationToken ct = default(CancellationToken))
//            where TTranslator : IDbQueryTranslator, new()
//        {
//            if(builder == null) throw new ArgumentNullException("builder");
//            if(targetType == null) throw new ArgumentNullException("targetType");

//            return await GetUniversalDispatch<TTranslator>(builder, targetType, ct)
//                .ConfigureAwait(false);
//        }

//        /// <summary>
//        /// Strongly-typed generic GET:
//        /// <list type="bullet">
//        ///   <item>DynamicRow → IL</item>
//        ///   <item>object → IL</item>
//        ///   <item>DTO with ctor → reflection mapping</item>
//        ///   <item>Else → IL fallback</item>
//        /// </list>
//        /// </summary>
//        public async Task<List<T>> Get<TTranslator, T>(
//            QueryBuilder builder,
//            CancellationToken ct = default(CancellationToken))
//            where TTranslator : IDbQueryTranslator, new()
//            where T : new()
//        {
//            if(builder == null) throw new ArgumentNullException("builder");

//            Type targetType = typeof(T);

//            // Case: DynamicRow or object → IL pipeline
//            if(targetType == typeof(DynamicRow) || targetType == typeof(object))
//            {
//                List<DynamicRow> rows = await GetDynamic<TTranslator>(builder, ct)
//                    .ConfigureAwait(false);

//                return rows.Cast<T>().ToList();
//            }

//            // Case: DTO (has parameterless ctor)
//            if(targetType.GetConstructor(Type.EmptyTypes) != null)
//            {
//                try
//                {
//                    List<T> dto = await GetDto<TTranslator, T>(builder, ct)
//                        .ConfigureAwait(false);
//                    return dto;
//                }
//                catch
//                {
//                    // Fallback to IL
//                }
//            }

//            // Final fallback: IL + DynamicRow + cast
//            List<DynamicRow> dyn = await GetDynamic<TTranslator>(builder, ct)
//                .ConfigureAwait(false);

//            return dyn.Cast<T>().ToList();
//        }

//        // ============================================================================
//        // INTERNAL DISPATCHER FOR UNIVERSAL GET
//        // ============================================================================

//        private async Task<List<dynamic>> GetUniversalDispatch<TTranslator>(
//            QueryBuilder builder,
//            Type targetType,
//            CancellationToken ct)
//            where TTranslator : IDbQueryTranslator, new()
//        {
//            // 1) DynamicRow → IL
//            if(targetType == typeof(DynamicRow))
//            {
//                List<DynamicRow> rows = await GetDynamic<TTranslator>(builder, ct)
//                    .ConfigureAwait(false);
//                return rows.Cast<dynamic>().ToList();
//            }

//            // 2) object → IL
//            if(targetType == typeof(object))
//            {
//                List<DynamicRow> rows = await GetDynamic<TTranslator>(builder, ct)
//                    .ConfigureAwait(false);
//                return rows.Cast<dynamic>().ToList();
//            }

//            // 3) DTO with ctor → try mapping
//            ConstructorInfo ctor = targetType.GetConstructor(Type.EmptyTypes);
//            if(ctor != null)
//            {
//                try
//                {
//                    // Use reflection to call generic GetDto<TTranslator, targetType>
//                    MethodInfo mi = typeof(DatabaseGateway)
//                        .GetMethod("GetDto", BindingFlags.Public | BindingFlags.Instance)
//                        .MakeGenericMethod(typeof(TTranslator), targetType);

//                    object task = mi.Invoke(this, new object[] { builder, ct });

//                    // Await the task
//                    await ((Task)task).ConfigureAwait(false);

//                    // Extract List<T>
//                    PropertyInfo resultProp = task.GetType().GetProperty("Result");
//                    object list = resultProp.GetValue(task, null);

//                    return ((IEnumerable<object>)list).Cast<dynamic>().ToList();
//                }
//                catch
//                {
//                    // fallback to IL
//                }
//            }

//            // 4) Fallback IL
//            {
//                List<DynamicRow> rows = await GetDynamic<TTranslator>(builder, ct)
//                    .ConfigureAwait(false);
//                return rows.Cast<dynamic>().ToList();
//            }
//        }

//        #endregion



//        #endregion
//    }

//    #region DbDataReader helper extension

//    /// <summary>
//    /// Extensões utilitárias para <see cref="DbDataReader"/>.
//    /// </summary>
//    internal static class DbDataReaderExtensions
//    {
//        /// <summary>
//        /// Verifica se o reader contém a coluna especificada.
//        /// Útil antes de acessar <c>reader[colName]</c> diretamente.
//        /// </summary>
//        public static bool HasColumn(this IDataRecord reader, string columnName)
//        {
//            if(reader == null) throw new ArgumentNullException("reader");
//            if(string.IsNullOrEmpty(columnName)) return false;

//            int fieldCount = reader.FieldCount;
//            for(int i = 0; i < fieldCount; i++)
//            {
//                string name = reader.GetName(i);
//                if(string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
//                    return true;
//            }

//            return false;
//        }
//    }

//    #endregion
//}
