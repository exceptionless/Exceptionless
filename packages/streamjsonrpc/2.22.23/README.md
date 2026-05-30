# StreamJsonRpc

[![codecov](https://codecov.io/gh/Microsoft/vs-streamjsonrpc/branch/main/graph/badge.svg)](https://codecov.io/gh/Microsoft/vs-streamjsonrpc)

StreamJsonRpc is a cross-platform, .NET portable library that implements the
[JSON-RPC][JSONRPC] wire protocol.

It works over [Stream](https://docs.microsoft.com/dotnet/api/system.io.stream), [WebSocket](https://docs.microsoft.com/dotnet/api/system.net.websockets.websocket), or System.IO.Pipelines pipes, independent of the underlying transport.

Bonus features beyond the JSON-RPC spec include:

1. Request cancellation
1. .NET Events as notifications
1. Dynamic client proxy generation
1. Support for [compact binary serialization](https://github.com/microsoft/vs-streamjsonrpc/blob/main/doc/extensibility.md) via MessagePack
1. Pluggable architecture for custom message handling and formatting.

Learn about the use cases for JSON-RPC and how to use this library from our [documentation](https://github.com/microsoft/vs-streamjsonrpc/blob/main/doc/index.md).

## Compatibility

This library has been tested with and is compatible with the following other
JSON-RPC libraries:

* [json-rpc-peer][json-rpc-peer] (npm)
* [vscode-jsonrpc][vscode-jsonrpc] (npm)

[JSONRPC]: https://www.jsonrpc.org/
[json-rpc-peer]: https://www.npmjs.com/package/json-rpc-peer
[vscode-jsonrpc]: https://www.npmjs.com/package/vscode-jsonrpc
