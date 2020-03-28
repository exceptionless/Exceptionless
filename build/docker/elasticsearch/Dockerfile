# https://www.docker.elastic.co/
FROM docker.elastic.co/elasticsearch/elasticsearch:7.6.1

RUN elasticsearch-plugin install -b mapper-size
RUN elasticsearch-plugin install -b repository-azure
RUN elasticsearch-plugin install -b repository-s3

# USER root
#
# RUN echo "bootstrap.memory_lock: true" >> /usr/share/elasticsearch/config/elasticsearch.yml
# RUN echo "xpack.security.enabled: false" >> /usr/share/elasticsearch/config/elasticsearch.yml
# RUN echo "xpack.graph.enabled: false" >> /usr/share/elasticsearch/config/elasticsearch.yml
# RUN echo "xpack.watcher.enabled: false" >> /usr/share/elasticsearch/config/elasticsearch.yml
# USER elasticsearch
