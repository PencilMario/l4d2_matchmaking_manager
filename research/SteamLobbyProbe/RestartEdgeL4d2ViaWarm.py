import sys
import time

import warm


def main() -> int:
    warm.stop_l4d2()
    warm.stop_steam()
    if not warm.start_steam():
        return 1

    time.sleep(warm.STEAM_READY_DELAY)
    return 0 if warm.start_l4d2(("202.105.108.88", 27081)) else 1


if __name__ == "__main__":
    sys.exit(main())
