using System.Text.Json;

namespace Exceptionless.Core.Extensions {
    public static class SerializerExtensions {
        public static string SafeGetStringProperty(this JsonDocument document, string property) {
            if (document?.RootElement == null)
                return null;
            
            return document.RootElement.TryGetProperty(property, out var value) ? value.GetString() : null;
        }
    }
}
