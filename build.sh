#!/bin/bash
dotnet run --project ./CakeBuild/CakeBuild.csproj -- "$@"

# copy .zip file to modfolder if set
if [[ ! -f .env ]]; then
    exit 1
fi

set -o allexport
source .env
set +o allexport


if [[ -z "VINTAGE_STORY_MOD_FOLDER" ]]; then
  echo "VINTAGE_STORY_MOD_FOLDER not set"
  exit 1
fi

ZIP_FILE=$(ls -t ./Releases/*.zip 2>/dev/null | head -n 1)

if [[ -z "$ZIP_FILE" ]]; then
  echo ".zip-File not found"
  exit 1
fi

cp "$ZIP_FILE" "$VINTAGE_STORY_MOD_FOLDER" 

echo "copied $ZIP_FILE to $VINTAGE_STORY_MOD_FOLDER"
