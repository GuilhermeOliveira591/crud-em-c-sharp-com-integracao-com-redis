using Domain.Entity;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Repository.Insert
{
    public interface IPessoaRepository
    {
        Task InsertAsync(Pessoa pessoa);
        Task<bool> IsPersonActiveAsync(int id);
        Task<bool> IsCpfActiveAsync(string cpf);
        Task<Pessoa> GetByCpfAsync(string cpf);
        Task<Pessoa> GetByIdAsync(int id);
        Task UpdateAsync(int id, Pessoa pessoa);
        Task DeleteAsync(int id);
        Task<IEnumerable<Pessoa>> GetAllAsync();
    }
}
