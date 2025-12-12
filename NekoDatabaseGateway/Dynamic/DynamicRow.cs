using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NekoDbGateway.Dynamic
{
    /// <summary>
    /// Representa uma linha dinâmica retornada por IL, permitindo acesso tanto por índice string quanto por membro dinâmico.
    /// </summary>
    public sealed class DynamicRow : DynamicObject
    {
        private readonly object _instance;
        private readonly Dictionary<string, PropertyInfo> _props;

        /// <summary>
        /// Cria um novo <see cref="DynamicRow"/> envolvendo a instância fornecida.
        /// </summary>
        public DynamicRow(object instance)
        {
            if(instance == null) throw new ArgumentNullException("instance");
            _instance = instance;

            PropertyInfo[] properties = instance
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

        /// <summary>
        /// Acesso por nome de coluna.
        /// </summary>
        public object this[string name]
        {
            get { return Get(name); }
            set { Set(name, value); }
        }

        private object Get(string name)
        {
            if(string.IsNullOrEmpty(name)) return null;
            string key = name.ToLowerInvariant();
            PropertyInfo pi;
            if(_props.TryGetValue(key, out pi))
                return pi.GetValue(_instance, null);
            return null;
        }

        private void Set(string name, object value)
        {
            if(string.IsNullOrEmpty(name)) return;
            string key = name.ToLowerInvariant();
            PropertyInfo pi;
            if(_props.TryGetValue(key, out pi) && pi.CanWrite)
                pi.SetValue(_instance, value, null);
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            result = Get(binder.Name);
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            Set(binder.Name, value);
            return true;
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            if(indexes != null && indexes.Length == 1 && indexes[0] is string)
            {
                string name = (string)indexes[0];
                result = Get(name);
                return true;
            }

            result = null;
            return false;
        }

        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value)
        {
            if(indexes != null && indexes.Length == 1 && indexes[0] is string)
            {
                string name = (string)indexes[0];
                Set(name, value);
                return true;
            }

            return false;
        }
    }
}
