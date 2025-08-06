using Application.DTO;
using Domain.Entity; 
using Domain.Repository;

namespace Application.UseCase
{
    public class InsertPessoaUseCase
    {
        private readonly ICreatePessoaRepository _repository;

        public InsertPessoaUseCase(ICreatePessoaRepository repository)
        {
            _repository = repository;
        }

        public async Task ExecuteAsync(CreatePessoaDto dto)
        {
            var pessoa = new Pessoa
            {
                NomeCompleto = dto.NomeCompleto,
                Genero = dto.Genero,
                CPF = dto.CPF,
                EnderecoCompleto = dto.EnderecoCompleto,
                Ativo = true
            };

            if (await _repository.IsCpfActiveAsync(pessoa.CPF))
                throw new System.Exception("CPF já existe.");

            await _repository.InsertAsync(pessoa);
        }
    }
}
