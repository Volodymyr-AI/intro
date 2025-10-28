using PMSIntegration.Core.Entities;

namespace PMSIntegration.Infrastructure.PmsAdapter.OpenDental.DTOs;

public static class OpenDentalMapper
{
    public static Patient ToPatient(this OpenDentalPatientDto dto)
    {
        return new Patient
        {
            Id = dto.PatNum,
            FirstName = dto.FName ?? "",
            LastName = dto.LName ?? "",
            Phone = GetBestPhone(dto.HmPhone, dto.WirelessPhone, dto.WkPhone),
            Email = dto.Email ?? "",
            Address = CombineAddress(dto.Address, dto.Address2),
            City = dto.City ?? "",
            State = dto.State ?? "",
            ZipCode = dto.Zip ?? "",
            DateOfBirth = ParseDate(dto.Birthdate),
            IsSynced = false,
            ReportReady = false
        };
    }

    public static Insurance ToInsurance(this OpenDentalInsuranceDto dto) 
    {     
        return new Insurance     
        {         
            PatientId = dto.PatNum,         
            CarrierName = dto.CarrierName ?? "Unknown Carrier",         
            PolicyNumber = dto.SubscriberID ?? dto.PatID ?? "",         
            GroupNumber = dto.GroupNum ?? "",         
            PolicyholderName = dto.SubscriberName ?? "Self",  // вже string
            Relationship = MapRelationship(dto.Relationship),         
            Priority = MapPriority(dto.Ordinal)
        }; 
    }  

    private static string MapRelationship(string? relationship) 
    {     
        if (string.IsNullOrWhiteSpace(relationship))         
            return "Self";          
    
        return relationship.Trim().ToLowerInvariant() switch     
        {         
            "self" => "Self",         
            "spouse" => "Spouse",         
            var r when r.Contains("child") || r.Contains("dependent") => "Child",         
            _ => "Other"     
        }; 
    }  

    private static string MapPriority(string? ordinal) 
    {     
        if (string.IsNullOrWhiteSpace(ordinal))
            return "Primary";
    
        return ordinal.Trim() switch     
        {         
            "1" => "Primary",         
            "2" => "Secondary",         
            _ => "Primary"     
        }; 
    }
    
    private static string GetBestPhone(string? home, string? wireless, string? work)
    {
        if (!string.IsNullOrWhiteSpace(wireless)) return wireless;
        if (!string.IsNullOrWhiteSpace(home)) return home;
        if (!string.IsNullOrWhiteSpace(work)) return work;
        return "";
    }
    
    private static string CombineAddress(string? address1, string? address2)
    {
        var parts = new[] { address1, address2 }
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .ToArray();
        
        return parts.Any() ? string.Join(", ", parts) : "";
    }
    
    private static DateTime ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return DateTime.MinValue;
        
        if (DateTime.TryParse(dateStr, out var date))
            return date;
        
        // Try common OpenDental date formats
        string[] formats = { "yyyy-MM-dd", "MM/dd/yyyy", "yyyy-MM-dd HH:mm:ss" };
        
        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dateStr, format, null,
                    System.Globalization.DateTimeStyles.None, out date))
            {
                return date;
            }
        }
        
        return DateTime.MinValue;
    }
}