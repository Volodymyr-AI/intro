using System.Text.Json.Serialization;

namespace PMSIntegration.Infrastructure.PmsAdapter.OpenDental.DTOs;

public class OpenDentalPatientDto
{
    [JsonPropertyName("PatNum")]
    public int PatNum { get; set; }
    
    [JsonPropertyName("FName")]
    public string? FName { get; set; }
    
    [JsonPropertyName("LName")]
    public string? LName { get; set; }
    
    [JsonPropertyName("Birthdate")]
    public string? Birthdate { get; set; }
    
    [JsonPropertyName("HmPhone")]
    public string? HmPhone { get; set; }
    
    [JsonPropertyName("WirelessPhone")]
    public string? WirelessPhone { get; set; }
    
    [JsonPropertyName("WkPhone")]
    public string? WkPhone { get; set; }
    
    [JsonPropertyName("Email")]
    public string? Email { get; set; }
    
    [JsonPropertyName("Address")]
    public string? Address { get; set; }
    
    [JsonPropertyName("Address2")]
    public string? Address2 { get; set; }
    
    [JsonPropertyName("City")]
    public string? City { get; set; }
    
    [JsonPropertyName("State")]
    public string? State { get; set; }
    
    [JsonPropertyName("Zip")]
    public string? Zip { get; set; }
    
    [JsonPropertyName("DateTStamp")]
    public string? DateTStamp { get; set; }
}