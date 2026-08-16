import subprocess
import sys
from pathlib import Path


WARMSV_DIRECTORY = Path(r"C:\Users\Administrator\Desktop\warmsv")
sys.path.insert(0, str(WARMSV_DIRECTORY))

import warm  # noqa: E402


command = [
    str(warm.STEAM_EXE),
    "-applaunch",
    warm.STEAM_APP_ID,
    "-novid",
    "-console",
    "-condebug",
]
subprocess.Popen(command, cwd=str(warm.STEAM_DIR))
stable = warm.wait_for_process_stable(
    warm.GAME_PROCESS,
    warm.GAME_START_TIMEOUT,
    warm.GAME_STABILITY_DELAY,
)
raise SystemExit(0 if stable else 1)
