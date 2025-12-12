using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoDbGateway
{
    /// <summary>
    /// Extensões utilitárias para <see cref="IDataRecord"/> / <see cref="System.Data.Common.DbDataReader"/>.
    /// </summary>
    public static class DbDataReaderExtensions
    {
        /// <summary>
        /// Verifica se o reader contém a coluna especificada (case-insensitive).
        /// </summary>
        public static bool HasColumn(this IDataRecord Reader, string ColumnName)
        {
            if(Reader == null) throw new ArgumentNullException(nameof(Reader));
            if(string.IsNullOrEmpty(ColumnName)) return false;

            int fieldCount = Reader.FieldCount;
            for(int i = 0; i < fieldCount; i++)
            {
                string name = Reader.GetName(i);
                if(string.Equals(name, ColumnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
