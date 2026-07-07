namespace EquipmentTracker.Web.Models;

/// <summary>Result of the certification validation gate at checkout time.</summary>
public enum CertValidationOutcome
{
    /// <summary>Checkout was not subject to cert validation (no requirements for this category).</summary>
    NotRequired,

    /// <summary>Operator holds valid certifications for all requirements — checkout allowed.</summary>
    Passed,

    /// <summary>Operator is missing or has expired certifications — checkout blocked.</summary>
    Blocked,

    /// <summary>Checkout was blocked but a supervisor recorded an authorised override.</summary>
    Overridden
}
