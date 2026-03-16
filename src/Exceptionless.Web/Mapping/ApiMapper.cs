using Exceptionless.Core.Models;
using Exceptionless.Web.Models;

namespace Exceptionless.Web.Mapping;

/// <summary>
/// Facade for all API type mappers. Delegates to type-specific mappers.
/// Uses compile-time source generation for type-safe, performant mappings.
/// </summary>
public class ApiMapper
{
    private readonly OrganizationMapper _organizationMapper;
    private readonly ProjectMapper _projectMapper;
    private readonly TokenMapper _tokenMapper;
    private readonly UserMapper _userMapper;
    private readonly WebHookMapper _webHookMapper;
    private readonly InvoiceMapper _invoiceMapper;

    public ApiMapper(TimeProvider timeProvider)
    {
        _organizationMapper = new OrganizationMapper(timeProvider);
        _projectMapper = new ProjectMapper();
        _tokenMapper = new TokenMapper();
        _userMapper = new UserMapper();
        _webHookMapper = new WebHookMapper();
        _invoiceMapper = new InvoiceMapper();
    }

    // Organization mappings
    public Organization MapToOrganization(NewOrganization source)
        => _organizationMapper.MapToOrganization(source);

    public ViewOrganization MapToViewOrganization(Organization source)
        => _organizationMapper.MapToViewOrganization(source);

    public List<ViewOrganization> MapToViewOrganizations(IEnumerable<Organization> source)
        => _organizationMapper.MapToViewOrganizations(source);

    // Project mappings
    public Project MapToProject(NewProject source)
        => _projectMapper.MapToProject(source);

    public ViewProject MapToViewProject(Project source)
        => _projectMapper.MapToViewProject(source);

    public List<ViewProject> MapToViewProjects(IEnumerable<Project> source)
        => _projectMapper.MapToViewProjects(source);

    // Token mappings
    public Token MapToToken(NewToken source)
        => _tokenMapper.MapToToken(source);

    public ViewToken MapToViewToken(Token source)
        => _tokenMapper.MapToViewToken(source);

    public List<ViewToken> MapToViewTokens(IEnumerable<Token> source)
        => _tokenMapper.MapToViewTokens(source);

    // User mappings
    public ViewUser MapToViewUser(User source)
        => _userMapper.MapToViewUser(source);

    public List<ViewUser> MapToViewUsers(IEnumerable<User> source)
        => _userMapper.MapToViewUsers(source);

    // WebHook mappings
    public WebHook MapToWebHook(NewWebHook source)
        => _webHookMapper.MapToWebHook(source);

    // Invoice mappings
    public InvoiceGridModel MapToInvoiceGridModel(Stripe.Invoice source)
        => _invoiceMapper.MapToInvoiceGridModel(source);

    public List<InvoiceGridModel> MapToInvoiceGridModels(IEnumerable<Stripe.Invoice> source)
        => _invoiceMapper.MapToInvoiceGridModels(source);
}
