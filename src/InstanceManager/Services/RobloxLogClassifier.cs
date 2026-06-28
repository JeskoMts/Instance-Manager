using System;

namespace InstanceManager.Services;

public enum RobloxSessionSignal
{
    InGame,

    Kicked,

    Error,

    GracefulLeave
}

public static class RobloxLogClassifier
{
    private static readonly string[] KickMarkers =
    {
        "error code: 267",
        "error code 267",
        "fehlercode: 267",
        "fehlercode 267",
        "errorcode 267",
        "errorcode: 267",
        "disconnect reason received: 267",
        "disconnection notification. reason: 267",
        "sending disconnect with reason: 267",
        "disconnect with reason: 267",
        "reason: 267",
        "you were kicked",
        "you have been kicked",
        "you were kicked or removed",
        "you have been removed",
        "kicked from this",
        "kicked or removed",
        "removed by the experience",
        "removed by the game",
        "removed by the moderator",
        "experience moderator",
        "moderation message",
        "du wurdest aus dieser experience gekickt",
        "gekickt oder von den moderatoren entfernt",
        "moderatoren entfernt",
        "moderationsnachricht",
        "you were removed",
        "removed from this experience",
    };

    private static readonly string[] ErrorMarkers =
    {
        "error code: 277",
        "error code 277",
        "fehlercode: 277",
        "fehlercode 277",
        "error code: 279",
        "error code 279",
        "fehlercode: 279",
        "fehlercode 279",
        "error code: 273",
        "error code 273",
        "error code: 268",
        "error code 268",
        "[flog::network] disconnect: reason",
        "sending disconnect with reason",
        "lostconnection",
        "lost connection",
        "connection lost",
        "verbindung getrennt",
        "disconnectedfromsecurity",
        "server has shut down",
        "server shut down",
        "server is shutting down",
        "server is offline",
        "server offline",
        "failed to connect",
        "connection closed",
    };

    private static readonly string[] InGameMarkers =
    {
        "[flog::network] replicator created",
        "client:connect",
        "join succeeded",
        "joining game",
        "[flog::gamejoinutil] joingame",
    };

    private static readonly string[] GracefulLeaveMarkers =
    {
        "leaveugcgameinternal",
        "[flog::singlesurfaceapp] leavegame",
        "user requested",
        "returning to app",
        "leaving game",
    };

    public static RobloxSessionSignal? Classify(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        string text = line.ToLowerInvariant();

        if (ContainsAny(text, KickMarkers))
            return RobloxSessionSignal.Kicked;
        if (ContainsAny(text, ErrorMarkers))
            return RobloxSessionSignal.Error;
        if (ContainsAny(text, GracefulLeaveMarkers))
            return RobloxSessionSignal.GracefulLeave;
        if (ContainsAny(text, InGameMarkers))
            return RobloxSessionSignal.InGame;

        return null;
    }

    private static bool ContainsAny(string text, string[] markers)
    {
        foreach (string marker in markers)
        {
            if (text.Contains(marker, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
