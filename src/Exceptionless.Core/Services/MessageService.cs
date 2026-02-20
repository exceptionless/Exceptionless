using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Repositories.Elasticsearch;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Exceptionless.Core.Services;

public class MessageService : IDisposable, IStartupAction
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly IStackRepository _stackRepository;
    private readonly IEventRepository _eventRepository;
    private readonly ITokenRepository _tokenRepository;
    private readonly IWebHookRepository _webHookRepository;
    private readonly IConnectionMapping _connectionMapping;
    private readonly AppOptions _options;
    private readonly ILogger _logger;
    private readonly List<Action> _disposeActions = [];

    public MessageService(
        IOrganizationRepository organizationRepository,
        IProjectRepository projectRepository,
        IUserRepository userRepository,
        IStackRepository stackRepository,
        IEventRepository eventRepository,
        ITokenRepository tokenRepository,
        IWebHookRepository webHookRepository,
        IConnectionMapping connectionMapping,
        AppOptions options,
        ILoggerFactory loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _userRepository = userRepository;
        _stackRepository = stackRepository;
        _eventRepository = eventRepository;
        _tokenRepository = tokenRepository;
        _webHookRepository = webHookRepository;
        _connectionMapping = connectionMapping;
        _options = options;
        _logger = loggerFactory.CreateLogger<MessageService>() ?? NullLogger<MessageService>.Instance;
    }

    public Task RunAsync(CancellationToken shutdownToken = default)
    {
        if (!_options.EnableRepositoryNotifications)
            return Task.CompletedTask;

        RegisterHandler<Organization>(_organizationRepository);
        RegisterHandler<User>(_userRepository);
        RegisterHandler<Project>(_projectRepository);
        RegisterHandler<Stack>(_stackRepository);
        RegisterHandler<PersistentEvent>(_eventRepository);
        RegisterHandler<Token>(_tokenRepository);
        RegisterHandler<WebHook>(_webHookRepository);

        return Task.CompletedTask;
    }

    private void RegisterHandler<T>(object repository) where T : class, IIdentity, new()
    {
        if (repository is not ElasticRepositoryBase<T> repo)
            return;

        Func<object, BeforePublishEntityChangedEventArgs<T>, Task> handler = OnBeforePublishEntityChangedAsync;
        repo.BeforePublishEntityChanged.AddHandler(handler);
        _disposeActions.Add(() => repo.BeforePublishEntityChanged.RemoveHandler(handler));
    }

    private async Task OnBeforePublishEntityChangedAsync<T>(object sender, BeforePublishEntityChangedEventArgs<T> args)
        where T : class, IIdentity, new()
    {
        var listenerCount = await GetNumberOfListeners(args.Message);
        args.Cancel = listenerCount == 0;
        if (args.Cancel)
            _logger.LogTrace("Cancelled {EntityType} Entity Changed Message: {@Message}", typeof(T).Name, args.Message);
    }

    private Task<int> GetNumberOfListeners(EntityChanged message)
    {
        var entityChanged = ExtendedEntityChanged.Create(message, false);
        if (String.IsNullOrEmpty(entityChanged.OrganizationId))
            return Task.FromResult(1); // Return 1 as we have no idea if people are listening.

        return _connectionMapping.GetGroupConnectionCountAsync(entityChanged.OrganizationId);
    }

    public void Dispose()
    {
        foreach (var disposeAction in _disposeActions)
            disposeAction();
        _disposeActions.Clear();
    }
}
