#!/usr/bin/env bash
set -e

if [ "${ENABLE_STEAM:-}" = "true" ]; then
    # Allow the Agent service user (a member of sudo) to control only Supervisor.
    sed -i 's|^;chmod=0700.*$|chmod=0660|' /etc/supervisord.conf
    sed -i 's|^;chown=nobody:nogroup.*$|chown=root:sudo|' /etc/supervisord.conf

    steam_root="${USER_HOME:-/home/default}/.steam/steam"
    steam_runtime_root="${USER_HOME:-/home/default}/.steam"
    library_directory="${steam_root}/steamapps"
    library_file="${library_directory}/libraryfolders.vdf"
    steam_config_directory="${steam_root}/config"
    steam_config_file="${steam_config_directory}/config.vdf"
    steam_region_status_file="${STEAM_DOWNLOAD_REGION_STATUS_FILE:-${steam_config_directory}/steam-download-region}"
    steam_download_region_id="${STEAM_DOWNLOAD_REGION_ID:-}"
    legacy_steam_download_region="${STEAM_DOWNLOAD_REGION:-}"
    millennium_install_directory="/usr/lib/millennium"
    millennium_plugin_directory="${USER_HOME:-/home/default}/.local/share/millennium/plugins/steam-region-bridge"
    millennium_config_directory="${USER_HOME:-/home/default}/.config/millennium"
    millennium_config_file="${millennium_config_directory}/config.json"
    millennium_region_target_file="${millennium_config_directory}/steam-region-bridge-region"
    millennium_plugin_source="${STEAM_REGION_BRIDGE_SOURCE:-/opt/steam-region-bridge}"
    millennium_ui_memory_saver_plugin_directory="${USER_HOME:-/home/default}/.local/share/millennium/plugins/steam-ui-memory-saver"
    millennium_ui_memory_saver_plugin_source="${STEAM_UI_MEMORY_SAVER_SOURCE:-/opt/steam-ui-memory-saver}"
    steam_webhelper_wrapper="${steam_runtime_root}/ubuntu12_64/steamwebhelper_sniper_wrap.sh"
    steam_webhelper_wrapper_backup="${steam_runtime_root}/ubuntu12_64/steamwebhelper_sniper_wrap.sh.valve-original"
    millennium_archive_url="https://github.com/SteamClientHomebrew/Millennium/releases/download/v3.4.1/millennium-v3.4.1-linux-x86_64.tar.gz"
    millennium_archive_sha256="5f2f6f73915523a7b3f7ecc500dd3e6ed0e5c88a1b1db6584c40f173aa9d13d4"

    install_millennium() {
        if [ -x "${millennium_install_directory}/libmillennium_x86.so" ] &&
            [ -x "${millennium_install_directory}/libmillennium_bootstrap_x86.so" ] &&
            [ -x "${millennium_install_directory}/libmillennium_bootstrap_hhx64.so" ]; then
            return 0
        fi

        command -v curl >/dev/null 2>&1 || {
            echo 'Millennium installation requires curl.' >&2
            return 1
        }
        command -v tar >/dev/null 2>&1 || {
            echo 'Millennium installation requires tar.' >&2
            return 1
        }
        command -v sha256sum >/dev/null 2>&1 || {
            echo 'Millennium installation requires sha256sum.' >&2
            return 1
        }

        archive_file="$(mktemp /tmp/millennium-v3.4.1.XXXXXX.tar.gz)"
        extract_directory="$(mktemp -d /tmp/millennium-v3.4.1.XXXXXX)"
        trap 'rm -f "${archive_file:-}"; rm -rf "${extract_directory:-}"' RETURN
        curl --fail --location --retry 3 --output "${archive_file}" "${millennium_archive_url}"
        printf '%s  %s\n' "${millennium_archive_sha256}" "${archive_file}" | sha256sum --check --status
        tar --extract --gzip --file "${archive_file}" --directory "${extract_directory}"
        [ -d "${extract_directory}/usr/lib/millennium" ] || {
            echo 'Millennium archive has an unexpected layout.' >&2
            return 1
        }
        install -d -m 0755 "${millennium_install_directory}"
        cp -a "${extract_directory}/usr/lib/millennium/." "${millennium_install_directory}/"
        chmod 0755 "${millennium_install_directory}"/*
    }

    install_steam_region_bridge() {
        [ -d "${millennium_plugin_source}" ] || {
            echo "Missing Steam Region Bridge source: ${millennium_plugin_source}" >&2
            return 1
        }
        [ -f "${millennium_plugin_source}/plugin.json" ] || {
            echo 'Steam Region Bridge source is missing plugin.json.' >&2
            return 1
        }
        [ -f "${millennium_plugin_source}/backend/main.lua" ] || {
            echo 'Steam Region Bridge source is missing backend/main.lua.' >&2
            return 1
        }
        [ -f "${millennium_plugin_source}/.millennium/Dist/index.js" ] || {
            echo 'Steam Region Bridge source is missing the compiled frontend bundle.' >&2
            return 1
        }

        install -d -o "${PUID:-1000}" -g "${PGID:-1000}" -m 0700 "$(dirname "${millennium_plugin_directory}")"
        install -d -o "${PUID:-1000}" -g "${PGID:-1000}" -m 0700 "${millennium_config_directory}"
        cp -a "${millennium_plugin_source}/." "${millennium_plugin_directory}/"
        chown -R "${PUID:-1000}:${PGID:-1000}" "${millennium_plugin_directory}"

        if [ ! -f "${millennium_config_file}" ]; then
            printf '%s\n' '{"plugins":{"enabledPlugins":[]}}' > "${millennium_config_file}"
        fi
        command -v jq >/dev/null 2>&1 || {
            echo 'Millennium plugin activation requires jq.' >&2
            return 1
        }
        temporary_file="$(mktemp "${millennium_config_file}.tmp.XXXXXX")"
        jq --arg plugin 'steam-region-bridge' \
            '.plugins.enabledPlugins = (((.plugins.enabledPlugins // []) + [$plugin]) | unique)' \
            "${millennium_config_file}" > "${temporary_file}"
        chown "${PUID:-1000}:${PGID:-1000}" "${temporary_file}"
        chmod 0600 "${temporary_file}"
        mv "${temporary_file}" "${millennium_config_file}"
    }

    install_steam_ui_memory_saver() {
        [ -d "${millennium_ui_memory_saver_plugin_source}" ] || {
            echo "Missing Steam UI Memory Saver source: ${millennium_ui_memory_saver_plugin_source}" >&2
            return 1
        }
        [ -f "${millennium_ui_memory_saver_plugin_source}/plugin.json" ] || {
            echo 'Steam UI Memory Saver source is missing plugin.json.' >&2
            return 1
        }
        [ -f "${millennium_ui_memory_saver_plugin_source}/backend/main.lua" ] || {
            echo 'Steam UI Memory Saver source is missing backend/main.lua.' >&2
            return 1
        }
        [ -f "${millennium_ui_memory_saver_plugin_source}/.millennium/Dist/index.js" ] || {
            echo 'Steam UI Memory Saver source is missing the compiled frontend bundle.' >&2
            return 1
        }

        install -d -o "${PUID:-1000}" -g "${PGID:-1000}" -m 0700 "$(dirname "${millennium_ui_memory_saver_plugin_directory}")"
        install -d -o "${PUID:-1000}" -g "${PGID:-1000}" -m 0700 "${millennium_config_directory}"
        cp -a "${millennium_ui_memory_saver_plugin_source}/." "${millennium_ui_memory_saver_plugin_directory}/"
        chown -R "${PUID:-1000}:${PGID:-1000}" "${millennium_ui_memory_saver_plugin_directory}"

        if [ ! -f "${millennium_config_file}" ]; then
            printf '%s\n' '{"plugins":{"enabledPlugins":[]}}' > "${millennium_config_file}"
        fi
        command -v jq >/dev/null 2>&1 || {
            echo 'Millennium plugin activation requires jq.' >&2
            return 1
        }
        temporary_file="$(mktemp "${millennium_config_file}.tmp.XXXXXX")"
        jq --arg plugin 'steam-ui-memory-saver' \
            '.plugins.enabledPlugins = (((.plugins.enabledPlugins // []) + [$plugin]) | unique)' \
            "${millennium_config_file}" > "${temporary_file}"
        chown "${PUID:-1000}:${PGID:-1000}" "${temporary_file}"
        chmod 0600 "${temporary_file}"
        mv "${temporary_file}" "${millennium_config_file}"
    }

    install_millennium
    install_steam_region_bridge
    install_steam_ui_memory_saver

    temporary_file="$(mktemp "${millennium_region_target_file}.tmp.XXXXXX")"
    printf '%s\n' "${steam_download_region_id}" > "${temporary_file}"
    chown "${PUID:-1000}:${PGID:-1000}" "${temporary_file}"
    chmod 0600 "${temporary_file}"
    mv "${temporary_file}" "${millennium_region_target_file}"

    # Retain the environment target for compatibility; Millennium reads the account-local file above.
    export STEAM_DOWNLOAD_REGION_ID="${steam_download_region_id}"
    export STEAM_DOWNLOAD_REGION="${legacy_steam_download_region}"
    export STEAM_DOWNLOAD_REGION_STATUS_FILE="${steam_region_status_file}"

    install -d -o "${PUID:-1000}" -g "${PGID:-1000}" -m 0700 \
        "${steam_runtime_root}/ubuntu12_32" "${steam_runtime_root}/ubuntu12_64" \
        "${steam_root}/ubuntu12_32"
    ln -sfn "${steam_runtime_root}/ubuntu12_32/steam" \
        "${steam_root}/ubuntu12_32/steam"
    ln -sfn "${millennium_install_directory}/libmillennium_bootstrap_x86.so" \
        "${steam_runtime_root}/ubuntu12_32/libXtst.so.6"
    ln -sfn "${millennium_install_directory}/libmillennium_bootstrap_hhx64.so" \
        "${steam_runtime_root}/ubuntu12_64/libXtst.so.6"
    ln -sfn "${millennium_install_directory}/libmillennium_hhx64.so" \
        "${steam_runtime_root}/ubuntu12_64/libmillennium_hhx64.so"

    set_l4d2_update_policy() {
        manifest="${STEAM_SHARED_LIBRARY_PATH:-}/steamapps/appmanifest_550.acf"
        [ -n "${STEAM_SHARED_LIBRARY_PATH:-}" ] && [ -f "${manifest}" ] || return 0
        sed -i 's|"AutoUpdateBehavior"[[:space:]]*"[^"]*"|"AutoUpdateBehavior" "1"|' "${manifest}"
    }

    set_vnc_autostart() {
        case "$1" in
            true|false) ;;
            *)
                echo 'VNC autostart must be true or false.' >&2
                return 1
                ;;
        esac

        sed -i "s|^autostart=.*$|autostart=$1|" /etc/supervisor.d/vnc.ini
    }

    set_desktop_autostart() {
        case "$1" in
            true|false) ;;
            *)
                echo 'Desktop autostart must be true or false.' >&2
                return 1
                ;;
        esac

        sed -i "s|^autostart=.*$|autostart=$1|" /etc/supervisor.d/desktop.ini
    }

    set_steam_webhelper_guard_autostart() {
        case "$1" in
            true|false) ;;
            *)
                echo 'Steam webhelper guard autostart must be true or false.' >&2
                return 1
                ;;
        esac

        sed -i "s|^autostart=.*$|autostart=$1|" /etc/supervisor.d/steamwebhelper-guard.ini
    }

    restore_steam_webhelper_wrapper() {
        [ -f "${steam_webhelper_wrapper_backup}" ] || return 0
        if [ -f "${steam_webhelper_wrapper}" ] && cmp -s "${steam_webhelper_wrapper_backup}" "${steam_webhelper_wrapper}"; then
            return 0
        fi

        install -d -o "${PUID:-1000}" -g "${PGID:-1000}" -m 0700 "$(dirname "${steam_webhelper_wrapper}")"
        temporary_file="$(mktemp "${steam_webhelper_wrapper}.restore.XXXXXX")"
        cp --preserve=mode,timestamps "${steam_webhelper_wrapper_backup}" "${temporary_file}"
        chown "${PUID:-1000}:${PGID:-1000}" "${temporary_file}"
        chmod 0700 "${temporary_file}"
        mv -f "${temporary_file}" "${steam_webhelper_wrapper}"
    }

    configure_steam_ui_mode() {
        login_ui_mode="${STEAM_LOGIN_UI_MODE:-auto}"
        steam_no_vnc_arguments='-silent -no-browser -dev -console -nofriendsui -no-dwrite -nointro -nobigpicture -nofasthtml -nocrashmonitor -noshaders -no-shared-textures -disablehighdpi -cef-single-process -cef-in-process-gpu -single_core -cef-disable-d3d11 -cef-disable-sandbox -disable-winh264 -cef-force-32bit -no-cef-sandbox -vrdisable -cef-disable-breakpad'
        case "${login_ui_mode}" in
            auto|always|never) ;;
            *)
                echo 'STEAM_LOGIN_UI_MODE must be auto, always or never.' >&2
                return 1
                ;;
        esac

        loginusers_file="${steam_config_directory}/loginusers.vdf"
        login_ready_marker="${steam_config_directory}/l4d2-agent-ready"
        enable_vnc=true
        enable_steam_webhelper_guard=false
        case "${login_ui_mode}" in
            always)
                steam_arguments=''
                ;;
            never)
                steam_arguments="${steam_no_vnc_arguments}"
                enable_vnc=false
                enable_steam_webhelper_guard=true
                ;;
            auto)
                if { [ -f "${loginusers_file}" ] && grep -Eq '"MostRecent"[[:space:]]*"1"' "${loginusers_file}"; } || [ -f "${login_ready_marker}" ]; then
                    steam_arguments="${steam_no_vnc_arguments}"
                    enable_vnc=false
                    enable_steam_webhelper_guard=true
                else
                    steam_arguments='-vgui -no-browser'
                    enable_steam_webhelper_guard=false
                fi
                ;;
        esac

        restore_steam_webhelper_wrapper
        sed -i "s|^command=.*$|command=/usr/games/steam ${steam_arguments}|" /etc/supervisor.d/steam.ini
        set_vnc_autostart "${enable_vnc}"
        set_desktop_autostart "${enable_vnc}"
        set_steam_webhelper_guard_autostart "${enable_steam_webhelper_guard}"
    }

    if [ -n "${STEAM_SHARED_LIBRARY_PATH:-}" ]; then
        install -d -o "${PUID:-1000}" -g "${PGID:-1000}" -m 0700 "${library_directory}"
        temporary_file="$(mktemp "${library_file}.tmp.XXXXXX")"
        printf '%s\n' \
            '"libraryfolders"' \
            '{' \
            '    "0"' \
            '    {' \
            "        \"path\" \"${steam_root}\"" \
            '        "label" "Home"' \
            '        "apps" {}' \
            '    }' \
            '    "1"' \
            '    {' \
            "        \"path\" \"${STEAM_SHARED_LIBRARY_PATH}\"" \
            '        "label" "Shared L4D2"' \
            '        "apps" {}' \
            '    }' \
            '}' > "${temporary_file}"
        chown "${PUID:-1000}:${PGID:-1000}" "${temporary_file}"
        chmod 0600 "${temporary_file}"
        mv "${temporary_file}" "${library_file}"
    fi

    install -d -o "${PUID:-1000}" -g "${PGID:-1000}" -m 0700 "${steam_config_directory}"
    if [ ! -f "${steam_config_file}" ]; then
        temporary_file="$(mktemp "${steam_config_file}.tmp.XXXXXX")"
        printf '%s\n' \
            '"InstallConfigStore"' \
            '{' \
            '    "Software"' \
            '    {' \
            '        "Valve"' \
            '        {' \
            '            "Steam"' \
            '            {' \
            '                "ShaderCacheManager"' \
            '                {' \
            '                    "DisableShaderCache" "1"' \
            '                }' \
            '            }' \
            '        }' \
            '    }' \
            '}' > "${temporary_file}"
        chown "${PUID:-1000}:${PGID:-1000}" "${temporary_file}"
        chmod 0600 "${temporary_file}"
        mv "${temporary_file}" "${steam_config_file}"
    elif grep -q '"DisableShaderCache"' "${steam_config_file}"; then
        sed -i 's|"DisableShaderCache"[[:space:]]*"[^"]*"|"DisableShaderCache" "1"|' "${steam_config_file}"
    elif grep -q '"ShaderCacheManager"' "${steam_config_file}"; then
        sed -i '/^[[:space:]]*"ShaderCacheManager"[[:space:]]*$/ { n; a\
                    "DisableShaderCache" "1"
        }' "${steam_config_file}"
    else
        sed -i '/^[[:space:]]*"Steam"[[:space:]]*$/ { n; a\
                "ShaderCacheManager"\
                {\
                    "DisableShaderCache" "1"\
                }
        }' "${steam_config_file}"
    fi
    chown "${PUID:-1000}:${PGID:-1000}" "${steam_config_file}"
    chmod 0600 "${steam_config_file}"
    set_l4d2_update_policy
    configure_steam_ui_mode

    sed -i 's|^autostart=.*$|autostart=true|' /etc/supervisor.d/steam.ini
fi
