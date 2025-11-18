namespace PMSIntegration.Core.Entities;

public class Patient
{
    // Essential patient identification
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; } 

    public string Gender { get; set; } 
    // Contact information
    public string Phone { get; set; } 
    public string Email { get; set; } 
    
    // Address information (added for completeness)
    public string Address { get; set; } 
    public string City { get; set; } 
    public string State { get; set; } 
    public string ZipCode { get; set; } 
    
    // Medical information
    public DateTime DateOfBirth { get; set; }
    
    public bool IsSynced { get; set; }

    public static Patient Create(
        string firstName, string lastName, string gender,
        string phone, string email,
        DateTime dateOfBirth)
    {
        return new Patient
        {
            FirstName = firstName,
            LastName = lastName,
            Gender = gender,
            Phone = phone,
            Email = email,
            DateOfBirth = dateOfBirth
        };
    }
}