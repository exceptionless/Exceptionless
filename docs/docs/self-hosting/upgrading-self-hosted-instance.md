---
title: "Upgrading"
---

# Upgrading

**Please ensure that you have created backups before upgrading!**

**If you are upgrading from v1 or [v2](https://github.com/exceptionless/Exceptionless/releases/tag/v2.0.0) you will need to upgrade to [v3.0](https://github.com/exceptionless/Exceptionless/releases/tag/v3.0.0) before upgrading to the latest release.**

## Upgrading from v7.1 to v8

We simplified the self hosting process by integrating the UI into the existing app images. As such `exceptionless/ui` docker images are deprecated and we recommend using `exceptionless/app`.

We also upgraded to Elasticsearch 8 in the base images. One requirement of upgrading with existing data is the Elasticsearch data is loaded with the latest Elasticsearch 7.x release. If you are using the all in one image, first use `exceptionless/exceptionless:8.0.0-elasticsearch7` and wait for the app to startup and give it some time. Then turn off the app and then use `exceptionless/exceptionless:8.0.0` image. We will be deprecating the `exceptionless/exceptionless:8.0.0-elasticsearch7` image in the future, this is only there to help with the upgrade process.

You may also need to update the connection strings. In v8.2.7 we updated the Redis Connection String to remove the `server=` prefix.

## Upgrading from v7 to v7.1

We made some changes to email configuration. Yoy wukk now be required to set the `EX_SmtpFrom` config map/environment variable in order to send email. The value should be in the following format: `"Exceptionless <noreply@YOUR_CUSTOM_DOMAIN_NAME>"`

## Upgrading from v6 to v7

A migration job will need to be run as there are several in place data migrations that need to be applied. The migrations will add new index mappings for soft delete support as well as stack status and populate various stack fields with data.

1. You'll need to run an out of process `Migration` job. In order to do this you'll need to update configuration to ensure it's pointing to your Elasticsearch (e.g., `EX_ConnectionStrings__Elasticsearch` environment variable/connection string) and Redis instances.
2. Scale down existing Exceptionless apps and jobs.
3. Start the `Migration` job.
   1. For docker, you just need to pass the `Migration` argument to the `exceptionless/job` container image (e.g., `docker run exceptionless/job:latest Migration`). Please remember to pass any configuration settings (e.g., connection strings) to the migration job.
   2. For Kubernetes, you can run [`kubectl apply -f manual-migration-job.yaml`](https://github.com/exceptionless/Exceptionless/blob/master/k8s/manual-migration-job.yaml). Please note that you may have to tweak namespaces and container image version.
4. Scale back up and you should be good to go.

## Upgrading from v5 to v6

Version 6 added support for Elasticsearch 7, which requires a complete data migration from Elasticsearch 5.x. This tutorial assumes you have docker/Kubernetes installed and have followed the [setup guide](/docs/self-hosting/).

1. Create a new Elasticsearch 7 cluster or modify your existing `docker-compose` file to also include our Elasticsearch 7.x image (There will need to be two Elasticsearch docker instances (5.x and 7.x). Please note that we've included the Elasticsearch major version number in the data path of the docker data path, this allows you to run two versions side by side without losing the data.
2. Add a new environment variable or config map setting for `EX_ElasticsearchToMigrate` with the value of the `EX_Elasticsearch` 5.x connection string.
3. Add your existing Elasticsearch instance to `reindex.remote.whitelist` in Elasticsearch 7's `elasticsearch.yml`. Make sure you include the port as well, for example `reindex.remote.whitelist: 127.0.0.1:9200`.
4. Update the existing `EX_Elasticsearch` environment variable or config map entry to point to the Elasticsearch 7.x Cluster.
5. Scale down existing Exceptionless apps and jobs.
6. Start the Data Migration by running the `DataMigration` job. You can run it by opening the CLI and executing `dotnet Exceptionless.Job.dll DataMigration`. If you receive a warning that the port is already used, you need to change `ASPNETCORE_URLS` first, for example `ASPNETCORE_URLS=http://+:8080`. You can also run an incremental reindex by setting the `EX_ReindexCutOffDate` with a date value (E.G., `2022-01-28T18:00:00.00Z`., environment variable or config map entry and rerunning the Migration Job.
7. Scale up the rest of the app!

## Upgrading from v4 to v5

We now only provide official docker images as release artifacts. This tutorial assumes you have docker/Kubernetes installed and have followed the [setup guide](/docs/self-hosting/).

1. We now can run on linux or windows as we are running on ASP.NET Core! As a result we've completely redone the configuration. For the most part we've prefixed configuration with `EX_` and simplified it as much as possible. I'd recommend taking a look at your previous configuration settings and then read over the following [configuration document](/docs/self-hosting/kubernetes#configuration) to migrate your settings.
2. Please note that no Elasticsearch changes are required. You can continue to use your existing Elasticsearch cluster. You just need to update the connection string by following step 1.
3. Please note that the UI and API no longer run on the same port as they are now two different docker images. You may need to update your server url accordingly in your client applications.

## Upgrading from v3 to v4

This process requires you to setup and configure a new Elasticsearch 5 instance and reindex your existing Elasticsearch 1.x data into the Elasticsearch 5 instance using [external reindexing](https://www.elastic.co/guide/en/elasticsearch/reference/current/docs-reindex.html). This tutorial assumes you have Elasticsearch 5 Kibana instance installed and configured.

1. Edit the Elasticsearch 5's `elasticsearch.yml` configuration file.
    1. Add the `reindex.remote.whitelist: 10.0.0.9:9200` setting with a value that contains the IP address or hostname of the 1.x server. This allows you to reindex the 1.x data into your new 5.x instance.
    2. Temporarily comment out (with a leading `#`) the following line: `#action.auto_create_index: .security,.monitoring*,.watches,.triggered_watches,.watcher-history*`
    3. Restart the elasticsearch service.
2. Open Kibana (E.G., [http://localhost:5601](http://localhost:5601)) and execute the following scripts to reindex your data.

### Create Organization Index with the correct mappings:

```json
PUT organizations-v1
{
  "settings": {
    "index.number_of_replicas": 1,
    "index.number_of_shards": 3,
    "analysis": {
      "analyzer": {
        "keyword_lowercase": {
          "type": "custom",
          "filter": [
            "lowercase"
          ],
          "tokenizer": "keyword"
        }
      }
    }
  },
  "aliases": {
    "organizations": {}
  },
  "mappings": {
    "organization": {
      "dynamic": false,
      "properties": {
        "id": {
          "type": "keyword"
        },
        "created_utc": {
          "type": "date"
        },
        "updated_utc": {
          "type": "date"
        },
        "name": {
          "type": "text",
          "fields": {
            "keyword": {
              "type": "keyword",
              "ignore_above": 256
            }
          }
        },
        "stripe_customer_id": {
          "type": "keyword"
        },
        "has_premium_features": {
          "type": "boolean"
        },
        "plan_id": {
          "type": "keyword"
        },
        "plan_name": {
          "type": "keyword",
          "ignore_above": 256
        },
        "subscribe_date": {
          "type": "date"
        },
        "billing_status": {
          "type": "float"
        },
        "billing_price": {
          "type": "double"
        },
        "is_suspended": {
          "type": "boolean"
        },
        "retention_days": {
          "type": "integer"
        },
        "invites": {
          "type": "object",
          "properties": {
            "token": {
              "type": "keyword"
            },
            "email_address": {
              "type": "text",
              "analyzer": "keyword_lowercase"
            }
          }
        },
        "usage": {
          "type": "object",
          "properties": {
            "date": {
              "type": "date"
            },
            "total": {
              "type": "float"
            },
            "blocked": {
              "type": "float"
            },
            "limit": {
              "type": "float"
            },
            "too_big": {
              "type": "float"
            }
          }
        },
        "overage_hours": {
          "type": "object",
          "properties": {
            "date": {
              "type": "date"
            },
            "total": {
              "type": "float"
            },
            "blocked": {
              "type": "float"
            },
            "limit": {
              "type": "float"
            },
            "too_big": {
              "type": "float"
            }
          }
        }
      }
    },
    "project": {
      "dynamic": false,
      "properties": {
        "id": {
          "type": "keyword"
        },
        "created_utc": {
          "type": "date"
        },
        "updated_utc": {
          "type": "date"
        },
        "organization_id": {
          "type": "keyword"
        },
        "name": {
          "type": "text",
          "fields": {
            "keyword": {
              "type": "keyword",
              "ignore_above": 256
            }
          }
        },
        "next_summary_end_of_day_ticks": {
          "type": "long"
        }
      }
    },
    "token": {
      "dynamic": false,
      "properties": {
        "id": {
          "type": "keyword"
        },
        "created_utc": {
          "type": "date"
        },
        "updated_utc": {
          "type": "date"
        },
        "expires_utc": {
          "type": "date"
        },
        "organization_id": {
          "type": "keyword"
        },
        "project_id": {
          "type": "keyword"
        },
        "default_project_id": {
          "type": "keyword"
        },
        "user_id": {
          "type": "keyword"
        },
        "refresh": {
          "type": "keyword"
        },
        "scopes": {
          "type": "keyword"
        },
        "type": {
          "type": "byte"
        }
      }
    },
    "user": {
      "dynamic": false,
      "properties": {
        "id": {
          "type": "keyword"
        },
        "created_utc": {
          "type": "date"
        },
        "updated_utc": {
          "type": "date"
        },
        "organization_ids": {
          "type": "keyword"
        },
        "full_name": {
          "type": "text",
          "fields": {
            "keyword": {
              "type": "keyword",
              "ignore_above": 256
            }
          }
        },
        "email_address": {
          "type": "text",
          "fields": {
            "keyword": {
              "type": "keyword",
              "ignore_above": 256
            }
          },
          "analyzer": "keyword_lowercase"
        },
        "verify_email_address_token": {
          "type": "keyword"
        },
        "password_reset_token": {
          "type": "keyword"
        },
        "roles": {
          "type": "keyword"
        },
        "o_auth_accounts": {
          "type": "object",
          "properties": {
            "provider": {
              "type": "keyword"
            },
            "provider_user_id": {
              "type": "keyword"
            },
            "username": {
              "type": "keyword"
            }
          }
        }
      }
    },
    "webhook": {
      "dynamic": false,
      "properties": {
        "id": {
          "type": "keyword"
        },
        "created_utc": {
          "type": "date"
        },
        "organization_id": {
          "type": "keyword"
        },
        "project_id": {
          "type": "keyword"
        },
        "url": {
          "type": "keyword"
        },
        "event_types": {
          "type": "keyword"
        }
      }
    }
  }
}
```

### Reindex Organization data from 1.x into 5.x **Please make sure you update the host name**:

```json
POST _reindex
{
  "source": {
    "remote": {
      "host": "http://10.0.0.9:9200"
    },
    "index": "organizations-v1"
  },
  "dest": {
    "index": "organizations-v1",
    "op_type": "create"
  },
  "script": {
    "inline": "if (ctx._source.modified_utc != null) { ctx._source.updated_utc = ctx._source.remove('modified_utc'); }",
    "lang": "painless"
  }
}
```

### Create Stack Index with the correct mappings:

```json
PUT stacks-v1
{
  "settings": {
    "index.number_of_replicas": 1,
    "index.number_of_shards": 3
  },
  "aliases": {
    "stacks": {}
  },
  "mappings": {
    "stacks": {
      "include_in_all": false,
      "dynamic": false,
      "properties": {
        "id": {
          "type": "keyword"
        },
        "organization_id": {
          "type": "keyword"
        },
        "project_id": {
          "type": "keyword"
        },
        "signature_hash": {
          "type": "keyword"
        },
        "type": {
          "type": "keyword"
        },
        "first_occurrence": {
          "type": "date"
        },
        "last_occurrence": {
          "type": "date"
        },
        "title": {
          "type": "text",
          "boost": 1.1,
          "include_in_all": true
        },
        "description": {
          "type": "text",
          "include_in_all": true
        },
        "tags": {
          "type": "text",
          "fields": {
            "keyword": {
              "type": "keyword",
              "ignore_above": 256
            }
          },
          "boost": 1.2,
          "include_in_all": true
        },
        "references": {
          "type": "text",
          "include_in_all": true
        },
        "date_fixed": {
          "type": "date"
        },
        "fixed": {
          "type": "boolean"
        },
        "fixed_in_version": {
          "type": "keyword"
        },
        "is_hidden": {
          "type": "boolean"
        },
        "is_regressed": {
          "type": "boolean"
        },
        "occurrences_are_critical": {
          "type": "boolean"
        },
        "total_occurrences": {
          "type": "integer"
        }
      }
    }
  }
}
```

### Reindex Stack data from 1.x into 5.x **Please make sure you update the host name**:

```json
POST _reindex
{
  "source": {
    "remote": {
      "host": "http://10.0.0.9:9200"
    },
    "index": "stacks-v1"
  },
  "dest": {
    "index": "stacks-v1",
    "op_type": "create"
  }
}
```

### Create Event Template so daily indexes can be created with the correct mappings:

```json
PUT _template/events-v1
{
  "template": "events-v1-*",
  "settings": {
    "number_of_shards": 1,
    "number_of_replicas": 1,
    "analysis": {
      "analyzer": {
        "comma_whitespace": {
          "type": "pattern",
          "pattern": "[,\\s]+"
        },
        "email": {
          "type": "custom",
          "filter": [
            "email",
            "lowercase",
            "unique"
          ],
          "tokenizer": "keyword"
        },
        "version_index": {
          "type": "custom",
          "filter": [
            "version_pad1",
            "version_pad2",
            "version_pad3",
            "version_pad4",
            "version",
            "lowercase",
            "unique"
          ],
          "tokenizer": "whitespace"
        },
        "version_search": {
          "type": "custom",
          "filter": [
            "version_pad1",
            "version_pad2",
            "version_pad3",
            "version_pad4",
            "lowercase"
          ],
          "tokenizer": "whitespace"
        },
        "whitespace_lower": {
          "type": "custom",
          "filter": [
            "lowercase"
          ],
          "tokenizer": "whitespace"
        },
        "typename": {
          "type": "custom",
          "filter": [
            "typename",
            "lowercase",
            "unique"
          ],
          "tokenizer": "typename_hierarchy"
        },
        "standardplus": {
          "type": "custom",
          "filter": [
            "standard",
            "typename",
            "lowercase",
            "stop",
            "unique"
          ],
          "tokenizer": "comma_whitespace"
        }
      },
      "filter": {
        "email": {
          "type": "pattern_capture",
          "patterns": [
            "(\\w+)",
            "(\\p{L}+)",
            "(\\d+)",
            "(.+)@",
            "@(.+)"
          ]
        },
        "typename": {
          "type": "pattern_capture",
          "patterns": [
            "\\.(\\w+)",
            "([^\\()]+)"
          ]
        },
        "version": {
          "type": "pattern_capture",
          "patterns": [
            "^(\\d+)\\.",
            "^(\\d+\\.\\d+)",
            "^(\\d+\\.\\d+\\.\\d+)"
          ]
        },
        "version_pad1": {
          "type": "pattern_replace",
          "pattern": "(\\.|^)(\\d{1})(?=\\.|-|$)",
          "replacement": "$10000$2"
        },
        "version_pad2": {
          "type": "pattern_replace",
          "pattern": "(\\.|^)(\\d{2})(?=\\.|-|$)",
          "replacement": "$1000$2"
        },
        "version_pad3": {
          "type": "pattern_replace",
          "pattern": "(\\.|^)(\\d{3})(?=\\.|-|$)",
          "replacement": "$100$2"
        },
        "version_pad4": {
          "type": "pattern_replace",
          "pattern": "(\\.|^)(\\d{4})(?=\\.|-|$)",
          "replacement": "$10$2"
        }
      },
      "tokenizer": {
        "comma_whitespace": {
          "type": "pattern",
          "pattern": "[,\\s]+"
        },
        "typename_hierarchy": {
          "type": "path_hierarchy",
          "delimiter": "."
        }
      }
    }
  },
  "mappings": {
    "events": {
        "dynamic": "false",
        "include_in_all": false,
        "_all": {
          "analyzer": "standardplus",
          "search_analyzer": "whitespace_lower"
        },
        "_size": {
          "enabled": true
        },
        "dynamic_templates": [
          {
            "idx_reference": {
              "match": "*-r",
              "mapping": {
                "ignore_above": 256,
                "type": "keyword"
              }
            }
          }
        ],
        "properties": {
          "count": {
            "type": "integer"
          },
          "created_utc": {
            "type": "date"
          },
          "data": {
            "properties": {
              "@environment": {
                "properties": {
                  "architecture": {
                    "type": "keyword"
                  },
                  "ip_address": {
                    "type": "text",
                    "index": false,
                    "copy_to": [
                      "ip"
                    ],
                    "include_in_all": true
                  },
                  "machine_name": {
                    "type": "text",
                    "boost": 1.1,
                    "fields": {
                      "keyword": {
                        "type": "keyword",
                        "ignore_above": 256
                      }
                    },
                    "include_in_all": true
                  },
                  "o_s_name": {
                    "type": "text",
                    "copy_to": [
                      "os"
                    ]
                  }
                }
              },
              "@error": {
                "properties": {
                  "data": {
                    "properties": {
                      "@target": {
                        "properties": {
                          "ExceptionType": {
                            "type": "text",
                            "index": false,
                            "copy_to": [
                              "error.targettype"
                            ],
                            "include_in_all": true
                          },
                          "Method": {
                            "type": "text",
                            "index": false,
                            "copy_to": [
                              "error.targetmethod"
                            ],
                            "include_in_all": true
                          }
                        }
                      }
                    }
                  }
                }
              },
              "@level": {
                "type": "text",
                "fields": {
                  "keyword": {
                    "type": "keyword",
                    "ignore_above": 256
                  }
                }
              },
              "@location": {
                "properties": {
                  "country": {
                    "type": "keyword"
                  },
                  "level1": {
                    "type": "keyword"
                  },
                  "level2": {
                    "type": "keyword"
                  },
                  "locality": {
                    "type": "keyword"
                  }
                }
              },
              "@request": {
                "properties": {
                  "client_ip_address": {
                    "type": "text",
                    "index": false,
                    "copy_to": [
                      "ip"
                    ],
                    "include_in_all": true
                  },
                  "data": {
                    "properties": {
                      "@browser": {
                        "type": "text",
                        "fields": {
                          "keyword": {
                            "type": "keyword",
                            "ignore_above": 256
                          }
                        }
                      },
                      "@browser_major_version": {
                        "type": "text"
                      },
                      "@browser_version": {
                        "type": "text",
                        "fields": {
                          "keyword": {
                            "type": "keyword",
                            "ignore_above": 256
                          }
                        }
                      },
                      "@device": {
                        "type": "text",
                        "fields": {
                          "keyword": {
                            "type": "keyword",
                            "ignore_above": 256
                          }
                        }
                      },
                      "@is_bot": {
                        "type": "boolean"
                      },
                      "@os": {
                        "type": "text",
                        "index": false,
                        "copy_to": [
                          "os"
                        ]
                      },
                      "@os_major_version": {
                        "type": "text"
                      },
                      "@os_version": {
                        "type": "text",
                        "fields": {
                          "keyword": {
                            "type": "keyword",
                            "ignore_above": 256
                          }
                        }
                      }
                    }
                  },
                  "path": {
                    "type": "text",
                    "fields": {
                      "keyword": {
                        "type": "keyword",
                        "ignore_above": 256
                      }
                    },
                    "include_in_all": true
                  },
                  "user_agent": {
                    "type": "text",
                    "fields": {
                      "keyword": {
                        "type": "keyword",
                        "ignore_above": 256
                      }
                    }
                  }
                }
              },
              "@simple_error": {
                "properties": {
                  "data": {
                    "properties": {
                      "@target": {
                        "properties": {
                          "ExceptionType": {
                            "type": "text",
                            "index": false,
                            "copy_to": [
                              "error.targettype"
                            ],
                            "include_in_all": true
                          }
                        }
                      }
                    }
                  }
                }
              },
              "@submission_method": {
                "type": "text",
                "fields": {
                  "keyword": {
                    "type": "keyword",
                    "ignore_above": 256
                  }
                }
              },
              "@user": {
                "properties": {
                  "identity": {
                    "type": "text",
                    "boost": 1.1,
                    "fields": {
                      "keyword": {
                        "type": "keyword",
                        "ignore_above": 256
                      }
                    },
                    "analyzer": "email",
                    "search_analyzer": "whitespace_lower",
                    "include_in_all": true
                  },
                  "name": {
                    "type": "text",
                    "fields": {
                      "keyword": {
                        "type": "keyword",
                        "ignore_above": 256
                      }
                    },
                    "include_in_all": true
                  }
                }
              },
              "@user_description": {
                "properties": {
                  "description": {
                    "type": "text",
                    "include_in_all": true
                  },
                  "email_address": {
                    "type": "text",
                    "boost": 1.1,
                    "fields": {
                      "keyword": {
                        "type": "keyword",
                        "ignore_above": 256
                      }
                    },
                    "analyzer": "email",
                    "search_analyzer": "simple",
                    "include_in_all": true
                  }
                }
              },
              "@version": {
                "type": "text",
                "fields": {
                  "keyword": {
                    "type": "keyword",
                    "ignore_above": 256
                  }
                },
                "analyzer": "version_index",
                "search_analyzer": "version_search"
              }
            }
          },
          "date": {
            "type": "date"
          },
          "error": {
            "include_in_all": true,
            "properties": {
              "code": {
                "type": "keyword",
                "boost": 1.1
              },
              "message": {
                "type": "text",
                "fields": {
                  "keyword": {
                    "type": "keyword",
                    "ignore_above": 256
                  }
                }
              },
              "targetmethod": {
                "type": "text",
                "boost": 1.2,
                "fields": {
                  "keyword": {
                    "type": "keyword",
                    "ignore_above": 256
                  }
                },
                "analyzer": "typename",
                "search_analyzer": "whitespace_lower"
              },
              "targettype": {
                "type": "text",
                "boost": 1.2,
                "fields": {
                  "keyword": {
                    "type": "keyword",
                    "ignore_above": 256
                  }
                },
                "analyzer": "typename",
                "search_analyzer": "whitespace_lower"
              },
              "type": {
                "type": "text",
                "boost": 1.1,
                "fields": {
                  "keyword": {
                    "type": "keyword",
                    "ignore_above": 256
                  }
                },
                "analyzer": "typename",
                "search_analyzer": "whitespace_lower"
              }
            }
          },
          "geo": {
            "type": "geo_point"
          },
          "id": {
            "type": "keyword",
            "include_in_all": true
          },
          "idx": {
            "type": "object",
            "dynamic": "true"
          },
          "ip": {
            "type": "text",
            "analyzer": "comma_whitespace"
          },
          "is_deleted": {
            "type": "boolean"
          },
          "is_first_occurrence": {
            "type": "boolean"
          },
          "is_fixed": {
            "type": "boolean"
          },
          "is_hidden": {
            "type": "boolean"
          },
          "message": {
            "type": "text",
            "include_in_all": true
          },
          "organization_id": {
            "type": "keyword"
          },
          "os": {
            "type": "text",
            "fields": {
              "keyword": {
                "type": "keyword",
                "ignore_above": 256
              }
            }
          },
          "project_id": {
            "type": "keyword"
          },
          "reference_id": {
            "type": "keyword"
          },
          "source": {
            "type": "text",
            "fields": {
              "keyword": {
                "type": "keyword",
                "ignore_above": 256
              }
            },
            "include_in_all": true
          },
          "stack_id": {
            "type": "keyword"
          },
          "tags": {
            "type": "text",
            "boost": 1.2,
            "fields": {
              "keyword": {
                "type": "keyword",
                "ignore_above": 256
              }
            },
            "include_in_all": true
          },
          "type": {
            "type": "keyword"
          },
          "updated_utc": {
            "type": "date"
          },
          "value": {
            "type": "double"
          }
        }
      }
  }
}
```

### Reindex Event data from 1.x into 5.x **Please make sure you update the host name**:

```json
POST _reindex
{
  "source": {
    "remote": {
      "host": "http://10.0.0.9:9200"
    },
    "index": "events-v1-*",
    "size": 200
  },
  "dest": {
    "index": "events-v1-error"
  },
  "script": {
    "lang": "painless",
    "inline": "ctx._index = 'events-v1-' + DateTimeFormatter.ofPattern('yyyy.MM.dd').format(OffsetDateTime.parse(ctx._source.date).toInstant().atZone(ZoneOffset.UTC)); if (ctx._source.updated_utc == null) { ctx._source.updated_utc = ctx._source.created_utc; } if (ctx._source.is_deleted == null) { ctx._source.is_deleted = false; } if (!ctx.containsKey('data') || !(ctx.data.containsKey('@error') || ctx.data.containsKey('@simple_error'))) return null;def types = [];def messages = [];def codes = [];def err = ctx.data.containsKey('@error') ? ctx.data['@error'] : ctx.data['@simple_error'];def curr = err;while (curr != null) { if (curr.containsKey('type'))  types.add(curr.type); if (curr.containsKey('message'))  messages.add(curr.message); if (curr.containsKey('code'))  codes.add(curr.code); curr = curr.inner;}if (ctx.error == null) ctx.error = new HashMap();ctx.error.type = types;ctx.error.message = messages;ctx.error.code = codes;"
  }
}
```

### Delete the previous Event Template:

```json
DELETE _template/events-v1
```

## Upgrading from v2 to v3

_Please note that upgrading from [v2](https://github.com/exceptionless/Exceptionless/releases/tag/v2.0.0) to [v3](https://github.com/exceptionless/Exceptionless/releases/tag/v3.0.0) requires that `Redis` is installed and configured._

1. Download and extract the [v3](https://github.com/exceptionless/Exceptionless/releases/tag/v3.0.0) release to a temp folder.
2. Update the connection strings in the `App_Data\JobRunner\Job.exe.config` config file.
   1. You'll also need to add a `Migration:MongoConnectionString` connection string for the migration jobs to run.

       ```xml
       <add name="Migration:MongoConnectionString" connectionString="mongodb://localhost/exceptionless" />
       ```

3. Open the terminal and run the following jobs to migrate data from previous major versions of Exceptionless. `Jobs.exe` can be found in the `\wwwroot\App_Data\JobRunner\` folder.

```powershell
Job.exe -t "Exceptionless.EventMigration.OrganizationMigrationJob, Exceptionless.EventMigration" -s "Exceptionless.Core.Jobs.JobBootstrapper, Exceptionless.Core"
```

## Upgrading from v1 to v3

_Please note that upgrading from v1 to [v3](https://github.com/exceptionless/Exceptionless/releases/tag/v3.0.0) requires that `Redis` is installed and configured._

1. Download and extract the [v3](https://github.com/exceptionless/Exceptionless/releases/tag/v3.0.0) release to a temp folder.
2. Update the connection strings in the `App_Data\JobRunner\Job.exe.config` config file.
   1. You'll also need to add a `Migration:MongoConnectionString` connection string for the migration jobs to run.

       ```xml
       <add name="Migration:MongoConnectionString" connectionString="mongodb://localhost/exceptionless" />
       ```

3. Open the terminal and run the following jobs to migrate data from previous major versions of Exceptionless. `Jobs.exe` can be found in the `\wwwroot\App_Data\JobRunner\` folder.

```powershell
Job.exe -t "Exceptionless.EventMigration.StackMigrationJob, Exceptionless.EventMigration" -s "Exceptionless.Core.Jobs.JobBootstrapper, Exceptionless.Core"
Job.exe -t "Exceptionless.EventMigration.QueueEventMigrationsJob, Exceptionless.EventMigration" -s "Exceptionless.Core.Jobs.JobBootstrapper, Exceptionless.Core"
Job.exe -t "Exceptionless.EventMigration.EventMigrationJob, Exceptionless.EventMigration" -c -s "Exceptionless.Core.Jobs.JobBootstrapper, Exceptionless.Core"
Job.exe -t "Exceptionless.EventMigration.OrganizationMigrationJob, Exceptionless.EventMigration" -s "Exceptionless.Core.Jobs.JobBootstrapper, Exceptionless.Core"
```
