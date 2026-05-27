using Xunit;

namespace Exceptionless.Tests;

/// <summary>
/// Collection definition for tests that assert on event queue state.
/// Tests in this collection run sequentially to prevent queue deletion races.
/// </summary>
[CollectionDefinition("EventQueue")]
public class EventQueueCollection : ICollectionFixture<AppWebHostFactory>;
