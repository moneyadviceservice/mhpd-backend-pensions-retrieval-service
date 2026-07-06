resource "azurerm_api_management_api" "pensions_retrieval_service" {
  name                  = "pension-retrieval-service"
  description           = "This service allows a client to retrieve pensions retrieval records for a pension owner session."
  resource_group_name   = data.azurerm_api_management.this.resource_group_name
  api_management_name   = data.azurerm_api_management.this.name
  revision              = "1"
  display_name          = "pension-retrieval-service"
  path                  = "pension-retrieval-service"
  service_url           = local.pensions_retrieval_backend_url_uks
  protocols             = ["https"]
  subscription_required = false

  subscription_key_parameter_names {
    header = "Ocp-Apim-Subscription-Key"
    query  = "subscription-key"
  }

  import {
    content_format = "swagger-json"
    content_value  = replace(data.http.pensions_retrieval_spec.response_body, "\"info\": {\n    \"title\":", "\"info\": {\n    \"version\": \"1.0\",\n    \"title\":")
  }

  lifecycle {
    ignore_changes = [revision, import]
  }
}

resource "azurerm_api_management_api_policy" "pensions_retrieval_service" {
  api_name            = azurerm_api_management_api.pensions_retrieval_service.name
  api_management_name = data.azurerm_api_management.this.name
  resource_group_name = data.azurerm_api_management.this.resource_group_name

  xml_content = <<-XML
    <policies>
      <inbound>
        <base />
        <choose>
          <when condition="@(context.Deployment.Region == &quot;UK West&quot;)">
            <set-backend-service base-url="${local.pensions_retrieval_backend_url_ukw}" />
          </when>
        </choose>
      </inbound>
      <backend>
        <base />
      </backend>
      <outbound>
        <base />
      </outbound>
      <on-error>
        <base />
      </on-error>
    </policies>
  XML

  lifecycle {
    ignore_changes = [xml_content]
  }
}

resource "azurerm_api_management_product_api" "pensions_retrieval_service" {
  api_name            = azurerm_api_management_api.pensions_retrieval_service.name
  product_id          = data.azurerm_api_management_product.mhpd.product_id
  api_management_name = data.azurerm_api_management.this.name
  resource_group_name = data.azurerm_api_management.this.resource_group_name
}

resource "azurerm_api_management_api_tag" "pensions_retrieval_service" {
  api_id = azurerm_api_management_api.pensions_retrieval_service.id
  name   = "mhpd"
}

resource "azurerm_api_management_api_diagnostic" "pensions_retrieval_service" {
  identifier                = "applicationinsights"
  resource_group_name       = data.azurerm_api_management.this.resource_group_name
  api_management_name       = data.azurerm_api_management.this.name
  api_name                  = azurerm_api_management_api.pensions_retrieval_service.name
  api_management_logger_id  = local.api_management_logger_id
  sampling_percentage       = var.sampling_percentage
  always_log_errors         = true
  log_client_ip             = true
  verbosity                 = var.verbosity
  http_correlation_protocol = var.http_correlation_protocol
}
