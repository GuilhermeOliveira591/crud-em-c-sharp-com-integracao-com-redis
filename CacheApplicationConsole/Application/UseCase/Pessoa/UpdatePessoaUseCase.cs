using System.Threading.Tasks;
using Application.DTO;
using Domain.Entity;
using Domain.Repository;

namespace Application.UseCases.Pessoa
{
    public class UpdatePessoaUseCase
    {
        private readonly ICreatePessoaRepository _repository;

        public UpdatePessoaUseCase(ICreatePessoaRepository repository)
        {
            _repository = repository;
        }

        public async Task ExecuteAsync(int id, UpdatePessoaDto dto)
        {
            var pessoa = await _repository.GetByIdAsync(id);
            if (pessoa == null || !pessoa.Ativo)
                throw new System.Exception("Pessoa não encontrada ou inativa.");

            pessoa.NomeCompleto = string.IsNullOrWhiteSpace(dto.NomeCompleto) ? pessoa.NomeCompleto : dto.NomeCompleto;
            pessoa.Genero = string.IsNullOrWhiteSpace(dto.Genero) ? pessoa.Genero : dto.Genero;
            pessoa.CPF = string.IsNullOrWhiteSpace(dto.CPF) ? pessoa.CPF : dto.CPF;
            pessoa.EnderecoCompleto = string.IsNullOrWhiteSpace(dto.EnderecoCompleto) ? pessoa.EnderecoCompleto : dto.EnderecoCompleto;

            await _repository.UpdateAsync(id, pessoa);
        }
    }
}
