using System;
using System.Collections.Generic;

namespace NekoDbGateway
{
    /// <summary>
    /// Modelo neutro de consulta SQL gerado pelo <see cref="QueryBuilder"/>.
    /// Contém SQL base (sem TOP específico de provedor), parâmetros e metadados.
    /// </summary>
    public sealed class QueryModel
    {
        /// <summary>
        /// SQL neutra gerada pelo <see cref="QueryBuilder"/>.
        /// </summary>
        public string Sql { get; }

        /// <summary>
        /// Parâmetros do comando (somente leitura externamente).
        /// </summary>
        public IReadOnlyDictionary<string, object> Parameters { get; }

        /// <summary>
        /// Valor de TOP solicitado (caso exista). Interpretação fica a cargo do tradutor.
        /// </summary>
        public int? Top { get; }

        public QueryModel(string Sql, Dictionary<string, object> Parameters, int? Top = null)
        {
            if(Sql == null) throw new ArgumentNullException(nameof(Sql));
            if(Parameters == null) throw new ArgumentNullException(nameof(Parameters));

            this.Sql = Sql;
            this.Parameters = new Dictionary<string, object>(Parameters);
            this.Top = Top;
        }
    }

    /// <summary>
    /// Representa uma consulta já traduzida para o SQL específico de um provedor.
    /// Pronta para ser executada pelo <see cref="DatabaseGateway"/>.
    /// </summary>
    public sealed class DbQuery
    {
        /// <summary>
        /// SQL final específica do provedor.
        /// </summary>
        public string Sql { get; }

        /// <summary>
        /// Parâmetros mutáveis, permitindo ajustes após a tradução, se necessário.
        /// </summary>
        public Dictionary<string, object> Parameters { get; }

        public DbQuery(string Sql, Dictionary<string, object> Parameters)
        {
            if(Sql == null) throw new ArgumentNullException(nameof(Sql));

            this.Sql = Sql;
            this.Parameters = Parameters ?? new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Interface de tradutores de <see cref="QueryModel"/> para <see cref="DbQuery"/> (SQL Server, Access, etc.).
    /// </summary>
    public interface IDbQueryTranslator
    {
        /// <summary>
        /// Traduz um <see cref="QueryModel"/> neutro para um <see cref="DbQuery"/> específico de provedor.
        /// </summary>
        DbQuery Translate(QueryModel Model);
    }
}
