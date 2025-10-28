using PMSIntegration.Core.Interfaces;

namespace PMSIntegration.Infrastructure.PmsAdapter.OpenDental;

public class OpenDentalConfiguration : ISyncConfiguration
{
    public string AuthScheme { get; set; } = "ODFHIR";
    public string AuthToken { get; set; } = "";
    public string ApiBaseUrl { get; set; } = "http://localhost:30222";
    public int TimeoutSeconds { get; set; } = 30;
    public string? ImageFolderPath { get; set; }
    
    public DateTime ExportStartDate { get; set; }
}