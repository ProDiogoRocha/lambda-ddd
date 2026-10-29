env                   = "prod"
availability_zones    = ["sa-east-1a", "sa-east-1b", "sa-east-1c"]
concurrent_executions = -1 # sem concorrência reservada; "reserved_concurrent_executions" não existe no variables.tf e era ignorada
deployment_strategy   = "CodeDeployDefault.LambdaAllAtOnce"
deployment_alias      = "prod"
architectures         = ["arm64"]
memory_size           = "1024"
datadog_layer_version = "74"

# URLs de prod ainda não confirmadas: com PREENCHER a Lambda falha no init (validação de URL), em vez de chamar o host errado.
# Pelo padrão de dev/hom, devem ser algo como https://posicaonumerarionacional.api-sp.prod.aws.cloud.ihf/... — confirme antes.
environment_variables = {
  ECS_BASE_URL           = "PREENCHER"
  CENTRALIZADOR_BASE_URL = "PREENCHER"
  AUTH_TOKEN_URL         = "PREENCHER"
  CORS_ALLOWED_ORIGIN    = "PREENCHER"
}
