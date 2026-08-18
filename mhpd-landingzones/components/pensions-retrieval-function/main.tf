resource "azurerm_storage_account" "this" {
  name                     = local.storage_name
  resource_group_name      = local.resource_group_name
  location                 = var.location
  account_tier             = "Standard"
  account_replication_type = local.zone_redundant ? "ZRS" : "LRS"

  lifecycle {
    ignore_changes = [tags]
  }
}

resource "azurerm_service_plan" "this" {
  name                   = local.service_plan_name
  resource_group_name    = local.resource_group_name
  location               = var.location
  os_type                = "Windows"
  sku_name               = local.sku_name
  zone_balancing_enabled = local.zone_redundant
  worker_count           = local.zone_redundant ? 3 : null

  lifecycle {
    ignore_changes = [tags]
  }
}

resource "azurerm_windows_function_app" "this" {
  name                          = lower(local.func_name)
  resource_group_name           = local.resource_group_name
  location                      = var.location
  service_plan_id               = azurerm_service_plan.this.id
  storage_account_name          = azurerm_storage_account.this.name
  storage_account_access_key    = azurerm_storage_account.this.primary_access_key
  app_settings                  = local.pensions_retrieval_app_settings
  https_only                    = true
  public_network_access_enabled = false
  virtual_network_subnet_id     = local.enable_vnet_integration ? local.apps_subnet_id : null
  tags                          = {}

  connection_string {
    name  = local.cosmos_db_connection_string.name
    type  = local.cosmos_db_connection_string.type
    value = local.cosmos_db_connection_string.value
  }

  connection_string {
    name  = local.service_bus_connection_string.name
    type  = local.service_bus_connection_string.type
    value = local.service_bus_connection_string.value
  }

  connection_string {
    name  = "RedisConnectionString"
    type  = "Custom"
    value = local.redis_connection_string
  }

  identity {
    type = "SystemAssigned"
  }

  site_config {
    ip_restriction_default_action = local.ip_restriction_default_action
    ftps_state                    = var.ftps_state
    vnet_route_all_enabled        = local.enable_vnet_integration ? true : null
    use_32_bit_worker             = false

    application_stack {
      dotnet_version              = "v10.0"
      use_dotnet_isolated_runtime = true
    }

    dynamic "ip_restriction" {
      for_each = local.ip_restrictions
      content {
        name                      = ip_restriction.value.name
        priority                  = ip_restriction.value.priority
        action                    = ip_restriction.value.action
        virtual_network_subnet_id = ip_restriction.value.virtual_network_subnet_id
        headers                   = ip_restriction.value.headers
        ip_address                = ip_restriction.value.ip_address
      }
    }
  }

  lifecycle {
    ignore_changes = [tags]
  }
}

resource "azurerm_windows_function_app_slot" "staging" {
  count                         = var.env == "prod" ? 1 : 0
  name                          = "staging"
  function_app_id               = azurerm_windows_function_app.this.id
  storage_account_name          = azurerm_storage_account.this.name
  storage_account_access_key    = azurerm_storage_account.this.primary_access_key
  app_settings                  = local.pensions_retrieval_app_settings
  https_only                    = true
  public_network_access_enabled = false
  virtual_network_subnet_id     = local.apps_subnet_id

  connection_string {
    name  = local.cosmos_db_connection_string.name
    type  = local.cosmos_db_connection_string.type
    value = local.cosmos_db_connection_string.value
  }

  connection_string {
    name  = local.service_bus_connection_string.name
    type  = local.service_bus_connection_string.type
    value = local.service_bus_connection_string.value
  }

  connection_string {
    name  = "RedisConnectionString"
    type  = "Custom"
    value = local.redis_connection_string
  }

  identity {
    type = "SystemAssigned"
  }

  site_config {
    ip_restriction_default_action = "Deny"
    ftps_state                    = var.ftps_state
    vnet_route_all_enabled        = true
    use_32_bit_worker             = false

    application_stack {
      dotnet_version              = "v10.0"
      use_dotnet_isolated_runtime = true
    }

    dynamic "ip_restriction" {
      for_each = local.ip_restrictions
      content {
        name                      = ip_restriction.value.name
        priority                  = ip_restriction.value.priority
        action                    = ip_restriction.value.action
        virtual_network_subnet_id = ip_restriction.value.virtual_network_subnet_id
        headers                   = ip_restriction.value.headers
        ip_address                = ip_restriction.value.ip_address
      }
    }
  }

  lifecycle {
    ignore_changes = [tags]
  }
}

resource "azurerm_private_endpoint" "this" {
  count               = local.pe_enabled ? 1 : 0
  name                = "pe-${var.product}-pensions-retrieval-${var.env}-${local.loc}"
  location            = var.location
  resource_group_name = local.resource_group_name
  subnet_id           = local.pe_subnet_id

  private_service_connection {
    name                           = "psc-${var.product}-pensions-retrieval-${var.env}-${local.loc}"
    private_connection_resource_id = azurerm_windows_function_app.this.id
    is_manual_connection           = false
    subresource_names              = ["sites"]
  }

  lifecycle {
    ignore_changes = [tags]
  }

  depends_on = [azurerm_windows_function_app.this]
}

resource "azurerm_application_insights" "this" {
  name                = lower(local.func_name)
  location            = var.location
  resource_group_name = local.resource_group_name

  application_type                      = "web"
  daily_data_cap_in_gb                  = 50
  sampling_percentage                   = 100
  workspace_id                          = local.logs_workspace_id
  retention_in_days                     = 90
  daily_data_cap_notifications_disabled = true

  lifecycle {
    ignore_changes = [tags]
  }
}


resource "azurerm_private_dns_a_record" "app_service" {
  count               = local.pe_enabled ? 1 : 0
  name                = lower(local.func_name)
  zone_name           = data.azurerm_private_dns_zone.app_service[0].name
  resource_group_name = "rg-${var.product}-${var.env}-uksouth"
  ttl                 = 300
  records             = [azurerm_private_endpoint.this[0].private_service_connection[0].private_ip_address]

  lifecycle {
    ignore_changes = [tags]
  }
}

resource "azurerm_private_dns_a_record" "app_service_scm" {
  count               = local.pe_enabled ? 1 : 0
  name                = lower("${local.func_name}.scm")
  zone_name           = data.azurerm_private_dns_zone.app_service[0].name
  resource_group_name = "rg-${var.product}-${var.env}-uksouth"
  ttl                 = 300
  records             = [azurerm_private_endpoint.this[0].private_service_connection[0].private_ip_address]

  lifecycle {
    ignore_changes = [tags]
  }
}