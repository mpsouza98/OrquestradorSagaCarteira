using System.Data;
using MySqlConnector;

namespace OrquestradorSagaCarteira.Infraestrutura.Persistencia;

/// <summary>
/// Factory para criar conexões com o banco de dados
/// </summary>
public class DbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string não configurada");
    }

    public IDbConnection CreateConnection()
    {
        return new MySqlConnection(_connectionString);
    }
}

