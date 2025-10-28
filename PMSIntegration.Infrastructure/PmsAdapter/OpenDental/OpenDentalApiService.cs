using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PMSIntegration.Core.Entities;
using PMSIntegration.Core.Interfaces;
using PMSIntegration.Infrastructure.PmsAdapter.OpenDental.DTOs;

namespace PMSIntegration.Infrastructure.PmsAdapter.OpenDental;

public class OpenDentalApiService : IPmsApiService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenDentalApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly OpenDentalConfiguration _configuration;
    
    public OpenDentalApiService(
        OpenDentalConfiguration configuration,
        ILogger<OpenDentalApiService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_configuration.ApiBaseUrl),
            Timeout = TimeSpan.FromSeconds(_configuration.TimeoutSeconds)
        };
        
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue(_configuration.AuthScheme, _configuration.AuthToken);
        
        _jsonOptions = new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        };
    }
    
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            _logger.LogDebug("Checking OpenDental API availability");
            
            // Simple health check - try to get 1 patient
            var response = await _httpClient.GetAsync("/api/v1/patients/Simple?limit=1");
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("OpenDental API is available");
                return true;
            }
            
            _logger.LogWarning($"OpenDental API returned status: {response.StatusCode}");
            
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogError("OpenDental API authentication failed. Check AuthToken in інтсConfiguration.");
            }
            
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error connecting to OpenDental API");
            return false;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "OpenDental API request timed out");
            return false;
        }
    }
    
    public async Task<List<Patient>> GetPatientsAsync(DateTime? since = null)
    {
        try
        {
            var dateFilter = since ?? DateTime.Now.AddYears(-20);
            var dateStamp = dateFilter.ToString("yyyy-MM-dd HH:mm:ss");
            
            _logger.LogInformation($"Fetching patients from OpenDental since {dateStamp}");
            
            var requestUrl = $"/api/v1/patients/Simple?DateTStamp={Uri.EscapeDataString(dateStamp)}";
            var response = await _httpClient.GetAsync(requestUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError($"OpenDental API error: {response.StatusCode} - {error}");
                return new List<Patient>();
            }
            
            var content = await response.Content.ReadAsStringAsync();
            var dtos = JsonSerializer.Deserialize<List<OpenDentalPatientDto>>(content, _jsonOptions);
            
            if (dtos == null)
            {
                _logger.LogWarning("Failed to deserialize patient data from OpenDental");
                return new List<Patient>();
            }
            
            var patients = dtos.Select(dto => dto.ToPatient()).ToList();
            _logger.LogInformation($"Successfully fetched {patients.Count} patients from OpenDental");
            
            return patients;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching patients from OpenDental");
            return new List<Patient>();
        }
    }
    
    public async Task<List<Insurance>> GetPatientInsuranceAsync(int patientId)
    {
        try
        {
            var requestUrl = $"/api/v1/familymodules/{patientId}/Insurance";
            var response = await _httpClient.GetAsync(requestUrl);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogDebug($"No insurance found for patient {patientId}");
                    return new List<Insurance>();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(new Exception($"API call failed: {response.StatusCode}"),
                    $"OpenDental Insurance API error: {errorContent}");
                return new List<Insurance>();
            }

            var content = await response.Content.ReadAsStringAsync();
            var dtos = JsonSerializer.Deserialize<List<OpenDentalInsuranceDto>>(content, _jsonOptions);

            if (dtos == null)
            {
                _logger.LogWarning("Failed to deserialize insurance data from OpenDental");
                return new List<Insurance>();
            }

            var insurances = dtos.Select(dto => dto.ToInsurance()).ToList();

            return insurances;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching patients from OpenDental");
            return new List<Insurance>();
        }
    }
    
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}