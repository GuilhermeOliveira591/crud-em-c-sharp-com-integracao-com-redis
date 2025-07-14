using System;
using StackExchange.Redis;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Dapper;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Newtonsoft.Json;
using System.Data.SqlTypes;
using System.Diagnostics.Metrics;
using System.Collections;
using System.Reflection;
using DotNetEnv;

namespace CacheApplicationConsole
{
    class Program
    {
        static void Main(string[] args)
        {

            string sqlConnectionString = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING");
            string redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING");
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(redisConnectionString);
            IDatabase redisDatabase = redis.GetDatabase();


            static async Task<bool> IsPersonActiveAsync (string sqlConnectionString, int id)
            {
                const string query = @"
                    SELECT
                     CASE
                       WHEN EXISTS(SELECT 1 FROM Pessoa WHERE Id = @Id AND Ativo = 1) THEN 1
                       ELSE 0
                     END AS IsPessoaAtiva;
                ";

                using var connection = new SqlConnection(sqlConnectionString);

                await connection.OpenAsync();

                int result = await connection.QueryFirstOrDefaultAsync<int>(query, new { Id=id });

                connection.Close();

                return result == 1;
            }

            static async Task<bool> IsCpfActiveAsync(string sqlConnectionString, string cpf)
            {
                const string query = @"
                    SELECT
                     CASE
                       WHEN EXISTS(SELECT 1 FROM Pessoa WHERE CPF = @CPF AND Ativo = 1) THEN 1
                       ELSE 0
                     END AS IsPessoaAtiva;
                ";

                using var connection = new SqlConnection(sqlConnectionString);

                await connection.OpenAsync();

                int result = await connection.QueryFirstOrDefaultAsync<int>(query, new { CPF = cpf });

                connection.Close();

                return result == 1;

            }

            static async Task InsertRecordToRedis(string sqlConnectionString, int id, IDatabase redisDatabase)
            {
                bool isPersonActive = await IsPersonActiveAsync(sqlConnectionString, id);

                if (!isPersonActive)
                {
                    Pessoa pessoa = new Pessoa();

                    var jsonObject = new
                    {
                        Id = pessoa.Id,
                        NomeCompleto = pessoa.NomeCompleto,
                        Genero = pessoa.Genero,
                        CPF = pessoa.CPF,
                        EnderecoCompleto = pessoa.EnderecoCompleto
                    };

                    string jsonValue = JsonConvert.SerializeObject(jsonObject);

                    // Insert JSON data into Redis list
                    string redisListKey = $"PessoaList";
                    redisDatabase.ListRightPush(redisListKey, jsonValue);
                }
                else
                {

                }
            }

            static async Task<int> GetPersonId(string sqlConnectionString, string cpf)
            {
                const string query = @"
                    SELECT 
                        Id 
                    FROM Pessoa
                    WHERE CPF = @CPF;
                ";

                using var connection = new SqlConnection(sqlConnectionString);

                await connection.OpenAsync();

                var pessoa = connection.QueryFirstOrDefault<Pessoa>(query, new { CPF = cpf });

                if (pessoa.Id != 0 || pessoa.Id != null)
                {
                    return pessoa.Id;
                }

                else
                {
                    Console.WriteLine("Nada a ser exibido");
                    return 0; // Ou outro valor padrão, dependendo do seu contexto
                }

            }


            static int GetRedisPageNumber(IDatabase redisDatabase, string listaKey, string campoOrdenacao, int valorBuscado, int pageSize)
            {
                RedisValue[] rangeList = redisDatabase.ListRange(listaKey, 0, -1);

                for (long indice = 0; indice < rangeList.Length; indice++)
                {
                    string valor = rangeList[indice];

                    // Verificar se o JSON contém o ID desejado
                    if (valor.Contains($"\"{campoOrdenacao}\":{valorBuscado}"))
                    {
                        int pageNumber = (int)(indice / pageSize) + 1;
                        return pageNumber;
                    }
                }
                return 1;
            }

            static string ShowMenuAndGetChoice()
            {
                string[] options = { "0", "1", "2", "3", "4" };
                string userChoice;

                do
                {
                    Console.WriteLine("Escolha uma opção \n Tecle 0 - Para Sair" +
                        "\n Tecle 1 - Para Inserção de dados " +
                        "\n Tecle 2 - Para Atualizar dados " +
                        "\n Tecle 3 - Para Deletar dados " +
                        "\n Tecle 4 - Para Visualizar dados");

                    userChoice = Console.ReadLine();

                    if (Array.IndexOf(options, userChoice) == -1 && userChoice != "0")
                    {
                        Console.WriteLine("Opção Inválida\n");
                    }

                } while (Array.IndexOf(options, userChoice) == -1);

                return userChoice;
            }

            static async Task ShowInsertMenu(string sqlConnectionString, string redisConnectionString, IDatabase redisDatabase)
            {

                SqlConnection sqlConnection;
                sqlConnection = new SqlConnection(sqlConnectionString);
                sqlConnection.Open();

                //Reading the data been inserted
                Console.WriteLine("Nome Completo: ");
                string nomeCompleto = Console.ReadLine();

                Console.WriteLine("Gênero: ");
                string genero = Console.ReadLine();

                Console.WriteLine("CPF sem traços e pontos: ");
                string CPF = Console.ReadLine();
                bool verificadorCpf = await IsCpfActiveAsync(sqlConnectionString, CPF);

                if(verificadorCpf == false)
                {
                    Console.WriteLine("Endereço Completo: ");
                    string enderecoCompleto = Console.ReadLine();

                    //Preparing the query
                    string insertQuery = "INSERT INTO Pessoa(nomeCompleto, genero, CPF, enderecoCompleto, Ativo) VALUES (@NomeCompleto, @Genero, @CPF, @EnderecoCompleto, 1)";
                    SqlCommand insertCommand = new SqlCommand(insertQuery, sqlConnection);

                    //Adding variable parameters
                    insertCommand.Parameters.AddWithValue("@NomeCompleto", nomeCompleto);
                    insertCommand.Parameters.AddWithValue("@Genero", genero);
                    insertCommand.Parameters.AddWithValue("@CPF", CPF);
                    insertCommand.Parameters.AddWithValue("@EnderecoCompleto", enderecoCompleto);

                    insertCommand.ExecuteNonQuery();
                    Console.WriteLine("Dados Inseridos \n");

                    int id = await GetPersonId(sqlConnectionString, CPF);
                    await InsertRecordToRedis(sqlConnectionString, id, redisDatabase);
                }
                else
                {
                    Console.WriteLine("CPF já existente no Banco de Dados \n");
                }                

                sqlConnection.Close();
            }

            static void ShowUpdateMenu(string sqlConnectionString, string redisConnectionString, IDatabase redisDatabase)
            {

                SqlConnection sqlConnection;
                sqlConnection = new SqlConnection(sqlConnectionString);
                sqlConnection.Open();
                ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(redisConnectionString);

                Console.WriteLine("Qual o Id a ser atualizado?: ");
                int id;
                while (!int.TryParse(Console.ReadLine(), out id))
                {
                    Console.WriteLine("Insira um valor do tipo inteiro! \nQual o Id a ser deletado?: ");
                }

                string selectQuery = $"SELECT COUNT(*) FROM Pessoa WHERE Id = {id} AND Ativo = 1";
                SqlCommand selecter = new SqlCommand(selectQuery, sqlConnection);

                int count = (int)selecter.ExecuteScalar();

                if (count > 0)
                {
                    Console.WriteLine("Caso não quiser alterar uma determinada informação, tecle ENTER \n");

                    Console.WriteLine("Atualizar -> Nome Completo: ");
                    string nomeCompleto = Console.ReadLine();

                    Console.WriteLine("Atualizar -> Gênero: ");
                    string genero = Console.ReadLine();

                    Console.WriteLine("Atualizar -> CPF sem traços e pontos: ");
                    string CPF = Console.ReadLine();

                    Console.WriteLine("Atualizar -> Endereço Completo: ");
                    string enderecoCompleto = Console.ReadLine();

                    string updateQuery = "UPDATE Pessoa SET";
                    SqlCommand updateCommand = new SqlCommand(updateQuery, sqlConnection);

                    if (!string.IsNullOrWhiteSpace(nomeCompleto))
                    {
                        updateQuery += " NomeCompleto = @NomeCompleto,";
                        updateCommand.Parameters.AddWithValue("@NomeCompleto", nomeCompleto);
                    }
                    if (!string.IsNullOrWhiteSpace(genero))
                    {
                        updateQuery += " Genero = @Genero,";
                        updateCommand.Parameters.AddWithValue("@Genero", genero);
                    }
                    if (!string.IsNullOrWhiteSpace(CPF))
                    {
                        updateQuery += " CPF = @CPF,";
                        updateCommand.Parameters.AddWithValue("@CPF", CPF);
                    }
                    if (!string.IsNullOrWhiteSpace(enderecoCompleto))
                    {
                        updateQuery += " EnderecoCompleto = @EnderecoCompleto,";
                        updateCommand.Parameters.AddWithValue("@EnderecoCompleto", enderecoCompleto);
                    }

                    updateQuery = updateQuery.TrimEnd(',');
                    updateQuery += " WHERE Id = @Id";
                    updateCommand.Parameters.AddWithValue("@Id", id);
                    updateCommand.CommandText = updateQuery;
                    updateCommand.ExecuteNonQuery();

                    Console.WriteLine("Valor Atualizado com Sucesso! \n");

                    int paginationSize = 1000;
                    int pageNumber = GetRedisPageNumber(redisDatabase, "PessoaList", "Id", id, paginationSize);
                    int startIndex = (pageNumber - 1) * paginationSize;

                    RedisValue[] rangeList = redisDatabase.ListRange("PessoaList", startIndex, (startIndex + paginationSize) - 1);

                    for (long indice = 0; indice < rangeList.Length; indice++)
                    {
                        string valor = rangeList[indice];

                        // Verificar se o JSON contém o ID desejado
                        if (valor.Contains($"\"Id\":{id}"))
                        {
                            string jsonString = valor.ToString();

                            Pessoa pessoa = JsonConvert.DeserializeObject<Pessoa>(jsonString);

                            if (nomeCompleto != "") { pessoa.NomeCompleto = nomeCompleto; }

                            if (genero != "") { pessoa.Genero = genero; }

                            if (CPF != "") { pessoa.CPF = CPF; }

                            if (enderecoCompleto != "") { pessoa.EnderecoCompleto = enderecoCompleto; }

                            var jsonObject = new
                            {
                                Id = id,
                                NomeCompleto = pessoa.NomeCompleto,
                                Genero = pessoa.Genero,
                                CPF = pessoa.CPF,
                                EnderecoCompleto = pessoa.EnderecoCompleto
                            };

                            var jsonValue = JsonConvert.SerializeObject(jsonObject);


                            redisDatabase.ListSetByIndex($"PessoaList", startIndex + indice, jsonValue);
                            break;
                        }
                    }

                    sqlConnection.Close();
                }
                else
                {
                    Console.WriteLine("Id não encontrado \n");
                }
            }

            static void ShowDeleteMenu(string sqlConnectionString, string redisConnectionString, IDatabase redisDatabase)
            {
                SqlConnection sqlConnection;
                sqlConnection = new SqlConnection(sqlConnectionString);
                sqlConnection.Open();

                ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(redisConnectionString);

                Console.WriteLine("Qual o Id a ser deletado?: ");
                int id;
                while (!int.TryParse(Console.ReadLine(), out id))
                {
                    Console.WriteLine("Insira um valor do tipo inteiro! \nQual o Id a ser deletado?: ");
                }

                string selectQuery = $"SELECT COUNT(*) FROM Pessoa WHERE Id = {id}";
                SqlCommand selecter = new SqlCommand(selectQuery, sqlConnection);

                int count = (int)selecter.ExecuteScalar();

                if (count > 0)
                {
                    string deleteQuery = $"UPDATE Pessoa SET Ativo = 0 WHERE Id = {id}";
                    SqlCommand deleter = new SqlCommand(deleteQuery, sqlConnection);

                    deleter.ExecuteNonQuery();
                    Console.WriteLine("\nRegistro excluído com sucesso! \n");

                    int paginationSize = 1000;
                    int pageNumber = GetRedisPageNumber(redisDatabase, "PessoaList", "Id", id, paginationSize);
                    int startIndex = (pageNumber - 1) * paginationSize;

                    RedisValue[] rangeList = redisDatabase.ListRange("PessoaList", startIndex, (startIndex + paginationSize) - 1);

                    // Iterar sobre os valores e procurar pelo ID desejado
                    for (long indice = 0; indice < rangeList.Length; indice++)
                    {
                        string valor = rangeList[indice];

                        // Verificar se o JSON contém o ID desejado
                        if (valor.Contains($"\"Id\":{id}"))
                        {
                            redisDatabase.ListRemove("PessoaList", rangeList[indice]);
                            break;
                        }
                    }

                }
                else
                {
                    Console.WriteLine("\nId não encontrado \n");
                }

                sqlConnection.Close();
            }

            static void ShowRedisData(string redisConnectionString, IDatabase redisDatabase)
            {
                ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(redisConnectionString);

                string listaKey = "PessoaList"; // Substitua pelo nome real da sua lista no Redis

                // Use LRange para obter todos os elementos na lista
                RedisValue[] lista = redisDatabase.ListRange(listaKey);

                foreach (var jsonValue in lista)
                {
                    string jsonString = jsonValue.ToString();

                    Pessoa pessoa = JsonConvert.DeserializeObject<Pessoa>(jsonString);

                    Console.WriteLine(
                        "Id: " + pessoa.Id +
                        ", Nome: " + pessoa.NomeCompleto +
                        ", Gênero: " + pessoa.Genero +
                        ", CPF: " + pessoa.CPF +
                        ", Endereço Completo: " + pessoa.EnderecoCompleto
                    );
                }

                Console.WriteLine("\n");
            }

            while (true)
            {
                string choice = ShowMenuAndGetChoice();

                switch (choice)
                {
                    case "0":
                        Environment.Exit(0);
                        break;
                    case "1":
                        ShowInsertMenu(sqlConnectionString, redisConnectionString, redisDatabase);
                        break;
                    case "2":
                        ShowRedisData(redisConnectionString, redisDatabase);
                        ShowUpdateMenu(sqlConnectionString, redisConnectionString, redisDatabase);
                        break;
                    case "3":
                        ShowRedisData(redisConnectionString, redisDatabase);
                        ShowDeleteMenu(sqlConnectionString, redisConnectionString, redisDatabase);
                        break;
                    case "4":
                        ShowRedisData(redisConnectionString, redisDatabase);
                        break;
                    default:
                        break;

                }

            }


        }
    }
}
