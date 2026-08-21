#!/usr/bin/env bash
set -euo pipefail

user_home="${USER_HOME:-/home/default}"
wrapper_path="${user_home}/.steam/ubuntu12_64/steamwebhelper_sniper_wrap.sh"
backup_path="${user_home}/.steam/ubuntu12_64/steamwebhelper_sniper_wrap.sh.valve-original"
shim_path="${STEAM_WEBHELPER_GUARD_SHIM_SOURCE:-/usr/local/lib/steamwebhelper-guard-shim.sh}"
probe_url="${STEAM_WEBHELPER_GUARD_PROBE_URL:-http://127.0.0.1:8080/v1/probe/status}"
poll_seconds="${STEAM_WEBHELPER_GUARD_POLL_SECONDS:-15}"
grace_seconds="${STEAM_WEBHELPER_GUARD_GRACE_SECONDS:-60}"

log() {
    printf '%s steamwebhelper-guard: %s\n' "$(date -Iseconds)" "$*"
}

is_valve_wrapper() {
    grep -Fqx 'exec ./steamwebhelper "$@"' "$1"
}

is_guard_shim() {
    cmp -s "${wrapper_path}" "${shim_path}"
}

save_valve_wrapper() {
    temporary_file="$(mktemp "${backup_path}.tmp.XXXXXX")"
    cp --preserve=mode,timestamps "${wrapper_path}" "${temporary_file}"
    mv -f "${temporary_file}" "${backup_path}"
}

replace_with_guard_shim() {
    temporary_file="$(mktemp "${wrapper_path}.tmp.XXXXXX")"
    cp --preserve=mode "${shim_path}" "${temporary_file}"
    chmod 0700 "${temporary_file}"
    mv -f "${temporary_file}" "${wrapper_path}"
}

agent_is_ready() {
    curl --noproxy '*' --fail --silent --show-error --max-time 3 "${probe_url}" |
        jq -e '.ready == true' >/dev/null 2>&1
}

wait_for_agent_ready() {
    while ! agent_is_ready; do
        sleep 2
    done
}

wait_for_stable_agent_ready() {
    while true; do
        wait_for_agent_ready
        log "Agent is ready; waiting ${grace_seconds} seconds before blocking Chromium helpers"
        sleep "${grace_seconds}"

        if agent_is_ready; then
            log "Agent stayed ready through the Chromium helper grace period"
            return 0
        fi

        log "Agent readiness dropped during the grace period; waiting again"
    done
}

ensure_guard_shim() {
    guard_replaced=false

    if [ ! -x "${shim_path}" ]; then
        log "shim is unavailable; leaving the Valve wrapper unchanged"
        return 1
    fi
    if [ ! -f "${wrapper_path}" ]; then
        log "Valve wrapper is unavailable; leaving Steam unchanged"
        return 1
    fi
    if is_guard_shim; then
        return 0
    fi
    if ! is_valve_wrapper "${wrapper_path}"; then
        log "Valve wrapper has an unrecognized format; leaving Steam unchanged"
        return 1
    fi

    save_valve_wrapper
    replace_with_guard_shim
    guard_replaced=true
    log "installed lightweight wrapper shim"
}

wait_for_stable_agent_ready

while true; do
    if ! ensure_guard_shim; then
        exit 0
    fi

    if [ "${guard_replaced}" = true ]; then
        pkill -TERM -x steamwebhelper || true
        log "terminated existing Chromium helper processes"
    fi

    sleep "${poll_seconds}"
done
