env                   = "hom"
availability_zones    = ["sa-east-1a", "sa-east-1b", "sa-east-1c"]
concurrent_executions = -1 # sem concorrência reservada; "reserved_concurrent_executions" não existe no variables.tf e era ignorada
deployment_strategy   = "CodeDeployDefault.LambdaAllAtOnce"
deployment_alias      = "hom"
architectures         = ["arm64"]
memory_size           = "1024"
datadog_layer_version = "74"

environment_variables = {
  ECS_BASE_URL           = "https://posicaonumerarionacional.api-sp.hom.aws.cloud.ihf/posicaonumerarionacional/v2"
  CENTRALIZADOR_BASE_URL = "https://consul-fornec-num-serv-v1.api-sp.hom.aws.cloud.ihf/consul-fornec-num-serv/v1"
  AUTH_TOKEN_URL         = "https://hom-sts-int.mbi.cloud.ihf/api/oauth/token"
  # Origem do front em hom (https://host, sem barra no fim); igual ao cors_allowed_origin do ApiGateway-Posicao.
  CORS_ALLOWED_ORIGIN = "PREENCHER"
}
