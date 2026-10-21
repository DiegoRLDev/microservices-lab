# Instrucoes para agentes

## Contexto
- MicroservicesLab e um laboratorio de microsservicos.
- Stack: .NET, Go, Angular, Kafka, gRPC, PostgreSQL e Docker.
- Priorizar simplicidade, responsabilidades claras e codigo didatico.
- Consultar README e docs apenas para o contexto necessario, sem duplica-los aqui.

## Desenvolvimento
- Trabalhar na branch atual; nao criar nem trocar branches.
- Nao executar `git add`, `git commit` ou `git push`; commits sao manuais.
- Conferir o estado do Git e preservar alteracoes locais preexistentes.
- Nao alterar regras de negocio fora do escopo nem realizar refatoracoes amplas.
- Preservar contratos existentes; mudancas exigem escopo explicito.
- Nao executar operacoes destrutivas, apagar volumes ou recriar bancos.
- Executar somente a fase/tarefa solicitada; nao avancar fases automaticamente.

## Eficiencia de tokens
- Investigar somente arquivos relacionados a tarefa; evitar leitura integral do repositorio.
- Reutilizar padroes existentes e contexto ja verificado.
- Evitar analises repetidas e comandos redundantes.
- Limitar tentativas repetidas de correcao; registrar bloqueios persistentes e sua causa.
- Preferir relatorios objetivos e evidencias relevantes.

## Validacao proporcional
- CSS/HTML: build e testes frontend relevantes ao impacto.
- Angular: `pnpm test:ci` e `pnpm build` no diretorio do frontend.
- .NET: testes dos projetos afetados.
- Go: `go test ./...` nos modulos afetados.
- Docker: validar configuracoes alteradas; usar `docker compose config --quiet`
  para Compose e build pertinente para Dockerfiles, quando necessario.
- Integracao completa somente quando necessaria; nao interromper servicos sem necessidade.
- Documentacao/instrucoes: revisar conteudo e diff, sem testes de aplicacao desnecessarios.
- Informar o que foi executado e o que nao foi possivel validar.

## Entrega
Apresentar relatorio curto com:
- Alteracoes.
- Testes e resultados.
- Pendencias ou limitacoes.
- Arquivos modificados.
