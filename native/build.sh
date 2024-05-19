if [ "$(basename "$PWD")" != "native" ]; then
  echo "Please run this command from the native directory."
  exit 1
fi

mkdir -p ../Benchmarks/bin/Release/net8.0
mkdir -p ../Benchmarks/bin/Debug/net8.0

OS="$(uname)"
if [ "$OS" == "Linux" ]; then
  OUTPUT_FILE="libprotozero.so"
elif [ "$OS" == "Darwin" ]; then
  OUTPUT_FILE="libprotozero.dylib"
else
  echo "Unsupported operating system: $OS"
  exit 1
fi

g++ protozero_test.cpp -std=c++17 -O3 -shared -o $OUTPUT_FILE