locals {

  sigla = "LN"

  sigla_app = "LN-L1021"

  produto = "posicao"

  context = "posicao"

  owner_contact_email = "jorge.luiz-maria@itau-unibanco.com.br"

  tech_team_email = "claudia.dessordi@itau-unibanco.com.br"

  finalidade = "modernizacao"

  squad_id = "NUMERARIO FORNECEDORES E CUSTOS"

  feature_name = "posicao"

  microservice_name = "lambda-ln-app-bff-posicao"

  function_name = local.microservice_name

  empresa = "341"

  additional_tags = {
    "sigla-app"                    = local.sigla_app
    "contexto"                     = local.context
    "finalidade"                   = local.finalidade
    "iu:finops:alocacao:squad"     = local.squad_id
    "iu:finops:alocacao:sigla"     = local.sigla
    "iu:finops:alocacao:sigla-app" = local.sigla_app
    "iu:finops:alocacao:produto"   = local.produto
    "iu:finops:alocacao:empresa"   = local.empresa
    "MicroServiceName"             = local.microservice_name
    "tech-team-email"              = local.tech_team_email
    "owner-team-email"             = local.owner_contact_email
    "map-migrated"                 = "mig2PGTYAWDWG"
  }

  # Layer com o ca_bundle.crt das CAs internas do banco, para as APIs internas (ECS, centralizadores, token).
  # Versões indicadas pelo time (Patrick): 29 funciona em dev/hom, 30 em prod. Cada versão só libera uma organização AWS.
  ca_bundle_layer_arn = {
    dev  = "arn:aws:lambda:sa-east-1:984688426935:layer:cloudsec-itau-ca-bundle-lambda-layer:29"
    hom  = "arn:aws:lambda:sa-east-1:984688426935:layer:cloudsec-itau-ca-bundle-lambda-layer:29"
    prod = "arn:aws:lambda:sa-east-1:984688426935:layer:cloudsec-itau-ca-bundle-lambda-layer:30"
  }[var.env]

  default_environment_variables = {
    "ACCOUNT_ID"  = data.aws_caller_identity.current.account_id,
    "ENVIRONMENT" = var.env,
    # Lidas pelo runtime .NET da Lambda: logs em JSON (campos pesquisáveis no CloudWatch e no Datadog) e nível mínimo.
    # O .NET não reconhece "INFO" (espera "Information"); os demais valores da variável ele aceita.
    "AWS_LAMBDA_HANDLER_LOG_FORMAT" = "Json",
    "AWS_LAMBDA_HANDLER_LOG_LEVEL"  = var.application_log_level == "INFO" ? "Information" : var.application_log_level,
    "DD_ENV"                        = var.env,
    "DD_SERVICE"                    = local.microservice_name,
    "DD_VERSION"                    = var.iupipes_build_version,
    "DD_LOGS_INJECTION"             = "true",
    "DD_SERVERLESS_LOGS_ENABLED"    = "true",
    "DD_TRACE_ENABLED"              = "true",
    "DD_CAPTURE_LAMBDA_PAYLOAD"     = "true",
    "AWS_LAMBDA_EXEC_WRAPPER"       = "/opt/datadog_wrapper",
    # Nome da secret do STS no Secrets Manager (cada ambiente é uma conta, então o nome é o mesmo). Ver CONFIGURACAO.md.
    # A chave-unica é assinada com o certificado empacotado na Lambda (app/.../Certificados), não com secret.
    "Secrets__ConsumoApi" = "${local.microservice_name}/consumo-api",
    # O .NET (e o Go da extensão do Datadog) carrega o SSL_CERT_FILE e todos os arquivos de cada pasta do SSL_CERT_DIR.
    # Arquivo na raiz da layer (versões novas) ou em /opt/certs (versões antigas): os dois caminhos ficam cobertos,
    # e /etc/pki/tls/certs mantém as CAs públicas (Secrets Manager e demais serviços AWS).
    "SSL_CERT_FILE" = "/opt/ca_bundle.crt",
    "SSL_CERT_DIR"  = "/etc/pki/tls/certs:/etc/ssl/certs:/opt/certs",
  }

  environment_variables = merge(local.default_environment_variables, var.environment_variables)

  datadog_configuration = {
    enable      = true
    version     = "1.0.0"
    environment = var.env
    tags = {
      "sigla"          = local.sigla
      "sigla_app"      = local.sigla_app
      "feature_name"   = local.produto
      "squad"          = local.squad_id
      "service"        = local.microservice_name
      "cloud_provider" = "aws"
      "condominio"     = "devops"
      "produto"        = local.produto
      "jornada"        = "modernizacao"
      "repo_url"       = "https://github.com/itau-corp/itau-ln-app-bff-posicao"
      "account_id"     = data.aws_caller_identity.current.account_id
    }
    org                    = "itau"
    capture_lambda_payload = true
    logs_injection         = true
    trace_enabled          = true
    service                = local.microservice_name
    layer_version          = var.datadog_layer_version
  }
}
