#!/bin/bash

scriptdir=$(cd "$(dirname "$0")" && pwd -P)
extradefs="$@"
machine=$(uname -m)

if [[ "$OSTYPE" == "darwin"* ]]; then
  libname=libBolt.dylib

  if [[ "$machine" == "arm64" ]]; then
    gccflags="-arch arm64 -Wno-pointer-sign -D_DARWIN_C_SOURCE=1"
  else
    gccflags="-arch x86_64 -Wno-pointer-sign -D_DARWIN_C_SOURCE=1"
  fi
else
  libname=libBolt.so
  gccflags=""
fi

binsubdir=netcoreapp3.0

pushd "$scriptdir/.." || exit 1
gcc -g -fPIC -shared -DNDEBUG=1 $gccflags -o $libname bolt.c $extradefs || exit 1
mkdir -p ../../../../../../bin/Release$CONFIGURATION_SUFFIX/bin/$binsubdir || exit 1
mv $libname ../../../../../../bin/Release$CONFIGURATION_SUFFIX/bin/$binsubdir/$libname || exit 1

if [[ "$OSTYPE" == "darwin"* ]]; then
  rm -rf $libname.dSYM || exit 1
fi

popd || exit 1
