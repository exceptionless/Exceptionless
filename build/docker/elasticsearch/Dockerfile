# https://www.docker.elastic.co/
FROM docker.elastic.co/elasticsearch/elasticsearch:7.7.1

RUN elasticsearch-plugin install -b mapper-size
RUN elasticsearch-plugin install -b repository-azure
RUN elasticsearch-plugin install -b repository-s3
