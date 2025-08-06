namespace Domain.Entity
{
    public class Pessoa
    {
        public int Id { get; set; }
        public string NomeCompleto { get; set; }
        public string Genero { get; set; }
        public bool Ativo { get; set; }
        public string CPF { get; set; }
        public string EnderecoCompleto { get; set; }
    }
}