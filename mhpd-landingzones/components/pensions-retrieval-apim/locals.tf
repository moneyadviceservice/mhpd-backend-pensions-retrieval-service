locals {
  location_short = {
    uksouth = "uks"
    ukwest  = "ukw"
  }
  loc = local.location_short[var.location]

  is_pe_env           = var.env == "nft" || var.env == "stg" || var.env == "prod"
  apim_name           = local.is_pe_env ? "apim-internal-mhpd-${var.env}-${local.loc}" : "apim-mhpd-${var.env}-${local.loc}"
  apim_resource_group = "rg-mhpd-${var.env}-apim-${var.location}"

  api_management_logger_id = "/subscriptions/${var.subscription_id}/resourceGroups/${local.apim_resource_group}/providers/Microsoft.ApiManagement/service/${local.apim_name}/loggers/apim-logger-mhpd-${var.env}"

  pensions_retrieval_backend_url_uks = "https://func-pensions-retrieval-uks-${var.env}.azurewebsites.net"
  pensions_retrieval_backend_url_ukw = "https://func-pensions-retrieval-ukw-${var.env}.azurewebsites.net"
}
