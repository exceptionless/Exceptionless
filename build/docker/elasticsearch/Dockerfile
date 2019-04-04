# https://www.docker.elastic.co/
FROM docker.elastic.co/elasticsearch/elasticsearch:5.6.16

RUN elasticsearch-plugin install mapper-size
RUN elasticsearch-plugin install repository-azure
RUN elasticsearch-plugin install repository-s3

# USER root
# RUN echo "bootstrap.memory_lock: true" >> /usr/share/elasticsearch/config/elasticsearch.yml
# RUN echo "xpack.security.enabled: false" >> /usr/share/elasticsearch/config/elasticsearch.yml
# RUN echo "xpack.graph.enabled: false" >> /usr/share/elasticsearch/config/elasticsearch.yml
# RUN echo "xpack.watcher.enabled: false" >> /usr/share/elasticsearch/config/elasticsearch.yml
# USER elasticsearch