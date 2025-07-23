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
using CacheApplicationConsole.Factory;
using CacheApplicationConsole.DTO;

namespace CacheApplicationConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var currentPath = AppDomain.CurrentDomain.BaseDirectory;
            DotNetEnv.Env.Load(Path.Combine(currentPath, ".env"));

            string sqlConnectionString = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING");
            string redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING");
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(redisConnectionString);
            IDatabase redisDatabase = redis.GetDatabase();

            static async Task<SqlConnection> OpenSqlConnectionAsync(string sqlConnectionString)
            {
                var connection = new SqlConnection(sqlConnectionString);
                await connection.OpenAsync();
                return connection; 
            }


            static async Task<bool> IsPersonActiveAsync (string sqlConnectionString, int id)
            {
                const string query = @"
                    SELECT
                     CASE
                       WHEN EXISTS(SELECT 1 FROM Pessoa WHERE Id = @Id AND Ativo = 1) THEN 1
                       ELSE 0
                     END AS IsPessoaAtiva;
                ";

                using var connection = await OpenSqlConnectionAsync(sqlConnectionString);

                int result = await connection.QueryFirstOrDefaultAsync<int>(query, new { Id=id });

                await connection.CloseAsync();

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

                using var connection = await OpenSqlConnectionAsync(sqlConnectionString);

                int result = await connection.QueryFirstOrDefaultAsync<int>(query, new { CPF = cpf });

                await connection.CloseAsync();

                return result == 1;

            }

            static async Task InsertRecordToRedis(string sqlConnectionString, Pessoa pessoa, IDatabase redisDatabase)
            {
                pessoa.Id = await GetPersonId(sqlConnectionString, pessoa.CPF);

                string jsonValue = JsonConvert.SerializeObject(pessoa);

                redisDatabase.ListRightPush("PessoaList", jsonValue);
            }

            static async Task InsertNewPersonAsync(string sqlConnectionString, Pessoa pessoa)
            {
                const string insertCommand = @"
                    INSERT INTO Pessoa(nomeCompleto, genero, CPF, enderecoCompleto, Ativo) 
                    VALUES(@NomeCompleto, @Genero, @CPF, @EnderecoCompleto, 1)
                ";

                using var connection = await OpenSqlConnectionAsync(sqlConnectionString);

                await connection.ExecuteAsync(insertCommand, new
                {
                    NomeCompleto = pessoa.NomeCompleto,
                    Genero = pessoa.Genero,
                    CPF = pessoa.CPF,
                    EnderecoCompleto = pessoa.EnderecoCompleto
                });

                await connection.CloseAsync();

            }

            static async Task<int> GetPersonId(string sqlConnectionString, string cpf)
            {
                const string query = @"
                    SELECT 
                        Id 
                    FROM Pessoa
                    WHERE CPF = @CPF;
                ";

                using var connection = await OpenSqlConnectionAsync(sqlConnectionString);
                
                var pessoa = await connection.QueryFirstOrDefaultAsync<Pessoa>(query, new { CPF = cpf });

                await connection.CloseAsync();

                if (pessoa.Id != 0 || pessoa.Id != null)
                {
                    return pessoa.Id;
                }

                else
                {
                    Console.WriteLine("Nada a ser exibido");
                    return 0;
                }

            }

            static async Task UpdatePersonAsync (string sqlConnectionString, int id, Pessoa pessoa)
            {
                using var connection = await OpenSqlConnectionAsync(sqlConnectionString);

                var updates = new List<string>();
                using var updateCommand = connection.CreateCommand();

                if (!string.IsNullOrWhiteSpace(pessoa.NomeCompleto))
                {
                    updates.Add("NomeCompleto = @NomeCompleto");
                    updateCommand.Parameters.AddWithValue("@NomeCompleto", pessoa.NomeCompleto);
                }
                if (!string.IsNullOrWhiteSpace(pessoa.Genero))
                {
                    updates.Add("Genero = @Genero");
                    updateCommand.Parameters.AddWithValue("@Genero", pessoa.Genero);
                }
                if (!string.IsNullOrWhiteSpace(pessoa.CPF))
                {
                    updates.Add("CPF = @CPF");
                    updateCommand.Parameters.AddWithValue("@CPF", pessoa.CPF);
                }
                if (!string.IsNullOrWhiteSpace(pessoa.EnderecoCompleto))
                {
                    updates.Add("EnderecoCompleto = @EnderecoCompleto");
                    updateCommand.Parameters.AddWithValue("@EnderecoCompleto", pessoa.EnderecoCompleto);
                }

                if (updates.Count == 0)
                {
                    Console.WriteLine("Nenhum campo para atualizar.");
                    return;
                }

                string updateQuery = $"UPDATE Pessoa SET {string.Join(", ", updates)} WHERE Id = @Id";
                updateCommand.Parameters.Add(new SqlParameter("@Id", id));
                updateCommand.CommandText = updateQuery;

                await updateCommand.ExecuteNonQueryAsync();
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

            static async Task ShowInsertMenu(string sqlConnectionString, IDatabase redisDatabase)
            {

                using var connection = await OpenSqlConnectionAsync(sqlConnectionString);

                Console.WriteLine("Nome Completo: ");
                string nomeCompleto = Console.ReadLine();

                Console.WriteLine("Gênero: ");
                string genero = Console.ReadLine();

                Console.WriteLine("CPF sem traços e pontos: ");
                string CPF = Console.ReadLine();

                if (await IsCpfActiveAsync(sqlConnectionString, CPF))
                {
                    Console.WriteLine("CPF já existente no Banco de Dados \n");
                    return;
                }

                Console.WriteLine("Endereço Completo: ");
                string enderecoCompleto = Console.ReadLine();

                PessoaFactoryImpl pessoaFactory = new PessoaFactoryImpl();
                var pessoa = await pessoaFactory.CreatePerson(nomeCompleto, genero, CPF, enderecoCompleto);

                await InsertNewPersonAsync(sqlConnectionString, pessoa);
                Console.WriteLine("Dados Inseridos \n");

                await InsertRecordToRedis(sqlConnectionString, pessoa, redisDatabase);

                await connection.CloseAsync();
            }

            static async Task ShowUpdateMenuAsync(string sqlConnectionString, IDatabase redisDatabase)
            {

                using var connection = await OpenSqlConnectionAsync(sqlConnectionString);

                int id;
                Console.WriteLine("Qual o id da pessoa que o registro será atualizado?: ");
                while (!int.TryParse(Console.ReadLine(), out id))
                {
                    Console.WriteLine("Por favor, insira um número válido para o Id:");
                }

                if (await IsPersonActiveAsync(sqlConnectionString, id))
                {
                    Console.WriteLine("Id não existente no Banco de Dados \n");
                    return;
                }

                Console.WriteLine("Caso não quiser alterar uma determinada informação, tecle ENTER \n");

                Console.WriteLine("Atualizar -> Nome Completo: ");
                string nomeCompleto = Console.ReadLine();

                Console.WriteLine("Atualizar -> Gênero: ");
                string genero = Console.ReadLine();

                Console.WriteLine("Atualizar -> CPF sem traços e pontos: ");
                string CPF = Console.ReadLine();

                Console.WriteLine("Atualizar -> Endereço Completo: ");
                string enderecoCompleto = Console.ReadLine();

                var updateDto = new PessoaUpdateDto
                {
                    NomeCompleto = string.IsNullOrWhiteSpace(nomeCompleto) ? null : nomeCompleto,
                    Genero = string.IsNullOrWhiteSpace(genero) ? null : genero,
                    CPF = string.IsNullOrWhiteSpace(CPF) ? null : CPF,
                    EnderecoCompleto = string.IsNullOrWhiteSpace(enderecoCompleto) ? null : enderecoCompleto
                };

                PessoaFactoryImpl pessoaFactory = new PessoaFactoryImpl();
                var pessoa = await pessoaFactory.UpdatePerson(id, updateDto);

                await UpdatePersonAsync(sqlConnectionString, id, pessoa);
                Console.WriteLine("Dados Atualizados \n");

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

            static void ShowDeleteMenu(string sqlConnectionString, IDatabase redisDatabase)
            {
                SqlConnection sqlConnection;
                sqlConnection = new SqlConnection(sqlConnectionString);
                sqlConnection.Open();

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

            static void ShowRedisData(IDatabase redisDatabase)
            {

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
                        await ShowInsertMenu(sqlConnectionString, redisDatabase);
                        break;
                    case "2":
                        ShowRedisData(redisDatabase);
                        ShowUpdateMenuAsync(sqlConnectionString, redisDatabase);
                        break;
                    case "3":
                        ShowRedisData(redisDatabase);
                        ShowDeleteMenu(sqlConnectionString, redisDatabase);
                        break;
                    case "4":
                        ShowRedisData(redisDatabase);
                        break;
                    default:
                        break;

                }

            }


        }
    }
}
