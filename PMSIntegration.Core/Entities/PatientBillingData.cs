namespace PMSIntegration.Core.Entities;

public class PatientBillingData
{
    public Patient Patient { get; set; }
    public List<Insurance> Insurances { get; set; }
}