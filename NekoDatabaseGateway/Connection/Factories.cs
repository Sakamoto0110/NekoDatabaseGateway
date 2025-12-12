using System;
using System.Data.Common;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Threading.Tasks;

namespace NekoDbGateway
{
    /// <summary>
    /// Abstração de fábrica de conexões para o <see cref="DatabaseGateway"/>.
    /// Permite desacoplar o tipo concreto de <see cref="DbConnection"/>.
    /// </summary>
    public interface IDbConnectionFactory : IDisposable
    {
        /// <summary>
        /// Cria uma nova instância de <see cref="DbConnection"/> ainda fechada.
        /// </summary>
        Task<DbConnection> Create();
    }

    /// <summary>
    /// Implementação genérica de <see cref="IDbConnectionFactory"/> utilizando <see cref="Activator"/>.
    /// </summary>
    /// <typeparam name="T">Tipo concreto de <see cref="DbConnection"/>.</typeparam>
    public class DbConnectionAbstractFactory<T> : IDbConnectionFactory where T : DbConnection
    {
        private readonly string _connectionString;
        private DbConnection _connection;
        /// <summary>
        /// Inicializa a fábrica com a connection string informada.
        /// </summary>
        public DbConnectionAbstractFactory(string ConnectionString)
        {
            if(ConnectionString == null) throw new ArgumentNullException(nameof(ConnectionString));
            _connectionString = ConnectionString;
        }

        /// <inheritdoc />
        public Task<DbConnection> Create()
        {
            _connection = (DbConnection)Activator.CreateInstance(typeof(T), _connectionString);
            return Task.FromResult(_connection);
        }

        public void Dispose()
        {
            if(_connection != null && _connection.State != System.Data.ConnectionState.Closed)
            {
                try
                {
                    _connection.Close();
                }
                catch(Exception ex) { Debug.WriteLine($"Unable to close {_connection.Database} connection\n{ex.Message}"); }
            }
            _connection.Dispose();            
        }
    }

    /// <summary>
    /// Fábrica de conexões para Access / OleDb.
    /// </summary>
    public sealed class AccessConnectionFactory : DbConnectionAbstractFactory<OleDbConnection>
    {
        public AccessConnectionFactory(string ConnectionString) : base(ConnectionString) { }
    }

    /// <summary>
    /// Fábrica de conexões para SQL Server.
    /// </summary>
    public sealed class SqlConnectionFactory : DbConnectionAbstractFactory<SqlConnection>
    {
        public SqlConnectionFactory(string ConnectionString) : base(ConnectionString) { }
    }
}
