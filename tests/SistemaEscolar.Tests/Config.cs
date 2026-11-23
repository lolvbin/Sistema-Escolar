// As regras de aprovação e multa ficam em propriedades estáticas (carregadas das Configurações).
// Os testes que as alteram não podem rodar ao mesmo tempo.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
