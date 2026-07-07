namespace EquipmentTracker.Web.Models;

/// <summary>
/// A document (PDF, JPEG, PNG) uploaded as evidence for an <see cref="OperatorCertRecord"/>.
/// Append-only — no delete or edit operations are permitted post-creation.
/// Maximum file size: 10 MB. Retained for at least 5 years (OSHA 29 CFR 1910.1020).
/// </summary>
public class CertDocument
{
    public int Id { get; set; }

    public int OperatorCertRecordId { get; set; }

    /// <summary>Original file name provided by the uploader.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME type: application/pdf, image/jpeg, or image/png.</summary>
    public string FileType { get; set; } = string.Empty;

    /// <summary>Server-side stored file name / path (or blob key).</summary>
    public string StoredFileName { get; set; } = string.Empty;

    /// <summary>Identity of the person who uploaded the document.</summary>
    public string UploadedBy { get; set; } = string.Empty;

    /// <summary>UTC timestamp recorded by the server at upload time.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
