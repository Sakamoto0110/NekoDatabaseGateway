using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace NekoDbGateway
{
    /// <summary>
    /// Utilitário para mapear dicionários de <see cref="RecordItem"/> para DTOs.
    /// </summary>
    public static class DataMapper
    {
        private static readonly Dictionary<Type, PropertyInfo[]> _cache =
            new Dictionary<Type, PropertyInfo[]>();

        public static T Map<T>(Dictionary<string, RecordItem> Row) where T : new()
        {
            if(Row == null) throw new ArgumentNullException(nameof(Row));

            T obj = new T();
            Type type = typeof(T);

            PropertyInfo[] props;
            if(!_cache.TryGetValue(type, out props))
            {
                props = type
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanWrite)
                    .ToArray();

                _cache[type] = props;
            }

            foreach(PropertyInfo prop in props)
            {
                KeyValuePair<string, RecordItem> kv = Row.FirstOrDefault(r =>
                    string.Equals(r.Key, prop.Name, StringComparison.OrdinalIgnoreCase));

                if(kv.Key == null)
                    continue;

                RecordItem record = kv.Value;
                object converted = ConvertValue(record, prop.PropertyType);

                try
                {
                    prop.SetValue(obj, converted, null);
                }
                catch
                {
                    // ignora falhas individuais
                }
            }

            return obj;
        }

        private static object ConvertValue(RecordItem Record, Type Target)
        {
            if(Target == typeof(string)) return Record.As<string>();

            if(Target == typeof(int) || Target == typeof(int?))
                return Record.As<int>();

            if(Target == typeof(long) || Target == typeof(long?))
                return Record.As<long>();

            if(Target == typeof(double) || Target == typeof(double?))
                return Record.As<double>();

            if(Target == typeof(decimal) || Target == typeof(decimal?))
            {
                decimal d;
                if(decimal.TryParse(Record.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out d))
                    return d;
                return default(decimal);
            }

            if(Target == typeof(bool) || Target == typeof(bool?))
                return Record.As<bool>();

            if(Target == typeof(DateTime) || Target == typeof(DateTime?))
                return Record.As<DateTime>();

            try
            {
                return Convert.ChangeType(Record.Value, Target, CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }
    }
}
