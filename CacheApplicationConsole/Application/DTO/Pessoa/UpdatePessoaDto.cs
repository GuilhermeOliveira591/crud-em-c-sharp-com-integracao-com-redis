using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTO
{
    public class UpdatePessoaDto
    {
        public string NomeCompleto { get; set; }
        public string Genero { get; set; }
        public string CPF { get; set; }
        public string EnderecoCompleto { get; set; }
    }
}
