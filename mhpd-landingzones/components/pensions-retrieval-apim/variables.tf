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

variable "sampling_percentage" {
  type    = number
  default = 5.0
}

variable "http_correlation_protocol" {
  type    = string
  default = "W3C"
}

variable "verbosity" {
  type    = string
  default = "verbose"
}
