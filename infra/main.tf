module "lambda" {
  source                = "git::https://github.com/itau-corp/itau-ei3-modules-terraform-lambda.git?ref=v2.23.0"
  function_name         = local.function_name
  description           = "BFF da posicao de transportadoras (ECS posicaonumerarionacional e centralizadores)"
  availability_zones    = var.availability_zones
  runtime               = "dotnet10"
  execution_role_arn    = [for role in module.iamsr_module.roles : role if role.name == local.iam_role_name][0].arn
  handler               = "Itau.LN.App.Bff.Posicao.Lambda::Itau.LN.App.Bff.Posicao.Lambda.Function::FunctionHandler"
  timeout               = 28 # o API Gateway corta em 29s; abaixo disso o front recebe o erro da Lambda, não um 504
  code                  = "code/application.zip"
  environment_variables = local.environment_variables
  lambda_layer_map      = { external_account = { arn = local.ca_bundle_layer_arn } }
  owner_team_email      = local.owner_contact_email
  tech_team_email       = local.tech_team_email
  github_repo_id        = var.github_repo_id
  tags                  = local.additional_tags
  create_kms            = true
  concurrent_executions = var.concurrent_executions
  datadog_configuration = local.datadog_configuration
  iupipes_environment   = var.iupipes_environment
  iupipes_build_version = var.iupipes_build_version
  github_repo_name      = var.github_repo_name
  disable_sg_creation   = false
  cw_log_retention_days = var.cw_log_retention_days
  encrypt_log_group     = var.encrypt_log_group
  codedeploy_alarm      = var.codedeploy_alarm
  memory_size           = var.memory_size
  architectures         = var.architectures
  squad_id              = local.squad_id
  sigla                 = local.sigla

  deployment_strategy                    = var.deployment_strategy
  deployment_alias                       = var.deployment_alias
  deployment_auto_rollback_using_cw      = var.deployment_auto_rollback_using_cw
  deployment_auto_rollback_using_cw_mode = var.deployment_auto_rollback_using_cw_mode
  provisioned_concurrency_config         = var.provisioned_concurrency_config
  replace_security_groups_on_destroy     = true
}
