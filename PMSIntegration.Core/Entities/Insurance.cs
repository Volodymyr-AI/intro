namespace PMSIntegration.Core.Entities;

/// <summary>
/// Generic insurance information that applies to all PMS systems
/// Needed for billing
/// </summary>
public class Insurance
{
    /// <summary>
    /// Patient ID in our system
    /// </summary>
    public int PatientId { get; set; }
    
    /// <summary>
    /// Insurance company name (e.g., "Blue Cross Blue Shield", "Aetna")
    /// Required for identifying where to send claims
    /// </summary>
    public string CarrierName { get; set; }
    
    /// <summary>
    /// Policy/Member ID number on the insurance card
    /// Required for claim submission
    /// </summary>
    public string PolicyNumber { get; set; }

    /// <summary>
    /// Group number for employer-based plans
    /// Required for group insurance claims
    /// </summary>
    public string GroupNumber { get; set; }
    
    /// <summary>
    /// Policyholder's full name (may differ from patient)
    /// Required when patient is dependent
    /// </summary>
    public string PolicyholderName { get; set; }
    
    /// <summary>
    /// Patient's relationship to policyholder: "Self", "Spouse", "Child", "Other"
    /// Required for dependent claims
    /// </summary>
    public string Relationship { get; set; }
    
    /// <summary>
    /// Insurance priority: "Primary" or "Secondary"
    /// Determines which insurance to bill first ( In OD - Ordinal field )
    /// </summary>
    public string Priority { get; set; }
    
    public Insurance() { }

    /// <summary>
    /// Create insurance with required fields for billing
    /// </summary>
    public static Insurance Create(
        int patientId, string carrierName,
        string policyNumber, string groupNumber,
        string policyholderName, string relationship, string priority)
    {
        return new Insurance
        {
            PatientId = patientId,
            CarrierName = carrierName,
            PolicyNumber = policyNumber,
            GroupNumber = groupNumber,
            PolicyholderName = policyholderName,
            Relationship = relationship,
            Priority = priority
        };
    }
}