![Foundatio](https://raw.githubusercontent.com/FoundatioFx/Foundatio/master/media/foundatio-dark-bg.svg#gh-dark-mode-only "Foundatio")![Foundatio](https://raw.githubusercontent.com/FoundatioFx/Foundatio/master/media/foundatio.svg#gh-light-mode-only "Foundatio")

[![Build status](https://github.com/FoundatioFx/Foundatio.RabbitMQ/workflows/Build/badge.svg)](https://github.com/FoundatioFx/Foundatio.RabbitMQ/actions)
[![NuGet Version](http://img.shields.io/nuget/v/Foundatio.RabbitMQ.svg?style=flat)](https://www.nuget.org/packages/Foundatio.RabbitMQ/)
[![feedz.io](https://img.shields.io/badge/endpoint.svg?url=https%3A%2F%2Ff.feedz.io%2Ffoundatio%2Ffoundatio%2Fshield%2FFoundatio.RabbitMQ%2Flatest)](https://f.feedz.io/foundatio/foundatio/packages/Foundatio.RabbitMQ/latest/download)
[![Discord](https://img.shields.io/discord/715744504891703319)](https://discord.gg/6HxgFCx)

Pluggable foundation blocks for building loosely coupled distributed apps.

## ✨ Why Choose Foundatio?

- 🔌 **Pluggable implementations** - Swap Redis, Azure, AWS, or in-memory with no code changes
- 🧪 **Developer friendly** - In-memory implementations for fast local development and testing
- 💉 **DI native** - Built for Microsoft.Extensions.DependencyInjection
- 🎯 **Interface-first** - Code against abstractions, not implementations
- ⚡ **Production ready** - Battle-tested in high-scale applications
- 🔄 **Consistent APIs** - Same patterns across caching, queues, storage, and more

## 🧱 Core Building Blocks

| Feature | Description |
|---------|-------------|
| [**Caching**](https://foundatio.dev/guide/caching) | In-memory, Redis, and hybrid caching with automatic invalidation |
| [**Queues**](https://foundatio.dev/guide/queues) | Reliable message queuing with Redis, Azure, AWS SQS |
| [**Locks**](https://foundatio.dev/guide/locks) | Distributed locking and throttling |
| [**Messaging**](https://foundatio.dev/guide/messaging) | Pub/sub with Redis, RabbitMQ, Kafka, Azure Service Bus |
| [**Jobs**](https://foundatio.dev/guide/jobs) | Background job processing with queue integration |
| [**File Storage**](https://foundatio.dev/guide/storage) | Unified file API for disk, S3, Azure Blob, and more |
| [**Resilience**](https://foundatio.dev/guide/resilience) | Retry policies, circuit breakers, and timeouts |

## 🚀 Quick Start

```bash
dotnet add package Foundatio.RabbitMQ
```

```csharp
// Messaging
IMessageBus messageBus = new RabbitMQMessageBus(o => o
    .ConnectionString("amqp://localhost"));
await messageBus.PublishAsync(new MyMessage { Data = "Hello" });
```

## 📦 Provider Implementations

| Provider | Caching | Queues | Messaging | Storage | Locks |
|----------|---------|--------|-----------|---------|-------|
| [In-Memory](https://foundatio.dev/guide/implementations/in-memory) | ✅ | ✅ | ✅ | ✅ | ✅ |
| [Redis](https://github.com/FoundatioFx/Foundatio.Redis) | ✅ | ✅ | ✅ | ✅ | ✅ |
| [Azure Storage](https://github.com/FoundatioFx/Foundatio.AzureStorage) | | ✅ | | ✅ | |
| [Azure Service Bus](https://github.com/FoundatioFx/Foundatio.AzureServiceBus) | | ✅ | ✅ | | |
| [AWS (S3/SQS/SNS)](https://github.com/FoundatioFx/Foundatio.AWS) | | ✅ | ✅ | ✅ | |
| [RabbitMQ](https://github.com/FoundatioFx/Foundatio.RabbitMQ) | | | ✅ | | |
| [Kafka](https://github.com/FoundatioFx/Foundatio.Kafka) | | | ✅ | | |
| [Minio](https://github.com/FoundatioFx/Foundatio.Minio) | | | | ✅ | |
| [Aliyun](https://github.com/FoundatioFx/Foundatio.Aliyun) | | | | ✅ | |
| [SFTP](https://github.com/FoundatioFx/Foundatio.Storage.SshNet) | | | | ✅ | |

## 📚 Learn More

**👉 [Complete Documentation](https://foundatio.dev)**

### Delayed Message Delivery

Foundatio.RabbitMQ supports delayed message delivery via the `DeliveryDelay` option on `PublishAsync`.

**Current behavior:**

1. **RabbitMQ < 4.3 with plugin installed**: If the [`rabbitmq_delayed_message_exchange`](https://github.com/rabbitmq/rabbitmq-delayed-message-exchange/) plugin is detected, it is used for delayed delivery. A warning is logged because the plugin is deprecated and will not work on RabbitMQ 4.3+.
2. **RabbitMQ < 4.3 without plugin**: Falls back to an in-memory delay scheduler provided by `MessageBusBase`. Messages are held in process memory and delivered after the delay. **This is not durable** -- delayed messages are lost if the process restarts.
3. **When the RabbitMQ server version is detected as >= 4.3**: The plugin probe is skipped (the plugin depends on Mnesia, which was removed in 4.3), and the in-memory fallback is used automatically. If the server version cannot be determined from `ServerProperties["version"]`, the probe may still be attempted before falling back.

**Migration guidance:**

The `rabbitmq_delayed_message_exchange` plugin is [archived and no longer maintained](https://github.com/rabbitmq/rabbitmq-delayed-message-exchange/). RabbitMQ 4.3 removes Mnesia, making the plugin incompatible. If you rely on delayed messages:

- On RabbitMQ < 4.3: The plugin still works but logs a deprecation warning at startup.
- On RabbitMQ >= 4.3: Delayed messages use the in-memory fallback automatically. Be aware that these are not durable across process restarts.
- For durable delayed delivery on RabbitMQ 4.3+, consider implementing TTL + Dead-Letter Exchange patterns or using an external scheduler.

### Core Features

- [Getting Started](https://foundatio.dev/guide/getting-started) - Installation and setup
- [Caching](https://foundatio.dev/guide/caching) - In-memory, Redis, and hybrid caching with invalidation
- [Queues](https://foundatio.dev/guide/queues) - FIFO message delivery with lock renewal and retry policies
- [Locks](https://foundatio.dev/guide/locks) - Distributed locking with null handling patterns
- [Messaging](https://foundatio.dev/guide/messaging) - Pub/sub with size limits and notification patterns
- [File Storage](https://foundatio.dev/guide/storage) - Unified file API across providers
- [Jobs](https://foundatio.dev/guide/jobs) - Background job processing and hosted service integration

### Advanced Topics

- [Resilience](https://foundatio.dev/guide/resilience) - Retry policies, circuit breakers, and timeouts
- [Serialization](https://foundatio.dev/guide/serialization) - Serializer configuration and performance
- [Dependency Injection](https://foundatio.dev/guide/dependency-injection) - DI setup and patterns
- [Configuration](https://foundatio.dev/guide/configuration) - Options and settings

## 📦 CI Packages (Feedz)

Want the latest CI build before it hits NuGet? Add the Feedz source and install the pre-release version:

```bash
dotnet nuget add source https://f.feedz.io/foundatio/foundatio/nuget -n foundatio-feedz
dotnet add package Foundatio.RabbitMQ --prerelease
```

Or add to your `NuGet.config`:

```xml
<configuration>
  <packageSources>
    <add key="foundatio-feedz" value="https://f.feedz.io/foundatio/foundatio/nuget" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="foundatio-feedz">
      <package pattern="Foundatio.*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request. See our [documentation](https://foundatio.dev) for development guidelines.

**Development Setup:**

1. Clone the repository
2. Open `Foundatio.RabbitMQ.slnx` in Visual Studio or VS Code
3. Run `dotnet build` to build
4. Run `dotnet test` to run tests

## 📄 License

Apache 2.0 License

## Thanks to all the people who have contributed

[![contributors](https://contributors-img.web.app/image?repo=foundatiofx/Foundatio.RabbitMQ)](https://github.com/foundatiofx/Foundatio.RabbitMQ/graphs/contributors)
