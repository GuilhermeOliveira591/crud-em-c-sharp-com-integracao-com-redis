using CacheApplicationConsole.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheApplicationConsole.Factory
{
    public interface IPessoaFactory
    {
        Task<Pessoa> CreatePerson(string nomeCompleto, string genero, string cpf, string enderecoCompleto);

        Task<Pessoa> UpdatePerson(int id, PessoaUpdateDto updateDto);

    }
}
