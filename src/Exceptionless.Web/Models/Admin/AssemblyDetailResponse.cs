using Exceptionless.Core.Utility;

namespace Exceptionless.Web.Models.Admin;

public record AssemblyDetailResponse(
    string? AssemblyName,
    string? AssemblyTitle,
    string? AssemblyDescription,
    string? AssemblyProduct,
    string? AssemblyCompany,
    string? AssemblyCopyright,
    string? AssemblyConfiguration,
    string? AssemblyVersion,
    string? AssemblyFileVersion,
    string? AssemblyInformationalVersion
)
{
    public static AssemblyDetailResponse FromAssemblyDetail(AssemblyDetail detail)
    {
        return new AssemblyDetailResponse(
            detail.AssemblyName,
            detail.AssemblyTitle,
            detail.AssemblyDescription,
            detail.AssemblyProduct,
            detail.AssemblyCompany,
            detail.AssemblyCopyright,
            detail.AssemblyConfiguration,
            detail.AssemblyVersion,
            detail.AssemblyFileVersion,
            detail.AssemblyInformationalVersion
        );
    }
}
