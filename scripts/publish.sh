#!/bin/sh
set -eu
# Run from repository root. Override DOTNET with a specific SDK executable if needed.
dotnet_cmd=${DOTNET:-dotnet}
for rid in linux-arm64 linux-x64 osx-arm64 osx-x64 win-x64 linux-musl-arm64; do
  output="artifacts/release/$rid"
  rm -rf "$output"
  "$dotnet_cmd" publish HyperSploit.csproj -c Release -r "$rid" --self-contained true -p:PublishAot=false -o "$output"
  cp LICENCE "$output/LICENCE"
  cp NOTICE "$output/NOTICE"
done
output=artifacts/release/portable
rm -rf "$output"
"$dotnet_cmd" publish HyperSploit.csproj -c Release --self-contained false -p:UseAppHost=false -o "$output"
cp LICENCE "$output/LICENCE"
cp NOTICE "$output/NOTICE"
