data "azurerm_cosmosdb_account" "mhpd" {
  name                = "cosmos-${var.product}-${var.env}-uks"
  resource_group_name = "rg-${var.product}-${var.env}-uksouth"
}

data "azurerm_servicebus_namespace" "mhpd" {
  name                = "sbns-${var.product}-${var.env}-uks"
  resource_group_name = "rg-${var.product}-${var.env}-uksouth"
}

data "azurerm_key_vault" "mhpd" {
  name                = "kv-${var.product}-${var.env}-uks"
  resource_group_name = "rg-${var.product}-${var.env}-uksouth"
}

data "azurerm_key_vault_secret" "newtonsoft_json_schema_license" {
  name         = "newtonsoft-json-schema-license"
  key_vault_id = data.azurerm_key_vault.mhpd.id
}

data "azurerm_key_vault_secret" "redis_endpoint" {
  name         = "redis-endpoint"
  key_vault_id = data.azurerm_key_vault.mhpd.id
}

data "azurerm_key_vault_secret" "redis_primary_key" {
  name         = "redis-primary-key"
  key_vault_id = data.azurerm_key_vault.mhpd.id
}

data "azurerm_private_dns_zone" "app_service" {
  count               = local.pe_enabled ? 1 : 0
  name                = "privatelink.azurewebsites.net"
  resource_group_name = "rg-${var.product}-${var.env}-uksouth"
}
