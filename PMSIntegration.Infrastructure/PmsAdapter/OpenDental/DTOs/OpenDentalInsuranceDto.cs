using System.Text.Json.Serialization;
using PMSIntegration.Application.Json;

namespace PMSIntegration.Infrastructure.PmsAdapter.OpenDental.DTOs;

public class OpenDentalInsuranceDto 
{      
    [JsonPropertyName("PatNum")]     
    public int PatNum { get; set; }          
    
    [JsonPropertyName("Subscriber")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? SubscriberName { get; set; }  // string замість object
    
    [JsonPropertyName("SubscriberID")]     
    public string? SubscriberID { get; set; }          
    
    [JsonPropertyName("Ordinal")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Ordinal { get; set; }  // string замість object
    
    [JsonPropertyName("Relationship")]     
    public string? Relationship { get; set; }          
    
    [JsonPropertyName("PatID")]     
    public string? PatID { get; set; }          
    
    [JsonPropertyName("CarrierName")]     
    public string? CarrierName { get; set; }          
    
    [JsonPropertyName("GroupNum")]     
    public string? GroupNum { get; set; } 
}