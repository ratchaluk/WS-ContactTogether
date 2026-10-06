namespace ContactTogetherApi.Helper;

/// <summary>
/// Response envelope. The HTTP status code carries success/failure for the transport; these
/// fields carry it for the client body.
/// </summary>
public class _0BaseReturn
{
    public bool CallAPIStatus { get; set; } = false;
    public string CallAPIStatusMessage { get; set; } = string.Empty;
    public object? Result { get; set; } = null;

    public _0BaseReturn() { }

    public static _0BaseReturn Success(object? result, string message = "") =>
        new() { CallAPIStatus = true, CallAPIStatusMessage = message, Result = result };

    public static _0BaseReturn Fail(string message) =>
        new() { CallAPIStatus = false, CallAPIStatusMessage = message };
}
