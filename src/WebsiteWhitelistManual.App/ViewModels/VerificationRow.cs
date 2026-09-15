// src/WebsiteWhitelistManual.App/ViewModels/VerificationRow.cs
namespace WebsiteWhitelistManual.App.ViewModels;

public sealed record VerificationRow(string Label, bool Passed, string Detail);
