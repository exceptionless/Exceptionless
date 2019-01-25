#!/bin/bash
set -e
CHECKSUM=$(kubectl get configmaps exceptionless-config -o yaml | shasum -a 256 | awk '{print $1}')
kubectl patch deployment exceptionless-statsd -p '{"spec":{"template":{"metadata":{"annotations":{"checksum/config":"$CHECKSUM"}}}}}'
