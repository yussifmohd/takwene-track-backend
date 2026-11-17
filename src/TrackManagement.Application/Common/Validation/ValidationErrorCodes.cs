namespace TrackManagement.Application.Common.Validation;

public static class ValidationErrorCodes
{
    /// <summary>
    /// The value clashes with existing data (e.g. a duplicate email). When every failure of a request
    /// carries this code, the HTTP edge answers 409 instead of 400, still naming the field.
    /// </summary>
    public const string Conflict = "conflict";
}
