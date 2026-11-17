namespace TrackManagement.Application.Common.Messaging;

/// <summary>
/// Non-generic marker carried by every command. Pipeline behaviors constrained to this type
/// (e.g. <c>UnitOfWorkBehavior</c>) are only ever closed over commands, never queries.
/// </summary>
public interface ICommandMarker;
