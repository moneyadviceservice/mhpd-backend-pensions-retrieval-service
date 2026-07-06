data "http" "pensions_retrieval_spec" {
  url = "https://raw.githubusercontent.com/moneyadviceservice/api-docs/refs/heads/main/specs/pension-retrieval.json"
}

data "azurerm_api_management" "this" {
  name                = local.apim_name
  resource_group_name = local.apim_resource_group
}

data "azurerm_api_management_product" "mhpd" {
  product_id          = var.product
  api_management_name = data.azurerm_api_management.this.name
  resource_group_name = data.azurerm_api_management.this.resource_group_name
}
