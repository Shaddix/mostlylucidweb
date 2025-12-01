# Where's the semantic search Scott?

Ok quick piece here. You've read all about semantic search, [in MANY articles here](/blog/category/Semantic%20Search). But you may have noticed; THIS SITE AIN'T ACTUALLY USING IT *YET*. So where is it?

<datetime class="hidden">2025-12-03T12:30</datetime>

Simple the way I deploy this site means I NEED downtime to change my [docker-compose.yml ](https://github.com/scottgal/mostlylucidweb/blob/main/docker-compose.yml) specifically to add

```yaml
  mostlylucid:
    image: scottgal/mostlylucid:latest
    restart: always
    labels:
      - "com.centurylinklabs.watchtower.enable=true"
    env_file:
      - .env
    environment:
...
      - SemanticSearch__Enabled=${SEMANTICSEARCH_ENABLED}
      - SemanticSearch__QdrantUrl=http://qdrant:6333
      - SemanticSearch__CollectionName=${SEMANTICSEARCH_COLLECTIONNAME}
      - SemanticSearch__EmbeddingModelPath=${SEMANTICSEARCH_EMBEDDINGMODELPATH}
      - SemanticSearch__VocabPath=${SEMANTICSEARCH_VOCABPATH}
      - SemanticSearch__VectorSize=${SEMANTICSEARCH_VECTORSIZE}
      - SemanticSearch__RelatedPostsCount=${SEMANTICSEARCH_RELATEDPOSTSCOUNT}
      - SemanticSearch__MinimumSimilarityScore=${SEMANTICSEARCH_MINIMUMSIMILARITYSCORE}
      - SemanticSearch__SearchResultsCount=${SEMANTICSEARCH_SEARCHRESULTSCOUNT}

 qdrant:
    image: qdrant/qdrant:latest
    container_name: qdrant
    restart: always
    volumes:
      - qdrant-data:/qdrant/storage
    networks:
      - app_network
    environment:
      - QDRANT__SERVICE__GRPC_PORT=6334
 ```

Which is really all I need. It IS running on my local test instance (https://local.mostlylucid.net) and I'm able to quickly troubleshoot and tweak this more easily (it's literally on a laptop next to me; old laptops make GREAT homelab machines and [Cloudflare Tunnels](https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/) make it SEAMLESS). So have a play if you like. 

It WILL arrive at some point; likely over Christmas when my traffic drops / I can't sleep one night. Until then the site is an example of graceful degradation as it works either way 🤓.
 
