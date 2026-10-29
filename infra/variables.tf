#####
# GENERAL VARIABLES
#####
variable "application_log_level" {
  description = "Application log level for lambda function. Possible values: 'DEBUG', 'INFO', 'WARN', 'ERROR', 'FATAL', 'TRACE'"
  type        = string
  default     = "INFO"
  validation {
    condition     = contains(["DEBUG", "INFO", "WARN", "ERROR", "FATAL", "TRACE"], var.application_log_level)
    error_message = "Invalid value for application_log_level. Possible values: 'DEBUG', 'INFO', 'WARN', 'ERROR', 'FATAL', 'TRACE'"
  }
}

variable "availability_zones" {
  type        = list(string)
  description = "List of availability zones to connect your lambda to"
}

variable "environment_variables" {
  type        = map(string)
  description = "environment variables to create on the lambda function"
  default     = {}
}

variable "env" {
  type        = string
  description = "Nome do ambiente (dev/hom/prod)"
}

variable "concurrent_executions" {
  type        = number
  description = "Number of concurrent executions"
  default     = 1
}


####
# KMS VARIABLES
####

variable "create_kms" {
  type        = bool
  description = "Create a new KMS Key for the lambda function"
  default     = false
}

variable "kms_master_key" {
  type        = string
  description = "Use provided KMS instead of creating a new one"
  default     = null
}

variable "create_kms_alias" {
  type        = bool
  description = "Create an alias to the KMS key"
  default     = false
}


####
# CW METRIC FILTER
####

variable "deployment_strategy" {
  type        = string
  description = "Deployment strategy to use"
  validation {
    condition = contains([
      "CodeDeployDefault.LambdaCanary10Percent30Minutes",
      "CodeDeployDefault.LambdaCanary10Percent10Minutes",
      "CodeDeployDefault.LambdaLinear10PercentEvery10Minutes",
      "CodeDeployDefault.LambdaCanary10Percent15Minutes",
      "CodeDeployDefault.LambdaAllAtOnce",
      "CodeDeployDefault.LambdaLinear10PercentEvery2Minutes",
      "CodeDeployDefault.LambdaLinear10PercentEvery3Minutes",
      "CodeDeployDefault.LambdaLinear10PercentEvery1Minute",
      "CodeDeployDefault.LambdaCanary10Percent5Minutes"
    ], var.deployment_strategy)
    error_message = "Deployment strategy must be one of the following: 'CodeDeployDefault.LambdaCanary10Percent30Minutes', 'CodeDeployDefault.LambdaCanary10Percent10Minutes', 'CodeDeployDefault.LambdaLinear10PercentEvery10Minutes', 'CodeDeployDefault.LambdaCanary10Percent15Minutes', 'CodeDeployDefault.LambdaAllAtOnce', 'CodeDeployDefault.LambdaLinear10PercentEvery2Minutes', 'CodeDeployDefault.LambdaLinear10PercentEvery3Minutes', 'CodeDeployDefault.LambdaLinear10PercentEvery1Minute', 'CodeDeployDefault.LambdaCanary10Percent5Minutes'."
  }
}

variable "github_repo_id" {
  description = "Github repository ID"
  type        = string
  default     = "GITHUB_REPOSITORY_TAG_PLACEHOLDER"
}

variable "github_repo_name" {
  description = "Github repository Name"
  type        = string
  default     = "GITHUB_REPOSITORY_NAME"
}

variable "codedeploy_alarm" {
  description = "Custom configuration for alarms."
  type = object({
    names                     = list(string)
    enabled                   = bool
    ignore_poll_alarm_failure = bool
    custom_evaluation_periods = number
    custom_threshold          = number
  })
  default = {
    names                     = []
    enabled                   = false
    ignore_poll_alarm_failure = false
    custom_evaluation_periods = 1
    custom_threshold          = 1
  }
}

variable "disable_sg_creation" {
  type        = bool
  description = "disable automatic creation of Security Groups"
  default     = true
}

variable "deployment_auto_rollback_using_cw_mode" {
  type        = string
  description = "Mode for auto rollback alarm configuration: 'CONSERVATIVE' or 'AGGRESSIVE'"
  default     = "CUSTOM"
}

variable "deployment_auto_rollback_using_cw" {
  type        = bool
  description = "Enable auto rollback using cloudwatch alarms. OBS: for DEPLOYMENT_FAILURE scenarios, auto-rollback is always turned on"
  default     = false
}

variable "provisioned_concurrency_config" {
  description = "Configures the quantity of provisioned concurrency for lambda. The deployment_alias will always be used as qualifier."
  type        = number
  default     = null
}

variable "cw_log_retention_days" {
  description = "Cloudwatch log retention period for lambda"
  type        = number
  default     = 7
  validation {
    condition     = var.cw_log_retention_days != 0 && var.cw_log_retention_days <= 180
    error_message = "Invalid value for cw_log_retention_days. Possible values: 1, 3, 5, 7, 14, 30, 60, 90, 120, 150, 180."
  }
}

variable "iupipes_build_version" {
  description = "(Required) IU Pipes Build Version variable."
  type        = string
  default     = "IUPIPES_BUILD_VERSION"
}

variable "iupipes_environment" {
  description = "Environment to deploy the lambda function. Possible values: 'dev', 'hom', 'prod'"
  type        = string
  default     = "IUPIPES_ENVIRONMENT"
}

####
# CLOUDWATCH

variable "encrypt_log_group" {
  type        = bool
  description = "Encrypts the log group using the provided KMS key. If its not managed by the module, must have de appropriate permissions (see https://docs.aws.amazon.com/AmazonCloudWatch/latest/logs/encrypt-log-data-kms.html)"
  default     = false
}

####
# SQS

variable "deployment_alias" {
  type        = string
  description = "Lambda alias."
}

variable "architectures" {
  type        = list(string)
  description = "Instruction set architecture for your Lambda function"
  default     = ["arm64"]
}

variable "memory_size" {
  type        = string
  description = "Memory size for the Lambda function in MB"
  default     = "1024"
}

variable "datadog_layer_version" {
  type = string
}
