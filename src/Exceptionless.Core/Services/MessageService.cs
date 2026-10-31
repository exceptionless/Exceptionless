using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Repositories.Elasticsearch;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.Logging;

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
    private readonly AppOptions _options;
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
        _options = options;

        // Preserve the public constructor shape for existing composition roots while push
        // publication no longer depends on distributed connection state or trace logging.
        _ = connectionMapping;
        _ = loggerFactory;
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

    private Task OnBeforePublishEntityChangedAsync<T>(object sender, BeforePublishEntityChangedEventArgs<T> args)
        where T : class, IIdentity, new()
    {
        // Push routing is replica-local. Publishing must not depend on immortal distributed
        // connection indexes because another replica may own the interested client.
        args.Cancel = false;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var disposeAction in _disposeActions)
            disposeAction();

        _disposeActions.Clear();
    }
}
