curl -X POST https://generator3.swagger.io/api/generate \
  -H 'content-type: application/json' \
  -d '{
    "specURL" : "https://collector.exceptionless.io/docs/v2/swagger.json",
    "lang" : "typescript-fetch",
    "type" : "CLIENT",
    "options" : {
      "additionalProperties" : {
        "supportsES6": true,
        "typescriptThreePlus": true
      }
    },
    "codegenVersion" : "V3"
  }' --output exceptionless-ts.zip