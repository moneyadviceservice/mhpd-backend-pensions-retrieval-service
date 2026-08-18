variable "env" {
  type = string
}

variable "location" {
  type    = string
  default = "uksouth"
}

variable "subscription_id" {
  type = string
}

variable "product" {
  type    = string
  default = "mhpd"
}


variable "http_correlation_protocol" {
  type    = string
  default = "W3C"
}

variable "verbosity" {
  type    = string
  default = "verbose"
}

variable "hub_firewall_private_ip" {
  type    = string
  default = null
}

variable "apim_gateway_url" {
  type    = string
  default = null
}
