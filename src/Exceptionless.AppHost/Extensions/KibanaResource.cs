namespace Aspire.Hosting;

/// <summary>
/// A resource that represents a Kibana container.
/// </summary>
/// <param name="name">The name of the resource.</param>
public class KibanaResource(string name) : ContainerResource(name)
{
}
