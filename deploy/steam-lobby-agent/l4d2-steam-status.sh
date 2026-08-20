#!/usr/bin/env bash
set -u

steam_home="${HOME:-/home/default}/.steam/steam"
bootstrap_log="${steam_home}/logs/bootstrap_log.txt"

if [ -t 1 ] && [ "${TERM:-dumb}" != "dumb" ]; then
    reset=$'\033[0m'
    bold=$'\033[1m'
    dim=$'\033[2m'
    red=$'\033[31m'
    green=$'\033[32m'
    yellow=$'\033[33m'
    cyan=$'\033[36m'
else
    reset=''; bold=''; dim=''; red=''; green=''; yellow=''; cyan=''
fi

print_section() {
    printf '\n%s%s%s\n' "${cyan}" "${bold}$1" "${reset}"
}

print_state() {
    state="$1"
    label="$2"
    color="${yellow}"
    case "${state}" in
        ok) color="${green}"; symbol='[OK]' ;;
        fail) color="${red}"; symbol='[!!]' ;;
        running) color="${green}"; symbol='[>>]' ;;
        waiting) color="${yellow}"; symbol='[..]' ;;
    esac
    printf '%s%s%s %s\n' "${color}" "${symbol}" "${reset}" "${label}"
}

show_status() {
    clear
    printf '%s%sSteam 状态%s %s\n' "${bold}" "${cyan}" "${reset}" "${dim}每 2 秒刷新，按任意键关闭${reset}"
    printf '%s\n' "${dim}========================================${reset}"

    print_section 'Steam 服务'
    if steam_status="$(supervisorctl status steam 2>/dev/null)"; then
        case "${steam_status}" in
            *RUNNING*) print_state running 'Steam Supervisor 正在运行' ;;
            *) print_state fail 'Steam Supervisor 未运行' ;;
        esac
    else
        print_state fail 'Steam Supervisor 不可读取'
    fi

    print_section '客户端更新'
    if [ -r "${bootstrap_log}" ]; then
        update_line="$(grep 'Downloading update' "${bootstrap_log}" | tail -n 1 || true)"
        if [ -n "${update_line}" ]; then
            printf '%s%s%s\n' "${yellow}" "${update_line}" "${reset}"
            print_state waiting 'Steam 客户端仍在更新'
        else
            last_line="$(tail -n 1 "${bootstrap_log}")"
            printf '%s\n' "${last_line}"
            print_state ok '未检测到正在下载的更新'
        fi
    else
        print_state waiting '尚未生成 Steam 更新日志'
    fi

    print_section 'Agent 调度资格'
    if command -v curl >/dev/null 2>&1; then
        readiness="$(curl --noproxy '*' --silent --show-error --max-time 3 http://127.0.0.1:8080/v1/probe/status || true)"
        if [ -n "${readiness}" ]; then
            if printf '%s' "${readiness}" | grep -q '"ready":true'; then
                print_state ok 'ready=true：允许进入调度候选集'
            else
                failure="$(printf '%s' "${readiness}" | sed -n 's/.*"failure":"\([^"]*\)".*/\1/p')"
                [ -n "${failure}" ] || failure='未通过就绪检查'
                print_state fail "ready=false：${failure}，不会参与调度"
            fi
        else
            print_state fail '无法读取 Agent 就绪状态'
        fi
    else
        print_state fail 'curl 不可用，无法读取 Agent 就绪状态'
    fi
}

while true; do
    show_status
    read -r -t 2 _ && break
done
