locals {
  location_short = {
    uksouth = "uks"
    ukwest  = "ukw"
  }
  loc = local.location_short[var.location]

  resource_group_name = "rg-${var.product}-${var.env}-${var.location}"

  pe_enabled     = var.env == "nft" || var.env == "stg" || var.env == "prod"
  sku_name       = var.env == "prod" || var.env == "nft" ? "EP2" : "EP1"
  zone_redundant = var.env == "prod" && var.location == "uksouth"

  func_name         = "func-pensions-retrieval-${local.loc}-${var.env}"
  service_plan_name = "${var.product}-asp-pensions-retrieval-${local.loc}-${var.env}"
  storage_name      = "sapensretrieval${local.loc}${var.env}"

  logs_resource_group = "rg-mhpd-${var.env}-logs-${var.location}"
  logs_workspace_name = "mhpd-logs-${var.env}-${local.loc}"
  logs_workspace_id   = "/subscriptions/${var.subscription_id}/resourceGroups/${local.logs_resource_group}/providers/Microsoft.OperationalInsights/workspaces/${local.logs_workspace_name}"

  spoke_rg        = "rg-mhpd-${var.env}-spoke-${var.location}"
  spoke_vnet_name = "vnet-mhpd-${var.env}-spoke-${var.location}"
  apps_subnet_id  = "/subscriptions/${var.subscription_id}/resourceGroups/${local.spoke_rg}/providers/Microsoft.Network/virtualNetworks/${local.spoke_vnet_name}/subnets/mhpd-apps"
  apim_subnet_id  = "/subscriptions/${var.subscription_id}/resourceGroups/${local.spoke_rg}/providers/Microsoft.Network/virtualNetworks/${local.spoke_vnet_name}/subnets/mhpd-apim"
  pe_subnet_id    = "/subscriptions/${var.subscription_id}/resourceGroups/${local.spoke_rg}/providers/Microsoft.Network/virtualNetworks/${local.spoke_vnet_name}/subnets/mhpd-private-endpoints"

  enable_vnet_integration       = local.pe_enabled
  ip_restriction_default_action = local.pe_enabled ? "Deny" : "Allow"

  ip_restrictions = local.pe_enabled ? [
    {
      name                      = "mhpd-apim-allow"
      priority                  = 200
      action                    = "Allow"
      virtual_network_subnet_id = local.apim_subnet_id
      ip_address                = null
      headers                   = []
    },
    {
      name                      = "firewall-ip-allow"
      priority                  = 300
      action                    = "Allow"
      virtual_network_subnet_id = null
      ip_address                = "${var.hub_firewall_private_ip}/32"
      headers                   = []
    }
  ] : []

  cosmos_account_name = "cosmos-${var.product}-${var.env}-uks"
  cosmos_endpoint     = var.location == "uksouth" ? "https://${local.cosmos_account_name}.documents.azure.com:443/" : "https://${local.cosmos_account_name}-${var.location}.documents.azure.com:443/"

  cosmos_db_connection_string = {
    name  = "CosmosDBConnectionString"
    type  = "Custom"
    value = "AccountEndpoint=${local.cosmos_endpoint};AccountKey=${data.azurerm_cosmosdb_account.mhpd.primary_key}"
  }

  service_bus_connection_string = {
    name  = "ServiceBusConnectionString"
    type  = "Custom"
    value = data.azurerm_servicebus_namespace.mhpd.default_primary_connection_string
  }

  apim_base_url = local.pe_enabled ? "https://apim-internal-mhpd-${var.env}-uks.azure-api.net" : "https://apim-mhpd-${var.env}-uks.azure-api.net"

  redis_connection_string = "${data.azurerm_key_vault_secret.redis_endpoint.value}:10000,password=${data.azurerm_key_vault_secret.redis_primary_key.value},ssl=True,abortConnect=False"

  pensions_retrieval_app_settings = {
    "APPLICATIONINSIGHTS_CONNECTION_STRING"                   = azurerm_application_insights.this.connection_string
    "FUNCTIONS_EXTENSION_VERSION"                             = "~4"
    "FUNCTIONS_WORKER_RUNTIME"                                = "dotnet-isolated"
    "WEBSITE_ENABLE_SYNC_UPDATE_SITE"                         = "true"
    "WEBSITE_RUN_FROM_PACKAGE"                                = "1"
    "WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED"                  = "1"
    "WEBSITE_CONTENTAZUREFILECONNECTIONSTRING"                = azurerm_storage_account.this.primary_connection_string
    "WEBSITE_CONTENTSHARE"                                    = "pensions-retrieval-${local.loc}-${var.env}"
    "AzureWebJobsStorage"                                     = azurerm_storage_account.this.primary_connection_string
    "PeiIntegrationServiceUrl"                                = "https://pei-integration-service-${local.loc}-${var.env}.azurewebsites.net/"
    "PeiRetryInterval"                                        = "5"
    "PeiRetryTimeout"                                         = "60"
    "PeiRetrievalDuration"                                    = local.pe_enabled ? "60" : "10"
    "PeiPollingInterval"                                      = "5"
    "CommonServiceBusConfiguration__InboundQueue"             = "pensions-retrieval-job"
    "CommonServiceBusConfiguration__OutboundQueue"            = "pension-details-request"
    "CosmosBusinessConfiguration__DatabaseId"                 = "mhpd-business-layer"
    "CosmosBusinessConfiguration__UserSessionDataContainer"   = "mhpdUserSessionData"
    "CosmosBusinessConfiguration__PensionsRetrievalContainer" = "mhpdPensionsRetrievalRecords"
    "NewtonsoftJsonSchemaLicense"                             = data.azurerm_key_vault_secret.newtonsoft_json_schema_license.value
  }
}
