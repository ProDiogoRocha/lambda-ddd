output "module" {
  value = module.lambda
}

output "lambda_function_name" {
  description = "Lambda function name."
  value       = module.lambda.lambda_function_name
}

output "lambda_deployment_alias" {
  description = "CodeDeploy deployment alias"
  value       = module.lambda.lambda_deployment_alias
}

output "lambda_deployment_source_version" {
  description = "Latest published version of your Lambda Function."
  value       = module.lambda.lambda_deployment_source_version
}

output "lambda_deployment_target_version" {
  description = "Latest published version of your Lambda Function."
  value       = module.lambda.lambda_deployment_target_version
}

output "lambda_deployment_application_name" {
  description = "CodeDeploy deployment application."
  value       = module.lambda.lambda_deployment_application_name
}

output "lambda_deployment_group_name" {
  description = "CodeDeploy deployment group name."
  value       = module.lambda.lambda_deployment_group_name
}

output "lambda_deployment_strategy" {
  description = "CodeDeploy deployment strategy."
  value       = module.lambda.lambda_deployment_strategy
}
