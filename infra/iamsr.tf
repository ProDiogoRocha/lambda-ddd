module "iamsr_module" {
  source = "git::https://github.com/itau-corp/itau-ey4-modulo-iamsr.git?ref=v2.2.1"

  policies = [
    {
      name     = local.iam_resource_name
      document = "${template_dir.policy_template_dir.destination_dir}/lambda-policies.json"
      path     = "/"
    },
    {
      name     = local.iam_invoke_name
      document = "${template_dir.policy_template_dir.destination_dir}/invoke-lambda-policies.json"
      path     = "/"
    }
  ]

  roles = [
    {
      name                  = local.iam_resource_name ### 'roleName' must have length less than or equal to 64 characters
      trust_policy_document = "${template_dir.trust_template_dir.destination_dir}/lambda-roles.json"

      attached_policies = [
        "arn:aws:iam::${data.aws_caller_identity.current.account_id}:policy/iamsr/${local.iam_resource_name}"
      ]
    },
    {
      name                  = local.iam_invoke_name ### 'roleName' must have length less than or equal to 64 characters
      trust_policy_document = "${template_dir.trust_template_dir.destination_dir}/invoke-lambda-roles.json"

      attached_policies = [
        "arn:aws:iam::${data.aws_caller_identity.current.account_id}:policy/iamsr/${local.iam_invoke_name}"
      ]
    }
  ]

  depends_on = [
    template_dir.policy_template_dir,
    template_dir.trust_template_dir
  ]
}

resource "template_dir" "policy_template_dir" {
  source_dir      = "${path.module}/iamsr/policy-template"
  destination_dir = "${path.module}/iamsr/policy"

  vars = {
    account_id    = data.aws_caller_identity.current.account_id
    aws_region    = data.aws_region.current.name
    function_name = local.function_name
  }
}

resource "template_dir" "trust_template_dir" {
  source_dir      = "${path.module}/iamsr/trust-template"
  destination_dir = "${path.module}/iamsr/trust"
}

### 'roleName' must have length less than or equal to 64 characters
locals {
  iam_resource_name = "${local.microservice_name}-${lower(local.sigla_app)}"
  iam_invoke_name   = "invoke-${local.microservice_name}-${lower(local.sigla_app)}"
  iam_role_name     = local.iam_resource_name
}
