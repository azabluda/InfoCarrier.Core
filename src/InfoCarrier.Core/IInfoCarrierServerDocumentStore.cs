// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core;

/// <summary>
///     Present in a <em>server's</em> service collection when that server's store writes an owned
///     type inside its owner's record rather than beside it (#102).
/// </summary>
/// <remarks>
///     <para>
///         <b>The presence of this service is the whole statement</b>, as it is for
///         <see cref="IInfoCarrierArbitrarySqlExecution" />. It carries no data because there is
///         nothing to enumerate: the answer is a property of the store, and it is the same answer
///         for every entity type in the model. Registered with
///         <see cref="InfoCarrierServiceCollectionExtensions.AddInfoCarrierServerDocumentStore" />,
///         and absent by default.
///     </para>
///     <para>
///         <b>What it buys is that a change set which does not mention part of a document no
///         longer erases it.</b> A document store has no partial write: changing a customer's name
///         writes the whole customer document, so an address the change set never mentioned is not
///         merely unchanged, it is gone. The save reports success and the next read fails on a
///         field that is no longer there. That is #100, and the client half of it — sending the
///         whole document — is
///         <see cref="InfoCarrierDbContextOptionsBuilder.UseNonRelationalServerStore" />.
///     </para>
///     <para>
///         <b>This is the half that does not depend on the client being configured correctly.</b>
///         The client half can only send what its own change tracker holds, so it is defeated two
///         ways: a deployment that forgot to call
///         <c>UseNonRelationalServerStore()</c> sends a bare root, and a client that attached a
///         stub — <c>Attach(new Customer { Id = id, Name = name })</c>, the ordinary relational way
///         to update one field without reading the row — sends a bare root even with the switch on.
///         Registered here, the server reads the stored document and supplies whatever the change
///         set left out, so both write a complete document.
///     </para>
///     <para>
///         <b>It costs one read, and only where the change set is actually incomplete.</b> The
///         server asks the store for the document exactly when the model says an owned navigation
///         could hold something and no entry for it arrived; a change set that mentions every
///         owned navigation of the root is written as it stands, which is what a correctly
///         configured client sends. Roots being inserted and roots being deleted are never read:
///         there is nothing to preserve in the first case and nothing to keep in the second.
///     </para>
///     <para>
///         <b>The deployment says it rather than the server sniffing it, because the obvious sniff
///         answers a different question.</b> <c>DbContext.Database.IsRelational()</c> is false for
///         EF's in-memory provider too, and that store gives an owned type storage of its own —
///         reading a document back into it would be work bought for nothing. Nor does the model
///         say it: on MongoDB a document root carries a <c>Mongo:CollectionName</c> annotation and
///         an owned type carries no annotation at all, while <c>IsOwned()</c> and
///         <c>FindOwnership()</c> are true of relational table splitting in exactly the same shape.
///         There is no store-agnostic API for the question, so it is asked of the deployment,
///         which knows.
///     </para>
/// </remarks>
public interface IInfoCarrierServerDocumentStore
{
}
