{{/* vim: set filetype=mustache: */}}
{{/*
Expand the name of the chart.
*/}}
{{- define "exceptionless.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
If release name contains chart name it will be used as a full name.
*/}}
{{- define "exceptionless.fullname" -}}
{{- if .Values.fullnameOverride -}}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- $name := default .Chart.Name .Values.nameOverride -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "exceptionless.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/*
Common OTEL environment variables. Only rendered when otel.enabled is true.
Usage: {{ include "exceptionless.otel-env" . | indent N }}
*/}}
{{- define "exceptionless.otel-env" -}}
{{- if and .Values.otel .Values.otel.enabled }}
- name: EX_OTEL_EXPORTER_OTLP_INSECURE
  value: "true"
- name: OTEL_EXPORTER_OTLP_INSECURE
  value: "true"
- name: EX_OTEL_EXPORTER_OTLP_ENDPOINT
  value: http://$(HOST_IP):4317
- name: OTEL_EXPORTER_OTLP_ENDPOINT
  value: http://$(HOST_IP):4317
- name: EX_OTEL_RESOURCE_ATTRIBUTES
  value: k8s.pod.ip=$(K8S_POD_IP),k8s.pod.uid=$(K8S_POD_UID)
- name: OTEL_RESOURCE_ATTRIBUTES
  value: k8s.pod.ip=$(K8S_POD_IP),k8s.pod.uid=$(K8S_POD_UID)
{{- end }}
{{- end -}}
