using Exceptionless.Web.Models;

namespace Exceptionless.Web.Api.Messages;

public record SubmitContactRequest(ContactRequest Request, HttpContext Context);
