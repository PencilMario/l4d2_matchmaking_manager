import subprocess
import sys
import time

import warm


def main() -> int:
    if len(sys.argv) != 2 or not sys.argv[1].isdigit() or int(sys.argv[1]) == 0:
        print("usage: RestartEdgeL4d2Lobby.py <lobby-id>", file=sys.stderr)
        return 2

    lobby_id = sys.argv[1]
    steam_was_running = warm.is_process_running(warm.STEAM_PROCESS)
    if not warm.stop_l4d2() or not warm.start_steam():
        return 1

    time.sleep(2 if steam_was_running else warm.STEAM_READY_DELAY)
    command = [
        str(warm.STEAM_EXE),
        "-applaunch",
        warm.STEAM_APP_ID,
        "-novid",
        "+connect_lobby",
        lobby_id,
    ]
    subprocess.Popen(command, cwd=str(warm.STEAM_DIR))
    return 0 if warm.wait_for_process_stable(
        warm.GAME_PROCESS,
        warm.GAME_START_TIMEOUT,
        warm.GAME_STABILITY_DELAY,
    ) else 1


if __name__ == "__main__":
    raise SystemExit(main())
