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

variable "ftps_state" {
  type    = string
  default = "FtpsOnly"
}

variable "hub_firewall_private_ip" {
  type = string
}
