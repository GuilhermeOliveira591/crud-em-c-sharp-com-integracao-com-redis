using CacheApplicationConsole.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheApplicationConsole.Factory
{
    public class PessoaFactoryImpl : IPessoaFactory
    {
        public async Task<Pessoa> CreatePerson(string nomeCompleto, string genero, string cpf, string enderecoCompleto)
        {
            return new Pessoa
            {
                NomeCompleto = nomeCompleto,
                Genero = genero,
                CPF = cpf,
                EnderecoCompleto = enderecoCompleto
            };
        }

        public async Task<Pessoa> UpdatePerson(int id, PessoaUpdateDto updateDto)
        {
            return new Pessoa
            {
                Id = id,
                NomeCompleto = updateDto.NomeCompleto ?? string.Empty,
                Genero = updateDto.Genero ?? string.Empty,
                CPF = updateDto.CPF ?? string.Empty,
                EnderecoCompleto = updateDto.EnderecoCompleto ?? string.Empty
            };

        }
    }
}
