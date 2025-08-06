using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain.Entity;
using Dapper;
using Domain.Repository.Insert;
using Infrastructure.Shared.Repository;

namespace Infrastructure.Insert
{
    public class PessoaRepositoryImpl : IPessoaRepository
    {

        private readonly ISqlConnectionFactory _connectionFactory;

        public PessoaRepositoryImpl(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public Task DeleteAsync(int id)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Pessoa>> GetAllAsync()
        {
            throw new NotImplementedException();
        }

        public Task<Pessoa> GetByCpfAsync(string cpf)
        {
            throw new NotImplementedException();
        }

        public Task<Pessoa> GetByIdAsync(int id)
        {
            throw new NotImplementedException();
        }

        public async Task InsertAsync(Pessoa pessoa)
        {
            using var connection = await _connectionFactory.OpenConnectionAsync();

            const string insertQuery = @"
                INSERT INTO Pessoa (NomeCompleto, Genero, CPF, EnderecoCompleto, Ativo)
                VALUES (@NomeCompleto, @Genero, @CPF, @EnderecoCompleto, 1);
            ";

            await connection.ExecuteAsync(insertQuery, new
            {
                pessoa.NomeCompleto,
                pessoa.Genero,
                pessoa.CPF,
                pessoa.EnderecoCompleto
            });

        }

        public Task<bool> IsCpfActiveAsync(string cpf)
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsPersonActiveAsync(int id)
        {
            throw new NotImplementedException();
        }

        public Task UpdateAsync(int id, Pessoa pessoa)
        {
            throw new NotImplementedException();
        }
    }
}
