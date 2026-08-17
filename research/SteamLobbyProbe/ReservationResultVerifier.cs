using System.Net;

internal static class ReservationResultVerifier
{
    internal static bool IsAccepted(
        int reservationResult,
        IPEndPoint endpoint,
        ulong lobbyId,
        string? rconPassword,
        Func<IPEndPoint, string, ulong, bool> verifyReservation)
    {
        ArgumentNullException.ThrowIfNull(verifyReservation);
        if (reservationResult == 0)
            return true;

        return reservationResult == ReservationCommand.ReservationTimeoutExitCode &&
            !string.IsNullOrWhiteSpace(rconPassword) &&
            verifyReservation(endpoint, rconPassword, lobbyId);
    }
}
