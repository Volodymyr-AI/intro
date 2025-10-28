namespace PMSIntegration.Core.Entities;

public class Patient
{
    // Essential patient identification
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    // Contact information
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    
    // Address information (added for completeness)
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    
    // Medical information
    public DateTime DateOfBirth { get; set; }
    public bool ReportReady { get;  set; }
    
    public bool IsSynced { get; set; }

    public static Patient Create(
        string firstName, string lastName,
        string phone, string email,
        DateTime dateOfBirth)
    {
        return new Patient
        {
            FirstName = firstName,
            LastName = lastName,
            Phone = phone,
            Email = email,
            DateOfBirth = dateOfBirth,
            ReportReady = false
        };
    }
    
    public void MarkReportAsReady() => ReportReady = true;
}