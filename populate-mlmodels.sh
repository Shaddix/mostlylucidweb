#!/bin/bash

# Populate the mlmodels Docker volume with model files
# Downloads all-MiniLM-L6-v2 ONNX model from Hugging Face

VOLUME_NAME="mostlylucidweb_mlmodels"

echo "Creating volume if it doesn't exist..."
docker volume create "$VOLUME_NAME" 2>/dev/null || true

echo "Downloading model files to volume..."
docker run --rm -v "$VOLUME_NAME:/models" alpine sh -c '
  apk add --no-cache wget
  cd /models
  wget -O all-MiniLM-L6-v2.onnx "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx"
  wget -O vocab.txt "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/vocab.txt"
'

echo "Done. Volume contents:"
docker run --rm -v "$VOLUME_NAME:/data:ro" alpine ls -la /data/
