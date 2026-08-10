// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core;

/// <summary>
///     A server-side failure whose own exception type this client cannot rebuild
///     (wire-protocol W5).
/// </summary>
/// <remarks>
///     Raised when <see cref="InfoCarrierFaultMapper" /> cannot resolve the server's exception
///     type in this process, or when that type has no constructor an exception can be rebuilt
///     through. The commonest cause is legitimate and not worth working around: a store-specific
///     exception such as <c>SqliteException</c> is a type the client assembly has no reason to
///     reference, and making every client reference every backend would be a worse trade than
///     losing the type name from a <c>catch</c> clause. It is still in
///     <see cref="ServerExceptionTypeName" /> and in the message.
/// </remarks>
public sealed class InfoCarrierServerException : Exception
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InfoCarrierServerException" /> class.
    /// </summary>
    /// <param name="serverExceptionTypeName">The CLR type name the server reported.</param>
    /// <param name="message">The server's message, verbatim.</param>
    /// <param name="innerException">The rebuilt inner failure, if there was one.</param>
    public InfoCarrierServerException(
        string serverExceptionTypeName, string message, Exception? innerException = null)
        : base(message, innerException)
        => ServerExceptionTypeName = serverExceptionTypeName;

    /// <summary>
    ///     The CLR type name of the exception the server actually threw.
    /// </summary>
    public string ServerExceptionTypeName { get; }
}
